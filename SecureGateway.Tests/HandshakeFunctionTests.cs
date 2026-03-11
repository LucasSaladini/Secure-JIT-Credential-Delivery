using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SecureGateway.Server.Functions;
using SecureGateway.Shared.DTOs;
using SecureGateway.Server.Services;
using SecureGateway.Server.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SecureGateway.Tests;

public class HandshakeFunctionTests
{
    private readonly Mock<ISecurityService> _securityMock;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<IVaultService> _vaultMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly HandshakeFunction _function;

    public HandshakeFunctionTests()
    {
        _securityMock = new Mock<ISecurityService>();
        _auditMock = new Mock<IAuditService>();
        _vaultMock = new Mock<IVaultService>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                          .Returns(new Mock<ILogger>().Object);

        _function = new HandshakeFunction(
            _securityMock.Object, 
            _loggerFactoryMock.Object, 
            _auditMock.Object, 
            _vaultMock.Object);
    }

    [Fact]
    public async Task Run_ShouldReturnUnauthorized_WhenSignatureIsInvalid()
    {
        // Arrange
        var requestDTO = new CredentialRequest("AppId", "Key", "InvalidHash");
        var context = new Mock<FunctionContext>();
        var httpRequestMock = CreateMockHttpRequestData(requestDTO, context);

        // Match amplo para garantir que o Moq pegue a chamada
        _securityMock.Setup(s => s.IsValidSignature(It.IsAny<CredentialRequest>(), It.IsAny<string>()))
                    .Returns(false);

        // Act
        var response = await _function.Run(httpRequestMock);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_ShouldReturnOk_WhenSignatureAndMfaAreValid()
    {
        // Arrange
        var clientId = "App01";
        var requestDTO = new CredentialRequest(clientId, "DbSecret", "ValidHash", "123456");
        var contextMock = new Mock<FunctionContext>();
        var httpRequestMock = CreateMockHttpRequestData(requestDTO, contextMock);

        _securityMock.Setup(s => s.IsValidSignature(It.IsAny<CredentialRequest>(), It.IsAny<string>()))
                    .Returns(true);

        _securityMock.Setup(s => s.IsValidMfa(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(true);

        _auditMock.Setup(a => a.LogAccessAsync(
            It.IsAny<CredentialRequest>(), 
            It.IsAny<bool>(), 
            It.IsAny<string>(), 
            It.IsAny<string>()))
        .ReturnsAsync(Guid.NewGuid());

        _vaultMock.Setup(v => v.GetSecretAsync(It.Is<string>(s => s.Contains("MFA-SEED"))))
                .ReturnsAsync("JBSWY3DPEHPK3PXP");

        _vaultMock.Setup(v => v.GetSecretAsync(It.Is<string>(s => s == "DbSecret")))
                .ReturnsAsync("FinalSecretValue");

        // Act
        var response = await _function.Run(httpRequestMock);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        _auditMock.Setup(a => a.LogAccessAsync(It.IsAny<CredentialRequest>(), true, It.IsAny<string>(), It.IsAny<string>()))
          .ReturnsAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task Run_ShouldReturnUnauthorized_WhenMfaIsInvalid()
    {
        // Arrange
        var requestDTO = new CredentialRequest("App01", "DbSecret", "ValidHash", "000000");
        var contextMock = new Mock<FunctionContext>();
        var httpRequestMock = CreateMockHttpRequestData(requestDTO, contextMock);

        _securityMock.Setup(s => s.IsValidSignature(It.IsAny<CredentialRequest>(), It.IsAny<string>()))
                    .Returns(true);

        _vaultMock.Setup(v => v.GetSecretAsync(It.IsAny<string>()))
                .ReturnsAsync("MFA_SEED_HERE");

        _securityMock.Setup(s => s.IsValidMfa(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(false);

        // Act
        var response = await _function.Run(httpRequestMock);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpRequestData CreateMockHttpRequestData(CredentialRequest body, Mock<FunctionContext> contextMock)
    {
        var services = new ServiceCollection();
        var serializer = new Azure.Core.Serialization.JsonObjectSerializer();
        
        services.Configure<WorkerOptions>(options => options.Serializer = serializer);
        var serviceProvider = services.BuildServiceProvider();
        contextMock.Setup(c => c.InstanceServices).Returns(serviceProvider);

        var request = new Mock<HttpRequestData>(contextMock.Object);
        
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var bodyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        request.Setup(r => r.Body).Returns(bodyStream);
        request.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        
        var response = new Mock<HttpResponseData>(contextMock.Object);
        response.SetupAllProperties();
        response.Setup(r => r.Body).Returns(new MemoryStream());
        response.Setup(r => r.Headers).Returns(new HttpHeadersCollection());

        request.Setup(r => r.CreateResponse()).Returns(response.Object);

        return request.Object;
    }
}