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

        // Not cached — an on-demand audit drill-down, not part of the main
        // report render path, so freshness matters more than round-trip cost.
        public async Task<StatisticsMembersPagedResultDto> GetStatisticsMembersAsync(StatisticsMembersRequestDto request)
            => await _repo.GetStatisticsMembersAsync(request.Filter, request.Bucket, request.PageNumber, request.PageSize);

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
                // ✅ CHANGED — order-independent so [1,2] and [2,1] hit the same
                // cache entry instead of silently missing each other.
                request.PaymentStatuses != null && request.PaymentStatuses.Any()
                    ? string.Join(",", request.PaymentStatuses.OrderBy(s => s))
                    : "null",
                request.PayrollQuarter?.ToString() ?? "null",
                request.FiscalYear?.ToString() ?? "null", // ✅ new
                request.DateEndorsedFrom?.ToString("yyyyMMdd") ?? "null",
                request.DateEndorsedTo?.ToString("yyyyMMdd") ?? "null",
                request.DateAddedFrom?.ToString("yyyyMMdd") ?? "null",
                request.DateAddedTo?.ToString("yyyyMMdd") ?? "null",
                request.IsLivenessVerified?.ToString() ?? "null",
                request.IsReadyForEft?.ToString() ?? "null",
                request.CoDateEndorsedFrom?.ToString("yyyyMMdd") ?? "null",
                request.CoDateEndorsedTo?.ToString("yyyyMMdd") ?? "null",
                request.CoDateApprovedFrom?.ToString("yyyyMMdd") ?? "null",
                request.CoDateApprovedTo?.ToString("yyyyMMdd") ?? "null");
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