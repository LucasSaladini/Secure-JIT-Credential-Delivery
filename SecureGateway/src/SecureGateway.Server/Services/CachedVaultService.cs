using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace SecureGateway.Server.Services;

public class CachedVaultService : IVaultService
{
    private readonly IVaultService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedVaultService> _logger;    
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;

    public CachedVaultService(
        IVaultService inner, 
        IMemoryCache cache, 
        ILogger<CachedVaultService> logger, 
        Meter meter)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;

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
            // Incrementar métrica de Hit
            _cacheHitCounter.Add(1, new TagList { { "resource", resourceKey } });
            
            _logger.LogInformation("VaultCache: {Result} for {ResourceKey}", "Hit", resourceKey);
            return cachedSecret;
        }

        // Incrementar métrica de Miss
        _cacheMissCounter.Add(1, new TagList { { "resource", resourceKey } });

        _logger.LogInformation("VaultCache: {Result} for {ResourceKey}. Fetching from provider.", "Miss", resourceKey);

        var secret = await _inner.GetSecretAsync(resourceKey);
        
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .SetSize(1);

        _cache.Set(resourceKey, secret, cacheOptions);

        return secret;
    }
}