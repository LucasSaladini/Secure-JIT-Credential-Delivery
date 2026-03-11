using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SecureGateway.Server.Services;

public class CachedVaultService(IVaultService inner, IMemoryCache cache, ILogger<CachedVaultService> logger) : IVaultService
{
    public async Task<string> GetSecretAsync(string resourceKey)
    {
        if (cache.TryGetValue(resourceKey, out string? cachedSecret) && cachedSecret != null)
        {
            logger.LogInformation("Cache hit for resource: {ResourceKey}", resourceKey);
            return cachedSecret;
        }

        logger.LogInformation("Cache miss for resource: {ResourceKey}. Fetching from Key Vault", resourceKey);

        var secret = await inner.GetSecretAsync(resourceKey);
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .SetSize(1);

        cache.Set(resourceKey, secret, cacheOptions);

        return secret;
    }
}