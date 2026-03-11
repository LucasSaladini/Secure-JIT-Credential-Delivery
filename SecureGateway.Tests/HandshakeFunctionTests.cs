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

        _securityMock.Setup(s => s.IsValidSignature(It.IsAny<CredentialRequest>(), It.IsAny<string>()))
                     .Returns(false);

        // Act
        var response = await _function.Run(httpRequestMock);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        
        _auditMock.Verify(a => a.LogAccessAsync(
            It.IsAny<CredentialRequest>(), 
            false, 
            "Invalid Signature", 
            It.IsAny<string>()), Times.Once);
    }

    private HttpRequestData CreateMockHttpRequestData(CredentialRequest body, Mock<FunctionContext> contextMock)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddFunctionsWorkerDefaults();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        contextMock.Setup(c => c.InstanceServices).Returns(serviceProvider);

        var request = new Mock<HttpRequestData>(contextMock.Object);
        
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var byteArray = System.Text.Encoding.UTF8.GetBytes(json);
        var bodyStream = new MemoryStream(byteArray);

        request.Setup(r => r.Body).Returns(bodyStream);
        request.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        
        var response = new Mock<HttpResponseData>(contextMock.Object);
        response.SetupAllProperties();
        response.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        request.Setup(r => r.CreateResponse()).Returns(response.Object);

        return request.Object;
    }
}