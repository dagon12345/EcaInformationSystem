using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EcaInformationSystem.Application.Services
{
    public class ShrinkPreviewCache : IShrinkPreviewCache
    {
        private readonly IMemoryCache _cache;

        // Abandoned previews (user closed the modal, picked a different file,
        // etc.) age out on their own rather than sitting in memory forever.
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        public ShrinkPreviewCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Guid Store(ShrinkPreviewEntry entry)
        {
            var token = Guid.NewGuid();
            _cache.Set(CacheKey(token), entry, Ttl);
            return token;
        }

        public ShrinkPreviewEntry? Take(Guid token)
        {
            var key = CacheKey(token);
            if (_cache.TryGetValue(key, out ShrinkPreviewEntry? entry))
            {
                _cache.Remove(key);
                return entry;
            }
            return null;
        }

        private static string CacheKey(Guid token) => $"shrink-preview:{token}";
    }
}
