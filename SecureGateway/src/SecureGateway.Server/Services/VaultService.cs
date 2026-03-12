using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SecureGateway.Server.Services;

public interface IVaultService
{
    Task<string> GetSecretAsync(string resourceKey);
}

public class VaultService : IVaultService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<VaultService> _logger;
    private readonly Histogram<double> _vaultRequestDuration;

    public VaultService(IConfiguration configuration, ILogger<VaultService> logger, Meter meter)
    {
        _logger = logger;
        var vaultUri = configuration["KeyVaultUri"] ?? throw new InvalidOperationException("Key Vault Uri not defined.");
        _secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());

        _vaultRequestDuration = meter.CreateHistogram<double>(
            "vault_request_duration_ms", 
            unit: "ms", 
            description: "Tempo de resposta das requisições ao Azure Key Vault");
    }

    public async Task<string> GetSecretAsync(string resourceKey)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("VaultOperation: GetSecret | Status: Started | Resource: {ResourceKey}", resourceKey);
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(resourceKey);
            return secret.Value;
        }
        finally
        {
            sw.Stop();
            // Registra a métrica independente de sucesso ou erro
            _vaultRequestDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList { { "resource", resourceKey } });
        }
    }
}