using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class WfpEcaService : IWfpEcaService
    {
        private readonly IWfpEcaRepository _repo;
        private readonly IRegionRepository _regionRepo;
        private readonly ILogRepository _logRepo;

        public WfpEcaService(IWfpEcaRepository repo, IRegionRepository regionRepo, ILogRepository logRepo)
        {
            _repo = repo;
            _regionRepo = regionRepo;
            _logRepo = logRepo;
        }

        public async Task<WfpEcaDto> GetAsync(int regionCode, int fiscalYear)
        {
            var entries = await _repo.GetAsync(regionCode, fiscalYear);
            var regionName = await ResolveRegionNameAsync(regionCode);

            var lines = entries.Select(e => new WfpEcaLineDto
            {
                Id = e.Id,
                UacsCode = e.UacsCode,
                UacsName = e.UacsName,
                Allotment = e.Allotment,
                Obligation = e.Obligation,
                Balance = e.Balance,
                Remarks = e.Remarks
            }).ToList();

            var totalAllotment = lines.Sum(l => l.Allotment);
            var totalObligation = lines.Sum(l => l.Obligation);

            var lastSet = entries.OrderByDescending(e => e.DateSet).FirstOrDefault();
            var lastModified = entries.Where(e => e.DateModified.HasValue)
                .OrderByDescending(e => e.DateModified).FirstOrDefault();

            return new WfpEcaDto
            {
                RegionCode = regionCode,
                RegionName = regionName,
                FiscalYear = fiscalYear,
                Lines = lines,
                TotalAllotment = totalAllotment,
                TotalObligation = totalObligation,
                TotalBalance = totalAllotment - totalObligation,
                ObligatedPercentage = totalAllotment == 0 ? 0 : Math.Round(totalObligation / totalAllotment * 100, 2),
                DateSet = lastSet?.DateSet,
                SetBy = lastSet?.SetBy,
                DateModified = lastModified?.DateModified,
                ModifiedBy = lastModified?.ModifiedBy
            };
        }

        public async Task<WfpEcaDto> UpsertAsync(int regionCode, int fiscalYear, List<UpsertWfpEcaLineDto> lines, string userName)
        {
            var toSave = lines.Select(l => new WfpEcaLineInput(l.Id, l.UacsCode.Trim(), l.UacsName.Trim(), l.Allotment, l.Obligation, l.Remarks));

            var saveResult = await _repo.UpsertAsync(regionCode, fiscalYear, toSave, userName);
            var regionName = await ResolveRegionNameAsync(regionCode);
            var regionLabel = regionName ?? $"Region {regionCode}";

            // One log entry per line actually added/edited/removed, rather than
            // one generic "table updated" entry, so the activity log shows who
            // did what to which specific UACS line item. A save triggered only
            // by reordering rows produces no diff and so logs nothing.
            var now = DateTime.UtcNow;

            foreach (var entry in saveResult.Added)
            {
                await _logRepo.AddAsync(new Log
                {
                    Id = Guid.NewGuid(),
                    Category = "WfpEca",
                    UserName = userName,
                    CreatedAt = now,
                    Activity = $"Added UACS line item '{entry.UacsName}' ({entry.UacsCode}) to the Work Financial Plan (ECA) for {regionLabel} — FY {fiscalYear}"
                });
            }

            foreach (var entry in saveResult.Updated)
            {
                await _logRepo.AddAsync(new Log
                {
                    Id = Guid.NewGuid(),
                    Category = "WfpEca",
                    UserName = userName,
                    CreatedAt = now,
                    Activity = $"Edited UACS line item '{entry.UacsName}' ({entry.UacsCode}) in the Work Financial Plan (ECA) for {regionLabel} — FY {fiscalYear}"
                });
            }

            foreach (var entry in saveResult.Removed)
            {
                await _logRepo.AddAsync(new Log
                {
                    Id = Guid.NewGuid(),
                    Category = "WfpEca",
                    UserName = userName,
                    CreatedAt = now,
                    Activity = $"Deleted UACS line item '{entry.UacsName}' ({entry.UacsCode}) from the Work Financial Plan (ECA) for {regionLabel} — FY {fiscalYear}"
                });
            }

            if (saveResult.Added.Count > 0 || saveResult.Updated.Count > 0 || saveResult.Removed.Count > 0)
                await _logRepo.SaveChangesAsync();

            return await GetAsync(regionCode, fiscalYear);
        }

        private async Task<string?> ResolveRegionNameAsync(int regionCode)
        {
            var regions = await _regionRepo.GetAllAsync();
            return regions.FirstOrDefault(r => r.PsgcCodeRegion == regionCode)?.Name;
        }
    }
}
