using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Exceptions;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class SeniorCitizenDirectoryRepository : ISeniorCitizenDirectoryRepository
    {
        private readonly AppDbContext _context;
        private readonly IPsgcNameCache _psgcNameCache;

        public SeniorCitizenDirectoryRepository(AppDbContext context, IPsgcNameCache psgcNameCache)
        {
            _context = context;
            _psgcNameCache = psgcNameCache;
        }

        public async Task<List<SeniorCitizenDirectoryListItemDto>> GetAllAsync(int regionCode)
        {
            var entries = await _context.SeniorCitizenDirectoryEntries
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.PsgcCodeRegion == regionCode)
                .ToListAsync();

            return entries
                .Select(x => new SeniorCitizenDirectoryListItemDto
                {
                    Id = x.Id,
                    PsgcCodeRegion = x.PsgcCodeRegion,
                    PsgcCodeProvince = x.PsgcCodeProvince,
                    PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                    RegionName = _psgcNameCache.GetRegionName(x.PsgcCodeRegion),
                    ProvinceName = _psgcNameCache.GetProvinceName(x.PsgcCodeProvince),
                    MunicipalityName = _psgcNameCache.GetMunicipalityName(x.PsgcCodeMunicipality),
                    IncomeClassification = x.IncomeClassification,
                    SeniorCitizensPopulation = x.SeniorCitizensPopulation,
                    LswdoName = x.LswdoName,
                    LswdoPosition = x.LswdoPosition,
                    LswdoContactNumber = x.LswdoContactNumber,
                    LswdoEmail = x.LswdoEmail,
                    OscaHeadName = x.OscaHeadName,
                    MayorName = x.MayorName,
                    HasSeniorCitizenCenter = x.HasSeniorCitizenCenter,
                    HasCashIncentive = x.HasCashIncentive,
                    HasVaopHelpDesk = x.HasVaopHelpDesk,
                    UpdatedAt = x.UpdatedAt,
                    UpdatedBy = x.UpdatedBy
                })
                .OrderBy(x => x.ProvinceName)
                .ThenBy(x => x.MunicipalityName)
                .ToList();
        }

        public async Task<SeniorCitizenDirectoryDto?> GetByIdAsync(Guid id)
        {
            var x = await _context.SeniorCitizenDirectoryEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

            if (x == null) return null;

            return MapToDto(x);
        }

        public async Task<SeniorCitizenDirectoryEntry?> GetEntityByIdAsync(Guid id) =>
            await _context.SeniorCitizenDirectoryEntries.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

        public async Task<SeniorCitizenDirectoryEntry?> FindActiveByMunicipalityAsync(int psgcCodeMunicipality, Guid? excludeId = null) =>
            await _context.SeniorCitizenDirectoryEntries
                .FirstOrDefaultAsync(e => e.PsgcCodeMunicipality == psgcCodeMunicipality
                    && !e.IsDeleted
                    && (excludeId == null || e.Id != excludeId.Value));

        public async Task<int?> GetProvinceCodeByNameAsync(string name, int regionCode)
        {
            var normalized = name.Trim().ToLower();
            return await _context.Provinces.AsNoTracking()
                .Where(p => p.PsgcCodeRegion == regionCode && p.Name != null && p.Name.ToLower() == normalized)
                .Select(p => (int?)p.PsgcCodeProvince)
                .FirstOrDefaultAsync();
        }

        public async Task<int?> GetMunicipalityCodeByNameAsync(string name, int provinceCode)
        {
            var normalized = name.Trim().ToLower();
            return await _context.Municipalities.AsNoTracking()
                .Where(m => m.PsgcCodeProvince == provinceCode && m.Name != null && m.Name.ToLower() == normalized)
                .Select(m => (int?)m.PsgcCodeMunicipality)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(SeniorCitizenDirectoryEntry entry) =>
            await _context.SeniorCitizenDirectoryEntries.AddAsync(entry);

        public void SetOriginalRowVersion(SeniorCitizenDirectoryEntry entity, byte[] rowVersion) =>
            _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = rowVersion;

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException(
                    "This directory entry was modified by another user. Please refresh and try again.", ex);
            }
        }

        public async Task AddLogAsync(Guid? entryId, string activity, string userName)
        {
            await _context.Logs.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                Category = "SeniorCitizenDirectory",
                SeniorCitizenDirectoryEntryId = entryId,
                Activity = activity,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid entryId) =>
            await _context.Logs
                .AsNoTracking()
                .Where(l => l.SeniorCitizenDirectoryEntryId == entryId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new SeniorCitizenDirectoryHistoryDto
                {
                    Id = l.Id,
                    Activity = l.Activity,
                    UserName = l.UserName,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

        private SeniorCitizenDirectoryDto MapToDto(SeniorCitizenDirectoryEntry x) => new()
        {
            Id = x.Id,
            PsgcCodeRegion = x.PsgcCodeRegion,
            PsgcCodeProvince = x.PsgcCodeProvince,
            PsgcCodeMunicipality = x.PsgcCodeMunicipality,
            RegionName = _psgcNameCache.GetRegionName(x.PsgcCodeRegion),
            ProvinceName = _psgcNameCache.GetProvinceName(x.PsgcCodeProvince),
            MunicipalityName = _psgcNameCache.GetMunicipalityName(x.PsgcCodeMunicipality),
            IncomeClassification = x.IncomeClassification,
            SeniorCitizensPopulation = x.SeniorCitizensPopulation,
            PopulationAsOfNote = x.PopulationAsOfNote,
            LswdoName = x.LswdoName,
            LswdoPosition = x.LswdoPosition,
            LswdoContactNumber = x.LswdoContactNumber,
            LswdoEmail = x.LswdoEmail,
            ScFocalName = x.ScFocalName,
            ScFocalContactNumber = x.ScFocalContactNumber,
            ScFocalEmail = x.ScFocalEmail,
            OscaHeadName = x.OscaHeadName,
            OscaHeadLengthOfService = x.OscaHeadLengthOfService,
            OscaHeadContactNumber = x.OscaHeadContactNumber,
            OscaHeadEmail = x.OscaHeadEmail,
            FscapPresidentName = x.FscapPresidentName,
            FscapPresidentContactNumber = x.FscapPresidentContactNumber,
            FscapPresidentEmail = x.FscapPresidentEmail,
            FscapPresidentLengthOfService = x.FscapPresidentLengthOfService,
            HasSeniorCitizenCenter = x.HasSeniorCitizenCenter,
            IsSccAccredited = x.IsSccAccredited,
            SccAccreditationValidity = x.SccAccreditationValidity,
            WithoutSccResourcesNote = x.WithoutSccResourcesNote,
            SccManagedBy = x.SccManagedBy,
            ServicesOffered = x.ServicesOffered,
            HasCashIncentive = x.HasCashIncentive,
            CashIncentiveDetails = x.CashIncentiveDetails,
            HasSupportingOrdinance = x.HasSupportingOrdinance,
            OrdinanceDocumentLinks = x.OrdinanceDocumentLinks,
            HasVaopHelpDesk = x.HasVaopHelpDesk,
            VaopReferralMechanism = x.VaopReferralMechanism,
            MayorName = x.MayorName,
            MayorOfficeEmail = x.MayorOfficeEmail,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy,
            RowVersion = x.RowVersion
        };
    }
}
