using jobAgentApi.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.Cache;

public sealed class JobCacheService : IJobCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<JobCacheService> _logger;
    private readonly JobCacheOptions _options;

    public JobCacheService(
        IMemoryCache cache,
        IOptions<JobCacheOptions> options,
        ILogger<JobCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public Task<CachedJobSearchResult?> GetCachedResultAsync(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(normalizedQuery);
        
        if (_cache.TryGetValue(cacheKey, out CachedJobSearchResult? cachedResult))
        {
            _logger.LogDebug("Cache hit for query {Query}", normalizedQuery);
            return Task.FromResult(cachedResult);
        }

        _logger.LogDebug("Cache miss for query {Query}", normalizedQuery);
        return Task.FromResult<CachedJobSearchResult?>(null);
    }

    public Task SetCachedResultAsync(string normalizedQuery, CachedJobSearchResult result, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(normalizedQuery);
        
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.TtlMinutes))
            .SetSlidingExpiration(TimeSpan.FromMinutes(_options.TtlMinutes / 2))
            .SetSize(1);

        _cache.Set(cacheKey, result, cacheEntryOptions);
        
        _logger.LogInformation(
            "Cached {JobCount} jobs for query {Query} with TTL {Ttl}min",
            result.Jobs.Count,
            normalizedQuery,
            _options.TtlMinutes);

        return Task.CompletedTask;
    }

    public Task RemoveCachedResultAsync(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(normalizedQuery);
        _cache.Remove(cacheKey);
        
        _logger.LogDebug("Removed cache for query {Query}", normalizedQuery);
        return Task.CompletedTask;
    }

    private static string BuildCacheKey(string normalizedQuery) => $"job_search:{normalizedQuery}";
}
