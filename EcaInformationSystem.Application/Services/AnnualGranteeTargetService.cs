using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class AnnualGranteeTargetService : IAnnualGranteeTargetService
    {
        private static readonly string[] QuarterLabels = { "Q1", "Q2", "Q3", "Q4" };

        private readonly IAnnualGranteeTargetRepository _repo;
        private readonly IRegionRepository _regionRepo;
        private readonly ILogRepository _logRepo;

        public AnnualGranteeTargetService(IAnnualGranteeTargetRepository repo, IRegionRepository regionRepo, ILogRepository logRepo)
        {
            _repo = repo;
            _regionRepo = regionRepo;
            _logRepo = logRepo;
        }

        public async Task<AnnualTargetComparisonDto> GetComparisonAsync(int regionCode, int fiscalYear)
        {
            var target = await _repo.GetAsync(regionCode, fiscalYear);
            var paidCounts = await _repo.GetQuarterlyPaidCountsAsync(regionCode, fiscalYear);
            var regionName = await ResolveRegionNameAsync(regionCode);

            var quarterlyTargets = target?.ToQuarterlyArray() ?? new int[4];

            var quarterly = new List<QuarterTargetVsActualDto>(4);
            for (int i = 0; i < 4; i++)
            {
                var quarter = i + 1;
                quarterly.Add(new QuarterTargetVsActualDto
                {
                    Quarter = quarter,
                    QuarterLabel = QuarterLabels[i],
                    Target = quarterlyTargets[i],
                    PaidCount = paidCounts.TryGetValue(quarter, out var count) ? count : 0
                });
            }

            return new AnnualTargetComparisonDto
            {
                RegionCode = regionCode,
                RegionName = regionName,
                FiscalYear = fiscalYear,
                HasTarget = target is not null,
                AnnualTarget = target?.AnnualTotal ?? 0,
                TotalPaidYtd = quarterly.Sum(q => q.PaidCount),
                Quarterly = quarterly,
                DateSet = target?.DateSet,
                SetBy = target?.SetBy,
                DateModified = target?.DateModified,
                ModifiedBy = target?.ModifiedBy
            };
        }

        public async Task<AnnualGranteeTargetDto> UpsertAsync(int regionCode, int fiscalYear, int[] quarterlyTargets, string userName)
        {
            var existedBefore = await _repo.GetAsync(regionCode, fiscalYear) is not null;
            var saved = await _repo.UpsertAsync(regionCode, fiscalYear, quarterlyTargets, userName);
            var regionName = await ResolveRegionNameAsync(regionCode);

            await _logRepo.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "AnnualTarget",
                UserName = userName,
                CreatedAt = DateTime.UtcNow,
                Activity = $"{(existedBefore ? "Updated" : "Set")} annual grantee target for {regionName ?? $"Region {regionCode}"} — FY {fiscalYear} (Annual Total: {saved.AnnualTotal:N0})"
            });
            await _logRepo.SaveChangesAsync();

            return new AnnualGranteeTargetDto
            {
                Id = saved.Id,
                RegionCode = saved.RegionCode,
                RegionName = regionName,
                FiscalYear = saved.FiscalYear,
                QuarterlyTargets = saved.ToQuarterlyArray(),
                AnnualTotal = saved.AnnualTotal,
                DateSet = saved.DateSet,
                SetBy = saved.SetBy,
                DateModified = saved.DateModified,
                ModifiedBy = saved.ModifiedBy
            };
        }

        private async Task<string?> ResolveRegionNameAsync(int regionCode)
        {
            var regions = await _regionRepo.GetAllAsync();
            return regions.FirstOrDefault(r => r.PsgcCodeRegion == regionCode)?.Name;
        }
    }
}
