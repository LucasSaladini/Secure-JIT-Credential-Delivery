using Moq;
using Polly;
using Polly.Registry;
using SecureGateway.Server.Services;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;
using Azure;
using Polly.CircuitBreaker;

namespace SecureGateway.Tests;

public class VaultServiceResilienceTests
{
    private readonly Mock<ResiliencePipelineProvider<string>> _pipelineProviderMock;
    private readonly Mock<ILogger<VaultService>> _loggerMock;
    private readonly Meter _testMeter;

    public VaultServiceResilienceTests()
    {
        _pipelineProviderMock = new Mock<ResiliencePipelineProvider<string>>();
        _loggerMock = new Mock<ILogger<VaultService>>();
        _testMeter = new Meter("TestMeter");
    }

    [Fact]
    public async Task GetSecretAsync_ShouldExecuteThroughPipeline_AndReturnSecret()
    {
        // Arrange
        var secretName = "DbPassword";
        var expectedValue = "p@ssword123";
        
        var keyVaultMock = new Mock<IKeyVaultClient>();
        var secret = new KeyVaultSecret(secretName, expectedValue);
        var response = Response.FromValue(secret, Mock.Of<Response>());
        
        keyVaultMock
            .Setup(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _pipelineProviderMock
            .Setup(x => x.GetPipeline("vault-strategy"))
            .Returns(ResiliencePipeline.Empty);

        var service = new VaultService(
            keyVaultMock.Object, 
            _loggerMock.Object, 
            _testMeter, 
            _pipelineProviderMock.Object);

        // Act
        var result = await service.GetSecretAsync(secretName);

        // Assert
        Assert.Equal(expectedValue, result);
        keyVaultMock.Verify(x => x.GetSecretAsync(secretName, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldRetry_WhenRequestFails()
    {
        // Arrange
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new() { 
                MaxRetryAttempts = 2, 
                Delay = TimeSpan.Zero
            })
            .Build();

        _pipelineProviderMock
            .Setup(x => x.GetPipeline("vault-strategy"))
            .Returns(pipeline);

        var keyVaultMock = new Mock<IKeyVaultClient>();
        keyVaultMock
            .SetupSequence(x => x.GetSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Fail 1"))
            .ThrowsAsync(new RequestFailedException("Fail 2"))
            .ReturnsAsync(Response.FromValue(new KeyVaultSecret("any", "success"), Mock.Of<Response>()));

        var service = new VaultService(keyVaultMock.Object, _loggerMock.Object, _testMeter, _pipelineProviderMock.Object);

        // Act
        var result = await service.GetSecretAsync("any");

        // Assert
        Assert.Equal("success", result);
        keyVaultMock.Verify(x => x.GetSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetSecretAsync_ShouldBreakCircuit_AfterThresholdReached()
    {
        // Arrange
        var pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<RequestFailedException>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();

        _pipelineProviderMock
            .Setup(x => x.GetPipeline("vault-strategy"))
            .Returns(pipeline);

        var keyVaultMock = new Mock<IKeyVaultClient>();
        keyVaultMock
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Azure Outage"));

        var service = new VaultService(
            keyVaultMock.Object, 
            _loggerMock.Object, 
            _testMeter, 
            _pipelineProviderMock.Object);

        // Act & Assert
        
        await Assert.ThrowsAsync<RequestFailedException>(() => service.GetSecretAsync("db-pwd"));
        await Assert.ThrowsAsync<RequestFailedException>(() => service.GetSecretAsync("db-pwd"));

        await Assert.ThrowsAsync<BrokenCircuitException>(() => service.GetSecretAsync("db-pwd"));

        // Assert Final
        keyVaultMock.Verify(x => x.GetSecretAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}