using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SecureGateway.Server.Services;

public interface IVaultService
{
    Task<string> GetSecretAsync(string resourceKey);
}

public interface IKeyVaultClient
{
    Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default);
}

// Implementação Real
public class KeyVaultClientWrapper(SecretClient secretClient) : IKeyVaultClient
{
    public Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default) 
        => secretClient.GetSecretAsync(name, version, cancellationToken);
}

// Implementação para Testes (Removido o Mock.Of para evitar dependência de biblioteca de mock no código de produção)
public class FakeKeyVaultClient : IKeyVaultClient
{
    public async Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken ct = default)
    {
        await Task.Delay(50);
        
        var secret = new KeyVaultSecret(name, $"fake-value-for-{name}");
        
        return Response.FromValue(secret, null!); 
    }
}

public class VaultService : IVaultService
{
    private readonly IKeyVaultClient _client;
    private readonly ILogger<VaultService> _logger;
    private readonly Histogram<double> _vaultRequestDuration;
    private readonly ResiliencePipeline _pipeline;

    public VaultService(
        IKeyVaultClient client,
        ILogger<VaultService> logger, 
        Meter meter, 
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _client = client;
        _logger = logger;
        
        _pipeline = pipelineProvider.GetPipeline("vault-strategy"); 

        _vaultRequestDuration = meter.CreateHistogram<double>(
            "vault_request_duration_ms", 
            unit: "ms");
    }

    public async Task<string> GetSecretAsync(string resourceKey)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("VaultOperation: GetSecret | Status: Started | Resource: {ResourceKey}", resourceKey);
            
            return await _pipeline.ExecuteAsync(async ct =>
            {
                var secretResponse = await _client.GetSecretAsync(resourceKey, cancellationToken: ct);
                return secretResponse.Value.Value;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VaultOperation: GetSecret | Status: Failed | Resource: {ResourceKey}", resourceKey);
            throw;
        }
        finally
        {
            sw.Stop();
            _vaultRequestDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList { { "resource", resourceKey } });
        }
    }
}