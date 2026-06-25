using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EcaInformationSystem.Infrastructure.Caching
{
    public class PsgcNameCache : IPsgcNameCache
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PsgcNameCache> _logger;

        private ConcurrentDictionary<int, string> _regions = new();
        private ConcurrentDictionary<int, string> _provinces = new();
        private ConcurrentDictionary<int, string> _municipalities = new();
        private ConcurrentDictionary<int, string> _barangays = new();

        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public PsgcNameCache(IServiceScopeFactory scopeFactory, ILogger<PsgcNameCache> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public string? GetRegionName(int code) => _regions.TryGetValue(code, out var v) ? v : null;
        public string? GetProvinceName(int code) => _provinces.TryGetValue(code, out var v) ? v : null;
        public string? GetMunicipalityName(int code) => _municipalities.TryGetValue(code, out var v) ? v : null;
        public string? GetBarangayName(int code) => _barangays.TryGetValue(code, out var v) ? v : null;

        public List<int> GetProvinceCodesByNameContains(string term) =>
            _provinces.Where(kv => kv.Value.ToLower().Contains(term)).Select(kv => kv.Key).ToList();

        public List<int> GetMunicipalityCodesByNameContains(string term) =>
            _municipalities.Where(kv => kv.Value.ToLower().Contains(term)).Select(kv => kv.Key).ToList();

        public List<int> GetBarangayCodesByNameContains(string term) =>
            _barangays.Where(kv => kv.Value.ToLower().Contains(term)).Select(kv => kv.Key).ToList();

        public List<int> GetRegionCodesByNameContains(string term) =>
            _regions.Where(kv => kv.Value.ToLower().Contains(term)).Select(kv => kv.Key).ToList();

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await _refreshLock.WaitAsync(ct);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var regions = await db.Regions.AsNoTracking()
                    .Select(r => new { r.PsgcCodeRegion, r.Name }).ToListAsync(ct);
                var provinces = await db.Provinces.AsNoTracking()
                    .Select(p => new { p.PsgcCodeProvince, p.Name }).ToListAsync(ct);
                var municipalities = await db.Municipalities.AsNoTracking()
                    .Select(m => new { m.PsgcCodeMunicipality, m.Name }).ToListAsync(ct);
                var barangays = await db.Barangays.AsNoTracking()
                    .Select(b => new { b.PsgcCodeBarangay, b.Name }).ToListAsync(ct);

                _regions = new ConcurrentDictionary<int, string>(
                    regions.Where(r => r.Name != null)
                           .Select(r => new KeyValuePair<int, string>(r.PsgcCodeRegion, r.Name!)));

                _provinces = new ConcurrentDictionary<int, string>(
                    provinces.Where(p => p.Name != null)
                             .Select(p => new KeyValuePair<int, string>(p.PsgcCodeProvince, p.Name!)));

                _municipalities = new ConcurrentDictionary<int, string>(
                    municipalities.Where(m => m.Name != null)
                                  .Select(m => new KeyValuePair<int, string>(m.PsgcCodeMunicipality, m.Name!)));

                _barangays = new ConcurrentDictionary<int, string>(
                    barangays.Where(b => b.Name != null)
                             .Select(b => new KeyValuePair<int, string>(b.PsgcCodeBarangay, b.Name!)));

                _logger.LogInformation(
                    "PSGC name cache refreshed: {Regions} regions, {Provinces} provinces, {Munis} municipalities, {Brgys} barangays",
                    _regions.Count, _provinces.Count, _municipalities.Count, _barangays.Count);
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }
}