using System.Security.Cryptography;
using System.Text;
using SecureGateway.Server.Services;
using SecureGateway.Shared.DTOs;
using Xunit;

namespace SecureGateway.Tests;

public class SecurityServiceTests
{
    private readonly SecurityService _service;
    private const string SharedSecret = "SUPER_SECRET_KEY_123";

    public SecurityServiceTests()
    {
        _service = new SecurityService();
    }

    [Fact]
    public void IsValidSignature_ValidHash_ReturnsTrue()
    {
        // Arrange
        var clientId = "LegacySystem01";
        var resourceKey = "DatabaseConnectionString";
        
        var expectedHash = GenerateTestHash(clientId, resourceKey, SharedSecret);
        
        var request = new CredentialRequest(clientId, resourceKey, expectedHash);

        // Act
        var result = _service.IsValidSignature(request, SharedSecret);

        // Assert
        Assert.True(result, "A assinatura válida deveria ser aceita.");
    }

    [Theory]
    [InlineData("WrongSecret")]
    [InlineData("ModifiedHash")]
    [InlineData("")]
    public void IsValidSignature_InvalidData_ReturnsFalse(string fakeSecretOrHash)
    {
        // Arrange
        var request = new CredentialRequest("ClientA", "ResA", fakeSecretOrHash);

        // Act
        var result = _service.IsValidSignature(request, SharedSecret);

        // Assert
        Assert.False(result, $"A assinatura com '{fakeSecretOrHash}' deveria ser rejeitada.");
    }

    [Fact]
    public void IsValidSignature_TamperedPayload_ReturnsFalse()
    {
        // Arrange
        var validHashForA = GenerateTestHash("Client1", "ResourceA", SharedSecret);
        var tamperedRequest = new CredentialRequest("Client1", "ResourceB", validHashForA);

        // Act
        var result = _service.IsValidSignature(tamperedRequest, SharedSecret);

        // Assert
        Assert.False(result, "O sistema não deve aceitar um hash gerado para um recurso diferente.");
    }

    private string GenerateTestHash(string clientId, string resourceKey, string secret)
    {
        var payload = $"{clientId}:{resourceKey}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hashBytes);
    }
}