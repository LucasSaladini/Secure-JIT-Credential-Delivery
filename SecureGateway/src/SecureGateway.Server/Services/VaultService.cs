using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureGateway.Server.Services;

public interface IVaultService
{
    Task<string> GetSecretAsync(string resourceKey);
}

public class VaultService : IVaultService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<VaultService> _logger;

    public VaultService(IConfiguration configuration, ILogger<VaultService> logger)
    {
        _logger = logger;
        var vaultUri = configuration["KeyVaultUri"] ?? throw new InvalidOperationException("Key Vault Uri not defined.");
        _secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string resourceKey)
    {
        try
        {
            _logger.LogInformation("Retrieving secret for resource: {ResourceKey}", resourceKey);

            KeyVaultSecret secret = await _secretClient.GetSecretAsync(resourceKey);

            return secret.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {ResourceKey} from Key Vault", resourceKey);
            throw new InvalidOperationException("Could not retrieve the requested resource.");
        }
    }
}