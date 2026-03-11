using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SecureGateway.Server.Services;

namespace SecureGateway.Tests;

public class CachedVaultServiceTests
{
    private readonly Mock<IVaultService> _innerVaultMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<CachedVaultService>> _loggerMock;
    private readonly CachedVaultService _cachedService;

    public CachedVaultServiceTests()
    {
        _innerVaultMock = new Mock<IVaultService>();
        _loggerMock = new Mock<ILogger<CachedVaultService>>();
        
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _cachedService = new CachedVaultService(
            _innerVaultMock.Object, 
            _memoryCache, 
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldCallInnerServiceOnlyOnce_WhenKeyIsCached()
    {
        // Arrange
        var resourceKey = "DatabasePassword";
        var expectedSecret = "P@ssword123";

        _innerVaultMock
            .Setup(v => v.GetSecretAsync(resourceKey))
            .ReturnsAsync(expectedSecret);

        // Act
        var firstResult = await _cachedService.GetSecretAsync(resourceKey);
        
        var secondResult = await _cachedService.GetSecretAsync(resourceKey);

        // Assert
        Assert.Equal(expectedSecret, firstResult);
        Assert.Equal(expectedSecret, secondResult);

        _innerVaultMock.Verify(v => v.GetSecretAsync(resourceKey), Times.Once);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldCallInnerServiceAgain_AfterCacheExpires()
    {
        // Arrange
        var resourceKey = "TemporaryToken";
        _innerVaultMock.Setup(v => v.GetSecretAsync(resourceKey)).ReturnsAsync("TokenV1");

        // Act
        await _cachedService.GetSecretAsync(resourceKey);
        
        _memoryCache.Remove(resourceKey);
        
        await _cachedService.GetSecretAsync(resourceKey);

        // Assert
        _innerVaultMock.Verify(v => v.GetSecretAsync(resourceKey), Times.Exactly(2));
    }
}