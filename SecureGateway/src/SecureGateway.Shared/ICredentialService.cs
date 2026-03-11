namespace SecureGateway.Server.Interfaces;

public interface ICredentialService
{
    Task<string> GetSecureCredentialAsync(string secretName);
}