namespace RestaurantDelivery.Core.Interfaces;

// The data sets worth caching: read constantly by every visitor, written rarely by one
// admin. Grouped rather than cached per key so a single write can drop everything it
// could possibly have affected - see IReadThroughCache.Invalidate.
public enum CacheGroup
{
    // The menu itself. Also invalidated by category writes, because renaming a category
    // rewrites MenuItem.Category on every item under it.
    Menu,

    // The category list.
    Categories,

    // Site settings: branding, tax, delivery fee. One row, read on nearly every request.
    Settings
}

// A small read-through cache over the handful of hot, rarely-changing reads.
//
// In-process (IMemoryCache), which is exact at the current single replica but worth
// knowing about if that ever changes: each replica would hold its own copy, so a write
// evicts only the replica that served it and the others stay stale until their entry
// expires. The TTL is the backstop for that, which is why entries expire on a timer even
// though every write path also invalidates explicitly.
public interface IReadThroughCache
{
    // Returns the cached value, or runs the factory and caches its result. Concurrent
    // callers for the same key wait on one factory run rather than all hitting the
    // database - the stampede that would otherwise happen the moment a cached menu
    // expires mid-service.
    Task<T> GetOrCreateAsync<T>(CacheGroup group, string key, Func<Task<T>> factory);

    // Drops every entry in the group. Called from the write paths, not left to the TTL:
    // an admin who edits a price expects to see it immediately, not in a minute.
    void Invalidate(CacheGroup group);
}
