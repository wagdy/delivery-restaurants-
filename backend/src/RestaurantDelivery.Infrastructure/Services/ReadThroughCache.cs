using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class ReadThroughCache : IReadThroughCache
{
    // 60 seconds. Short enough that a stale read is never interesting (and bounds
    // staleness if this ever runs on more than one replica, where a write only evicts the
    // replica that handled it), long enough to absorb the repeated menu/settings reads a
    // single page load produces.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    // One token source per group. Entries in a group register its token as an expiration
    // token, so cancelling the source evicts all of them at once - IMemoryCache has no
    // "remove by prefix", and tracking every key that was ever written would be both
    // fiddly and easy to get subtly wrong.
    private readonly Dictionary<CacheGroup, CancellationTokenSource> _groupTokens;

    // Guards both the token dictionary and the per-key factory locks below. Held only
    // while swapping references, never while awaiting a factory.
    private readonly Lock _gate = new();

    // One semaphore per cache key, so concurrent misses for the same key run the factory
    // once instead of all querying the database together.
    //
    // Never trimmed, so callers must only cache keys drawn from a bounded set - today
    // that is category name x availability x has-addons, a few dozen combinations at
    // most. MenuItemService deliberately bypasses the cache for search queries for this
    // reason: free text would let a caller grow this dictionary without limit.
    private readonly Dictionary<string, SemaphoreSlim> _keyLocks = new();

    public ReadThroughCache(IMemoryCache cache)
    {
        _cache = cache;
        _groupTokens = Enum.GetValues<CacheGroup>().ToDictionary(group => group, _ => new CancellationTokenSource());
    }

    public async Task<T> GetOrCreateAsync<T>(CacheGroup group, string key, Func<Task<T>> factory)
    {
        var cacheKey = $"{group}:{key}";

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var keyLock = GetKeyLock(cacheKey);
        await keyLock.WaitAsync();
        try
        {
            // Re-checked inside the lock: whoever held it before us has just populated
            // this key, and running the factory again would defeat the point.
            if (_cache.TryGetValue(cacheKey, out T? cachedAfterWait) && cachedAfterWait is not null)
            {
                return cachedAfterWait;
            }

            // Captured BEFORE the factory runs, which is the whole point. If an admin
            // saves a price while this read is still querying, Invalidate replaces the
            // group's token and cancels this one - so the entry is created against an
            // already-cancelled token and expires immediately, instead of caching a value
            // that was read before the write and serving it for the next minute.
            //
            // Capturing it after the factory would pick up the post-invalidation token and
            // do exactly that: quietly cache the stale read. Losing a cache entry costs one
            // extra query; keeping a stale one shows the customer the old price.
            var token = CurrentTokenFor(group);

            var value = await factory();

            var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl };
            options.AddExpirationToken(new CancellationChangeToken(token));

            _cache.Set(cacheKey, value, options);
            return value;
        }
        finally
        {
            keyLock.Release();
        }
    }

    public void Invalidate(CacheGroup group)
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            previous = _groupTokens[group];
            _groupTokens[group] = new CancellationTokenSource();
        }

        // Cancelled after the replacement is in place, so a read racing this call binds
        // to the new token rather than one that is about to fire.
        previous.Cancel();
        previous.Dispose();
    }

    private CancellationToken CurrentTokenFor(CacheGroup group)
    {
        lock (_gate)
        {
            return _groupTokens[group].Token;
        }
    }

    private SemaphoreSlim GetKeyLock(string cacheKey)
    {
        lock (_gate)
        {
            if (!_keyLocks.TryGetValue(cacheKey, out var keyLock))
            {
                keyLock = new SemaphoreSlim(1, 1);
                _keyLocks[cacheKey] = keyLock;
            }

            return keyLock;
        }
    }
}
