using Azure.Security.KeyVault.Secrets;
using SecureGateway.Server.Interfaces;

namespace SecureGateway.Server.Services;

public class CredentialService : ICredentialService
{
    private readonly SecretClient _secretClient;

    public CredentialService(SecretClient secretClient)
    {
        _secretClient = secretClient;
    }

    public async Task<string> GetSecureCredentialAsync(string secretName)
    {
        KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);

        return secret.Value;
    }
}