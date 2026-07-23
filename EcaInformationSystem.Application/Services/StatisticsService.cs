using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EcaInformationSystem.Application.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly IBeneficiaryInformationRepository _repo;
        private readonly IMemoryCache _memoryCache;
        private const string StatisticsCacheVersionKey = "statistics_cache_version_v1";
        private const string StatisticsCachePrefix = "statistics_report_";

        public StatisticsService(
            IBeneficiaryInformationRepository repo,
            IMemoryCache memoryCache)
        {
            _repo = repo;
            _memoryCache = memoryCache;
        }

        public async Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request)
        {
            var cacheKey = BuildCacheKey(request);

            if (_memoryCache.TryGetValue(cacheKey, out StatisticsReportDto? cached) && cached is not null)
                return cached;

            var report = await _repo.GetStatisticsReportAsync(request);

            _memoryCache.Set(cacheKey, report, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

            return report;
        }

        public Task InvalidateStatisticsCacheAsync()
        {
            // Bump the version token to invalidate all statistics cache entries
            var newVersion = Guid.NewGuid().ToString();
            _memoryCache.Set(StatisticsCacheVersionKey, newVersion);
            return Task.CompletedTask;
        }

        private string BuildCacheKey(StatisticsRequestDto request)
        {
            var version = GetCurrentCacheVersion();

            return string.Join("|",
                StatisticsCachePrefix,
                version,
                request.Region?.ToString() ?? "null",
                request.Province?.ToString() ?? "null",
                request.Municipality?.ToString() ?? "null",
                request.MilestoneYear.ToString(),
                request.MilestoneAge.ToString(),
                request.PaymentStatus.ToString(),
                request.PayrollQuarter?.ToString() ?? "null",
                request.FiscalYear?.ToString() ?? "null", // ✅ new
                request.DateEndorsedFrom?.ToString("yyyyMMdd") ?? "null",
                request.DateEndorsedTo?.ToString("yyyyMMdd") ?? "null");
        }
        private string GetCurrentCacheVersion()
        {
            return _memoryCache.GetOrCreate(StatisticsCacheVersionKey, entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return "v1";
            })!;
        }
    }
}