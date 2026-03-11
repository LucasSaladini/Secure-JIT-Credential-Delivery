using SecureGateway.Shared.DTOs;

namespace SecureGateway.Server.Interfaces;

public interface ISecurityService
{
    bool IsValidSignature(CredentialRequest request, string clientSecret);
}