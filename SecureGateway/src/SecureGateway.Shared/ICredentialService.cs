namespace SecureGatewat.Shared;

public interface ICredentialService
{
    Task<string> GetSecureCredentialAsync(string secretName);
}