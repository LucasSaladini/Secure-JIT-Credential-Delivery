using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace SecureGateway.Server.Services;

public class CachedVaultService : IVaultService
{
    private readonly IVaultService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedVaultService> _logger;
    private readonly TimeSpan _cacheDuration;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;

    public CachedVaultService(
        IVaultService inner, 
        IMemoryCache cache, 
        IConfiguration config,
        ILogger<CachedVaultService> logger, 
        Meter meter)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        
        var ttlMinutes = config.GetValue<int>("VaultSettings:CacheTTLMinutes", 5);
        _cacheDuration = TimeSpan.FromMinutes(ttlMinutes);

        _cacheHitCounter = meter.CreateCounter<long>(
            "vault_cache_hits_total", 
            description: "Total number of successful cache lookups");
            
        _cacheMissCounter = meter.CreateCounter<long>(
            "vault_cache_misses_total", 
            description: "Total number of cache lookups that required a Key Vault call");
    }

    public async Task<string> GetSecretAsync(string resourceKey)
    {
        if (_cache.TryGetValue(resourceKey, out string? cachedSecret) && cachedSecret != null)
        {
            _cacheHitCounter.Add(1, new TagList { { "resource", resourceKey } });
            _logger.LogInformation("VaultCache: Hit | Resource: {ResourceKey}", resourceKey);
            return cachedSecret;
        }

        // Cache Miss
        _cacheMissCounter.Add(1, new TagList { { "resource", resourceKey } });
        _logger.LogInformation("VaultCache: Miss | Resource: {ResourceKey}. Fetching from provider.", resourceKey);

        var secret = await _inner.GetSecretAsync(resourceKey);
        
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheDuration)
            .SetSize(1);

        _cache.Set(resourceKey, secret, cacheOptions);

        return secret;
    }
}