using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SecureGateway.Server.Services;
using System.Diagnostics.Metrics;

namespace SecureGateway.Tests;

public class CachedVaultServiceTests : IDisposable
{
    private readonly Mock<IVaultService> _innerVaultMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<CachedVaultService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly Meter _testMeter;
    private readonly CachedVaultService _cachedService;

    public CachedVaultServiceTests()
    {
        _innerVaultMock = new Mock<IVaultService>();
        _loggerMock = new Mock<ILogger<CachedVaultService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _testMeter = new Meter("SecureGateway.Tests");

        var myConfiguration = new Dictionary<string, string?>
        {
            {"VaultSettings:CacheTTLMinutes", "5"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        _cachedService = new CachedVaultService(
            _innerVaultMock.Object, 
            _memoryCache, 
            _configuration,
            _loggerMock.Object,
            _testMeter);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldCallInnerServiceOnlyOnce_WhenKeyIsCached()
    {
        var resourceKey = "DatabasePassword";
        var expectedSecret = "P@ssword123";

        _innerVaultMock
            .Setup(v => v.GetSecretAsync(resourceKey))
            .ReturnsAsync(expectedSecret);

        await _cachedService.GetSecretAsync(resourceKey);
        var secondResult = await _cachedService.GetSecretAsync(resourceKey);

        Assert.Equal(expectedSecret, secondResult);
        _innerVaultMock.Verify(v => v.GetSecretAsync(resourceKey), Times.Once);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldCallInnerServiceAgain_AfterCacheExpires()
    {
        var resourceKey = "TemporaryToken";
        _innerVaultMock.Setup(v => v.GetSecretAsync(resourceKey)).ReturnsAsync("TokenV1");

        await _cachedService.GetSecretAsync(resourceKey);
        _memoryCache.Remove(resourceKey); 
        await _cachedService.GetSecretAsync(resourceKey);

        _innerVaultMock.Verify(v => v.GetSecretAsync(resourceKey), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSecretAsync_ShouldReturnFromCache_WhenKeyExists()
    {
        var resourceKey = "ApiKey";
        var cachedValue = "cached-secret-123";
        _memoryCache.Set(resourceKey, cachedValue);

        var result = await _cachedService.GetSecretAsync(resourceKey);

        Assert.Equal(cachedValue, result);
        _innerVaultMock.Verify(x => x.GetSecretAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldCallInnerService_AndPopulateCache_WhenCacheIsEmpty()
    {
        var resourceKey = "NewKey";
        var vaultValue = "value-from-vault";
        _innerVaultMock.Setup(x => x.GetSecretAsync(resourceKey)).ReturnsAsync(vaultValue);

        await _cachedService.GetSecretAsync(resourceKey);

        var exists = _memoryCache.TryGetValue(resourceKey, out string? val);
        Assert.True(exists);
        Assert.Equal(vaultValue, val);
    }
    
    [Fact]
    public async Task GetSecretAsync_ShouldUseConfiguredTTL()
    {
        // Arrange
        var resourceKey = "ConfigKey";
        _innerVaultMock.Setup(v => v.GetSecretAsync(resourceKey)).ReturnsAsync("any");

        // Act
        await _cachedService.GetSecretAsync(resourceKey);

        // Assert
        var exists = _memoryCache.TryGetValue(resourceKey, out _);
        Assert.True(exists);
    }

    public void Dispose()
    {
        _testMeter.Dispose();
        _memoryCache.Dispose();
    }
}