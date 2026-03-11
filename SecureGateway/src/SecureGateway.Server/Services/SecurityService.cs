using System.Security.Cryptography;
using System.Text;
using OtpNet;
using SecureGateway.Server.Interfaces;
using SecureGateway.Shared.DTOs;

namespace SecureGateway.Server.Services;

public class SecurityService : ISecurityService
{
    public bool IsValidSignature(CredentialRequest request, string clientSecret)
    {
        var payload = $"{request.ClientId}:{request.ResourceKey}";
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var computedHash = Convert.ToBase64String(hashBytes);

        return computedHash == request.RequestHash;
    }

    public bool IsValidMfa(string secretSeed, string providedCode)
    {
        var base32Bytes = Base32Encoding.ToBytes(secretSeed); 
        var totp = new Totp(base32Bytes);

        return totp.VerifyTotp(providedCode, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }
}