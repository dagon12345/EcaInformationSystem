using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class SeniorCitizenDirectoryService : ISeniorCitizenDirectoryService
    {
        private readonly ISeniorCitizenDirectoryRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;

        public SeniorCitizenDirectoryService(ISeniorCitizenDirectoryRepository repo, IPsgcNameCache psgcNameCache)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
        }

        public async Task<List<SeniorCitizenDirectoryListItemDto>> GetAllAsync(int regionCode) =>
            await _repo.GetAllAsync(regionCode);

        public async Task<SeniorCitizenDirectoryDto?> GetByIdAsync(Guid id, int regionCode)
        {
            var result = await _repo.GetByIdAsync(id);
            // Treat a cross-region entry as "not found" rather than leaking its
            // existence/detail to a user outside that region.
            return result != null && result.PsgcCodeRegion == regionCode ? result : null;
        }

        public async Task<List<SeniorCitizenDirectoryHistoryDto>> GetHistoryAsync(Guid id, int regionCode)
        {
            var entity = await _repo.GetEntityByIdAsync(id);
            if (entity == null || entity.PsgcCodeRegion != regionCode) return new();
            return await _repo.GetHistoryAsync(id);
        }

        public async Task<SeniorCitizenDirectoryDto> CreateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode)
        {
            if (dto.PsgcCodeRegion <= 0 || dto.PsgcCodeProvince <= 0 || dto.PsgcCodeMunicipality <= 0)
                throw new Exception("Region, Province, and Municipality are required.");

            if (dto.PsgcCodeRegion != regionCode)
                throw new Exception("You can only add directory entries for your own region.");

            var existing = await _repo.FindActiveByMunicipalityAsync(dto.PsgcCodeMunicipality);
            if (existing != null)
                throw new Exception("A directory entry for this municipality/city already exists. Edit that entry instead of creating a new one.");

            var entry = new SeniorCitizenDirectoryEntry
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };
            ApplyFields(entry, dto);

            await _repo.AddAsync(entry);
            await _repo.AddLogAsync(entry.Id,
                $"Added Senior Citizens Directory entry for {MunicipalityLabel(entry)}", userName);
            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entry.Id);
            return result!;
        }

        public async Task<SeniorCitizenDirectoryDto> UpdateAsync(UpsertSeniorCitizenDirectoryDto dto, string userName, int regionCode)
        {
            if (dto.Id is null || dto.Id == Guid.Empty)
                throw new Exception("Entry Id is required for an update.");

            if (dto.PsgcCodeRegion <= 0 || dto.PsgcCodeProvince <= 0 || dto.PsgcCodeMunicipality <= 0)
                throw new Exception("Region, Province, and Municipality are required.");

            if (dto.PsgcCodeRegion != regionCode)
                throw new Exception("You can only edit directory entries for your own region.");

            var entity = await _repo.GetEntityByIdAsync(dto.Id.Value)
                ?? throw new Exception("Directory entry not found.");

            if (entity.PsgcCodeRegion != regionCode)
                throw new Exception("You can only edit directory entries for your own region.");

            var duplicate = await _repo.FindActiveByMunicipalityAsync(dto.PsgcCodeMunicipality, dto.Id);
            if (duplicate != null)
                throw new Exception("Another directory entry already exists for this municipality/city.");

            if (dto.RowVersion != null)
                _repo.SetOriginalRowVersion(entity, dto.RowVersion);

            var changes = BuildChangeSummary(entity, dto);

            ApplyFields(entity, dto);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            if (changes.Count > 0)
            {
                var activity = $"Updated Senior Citizens Directory entry for {MunicipalityLabel(entity)} — " +
                                string.Join("; ", changes);
                await _repo.AddLogAsync(entity.Id, activity, userName);
            }

            await _repo.SaveChangesAsync();

            var result = await _repo.GetByIdAsync(entity.Id);
            return result!;
        }

        public async Task DeleteAsync(Guid id, string userName, int regionCode)
        {
            var entity = await _repo.GetEntityByIdAsync(id)
                ?? throw new Exception("Directory entry not found.");

            if (entity.PsgcCodeRegion != regionCode)
                throw new Exception("You can only delete directory entries for your own region.");

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = userName;

            await _repo.AddLogAsync(entity.Id,
                $"Deleted Senior Citizens Directory entry for {MunicipalityLabel(entity)}", userName);
            await _repo.SaveChangesAsync();
        }

        private string MunicipalityLabel(SeniorCitizenDirectoryEntry entity) =>
            _psgcNameCache.GetMunicipalityName(entity.PsgcCodeMunicipality) ?? $"municipality code {entity.PsgcCodeMunicipality}";

        private static void ApplyFields(SeniorCitizenDirectoryEntry entity, UpsertSeniorCitizenDirectoryDto dto)
        {
            entity.PsgcCodeRegion = dto.PsgcCodeRegion;
            entity.PsgcCodeProvince = dto.PsgcCodeProvince;
            entity.PsgcCodeMunicipality = dto.PsgcCodeMunicipality;

            entity.IncomeClassification = dto.IncomeClassification;
            entity.SeniorCitizensPopulation = dto.SeniorCitizensPopulation;
            entity.PopulationAsOfNote = dto.PopulationAsOfNote;

            entity.LswdoName = dto.LswdoName;
            entity.LswdoPosition = dto.LswdoPosition;
            entity.LswdoContactNumber = dto.LswdoContactNumber;
            entity.LswdoEmail = dto.LswdoEmail;

            entity.ScFocalName = dto.ScFocalName;
            entity.ScFocalContactNumber = dto.ScFocalContactNumber;
            entity.ScFocalEmail = dto.ScFocalEmail;

            entity.OscaHeadName = dto.OscaHeadName;
            entity.OscaHeadLengthOfService = dto.OscaHeadLengthOfService;
            entity.OscaHeadContactNumber = dto.OscaHeadContactNumber;
            entity.OscaHeadEmail = dto.OscaHeadEmail;

            entity.FscapPresidentName = dto.FscapPresidentName;
            entity.FscapPresidentContactNumber = dto.FscapPresidentContactNumber;
            entity.FscapPresidentEmail = dto.FscapPresidentEmail;
            entity.FscapPresidentLengthOfService = dto.FscapPresidentLengthOfService;

            entity.HasSeniorCitizenCenter = dto.HasSeniorCitizenCenter;
            entity.IsSccAccredited = dto.IsSccAccredited;
            entity.SccAccreditationValidity = dto.SccAccreditationValidity;
            entity.WithoutSccResourcesNote = dto.WithoutSccResourcesNote;
            entity.SccManagedBy = dto.SccManagedBy;
            entity.ServicesOffered = dto.ServicesOffered;

            entity.HasCashIncentive = dto.HasCashIncentive;
            entity.CashIncentiveDetails = dto.CashIncentiveDetails;
            entity.HasSupportingOrdinance = dto.HasSupportingOrdinance;
            entity.OrdinanceDocumentLinks = dto.OrdinanceDocumentLinks;

            entity.HasVaopHelpDesk = dto.HasVaopHelpDesk;
            entity.VaopReferralMechanism = dto.VaopReferralMechanism;

            entity.MayorName = dto.MayorName;
            entity.MayorOfficeEmail = dto.MayorOfficeEmail;
        }

        // ── Field-level diff for the audit log — labels chosen to read as a
        // sentence fragment after "Updated ... for <municipality> — ". ──────
        private static List<string> BuildChangeSummary(SeniorCitizenDirectoryEntry before, UpsertSeniorCitizenDirectoryDto after)
        {
            var changes = new List<string>();

            void Diff(string label, object? oldVal, object? newVal)
            {
                var oldStr = FormatValue(oldVal);
                var newStr = FormatValue(newVal);
                if (oldStr != newStr)
                    changes.Add($"{label} '{oldStr}' → '{newStr}'");
            }

            Diff("Income Classification", before.IncomeClassification, after.IncomeClassification);
            Diff("SC Population", before.SeniorCitizensPopulation, after.SeniorCitizensPopulation);
            Diff("Population As-Of Note", before.PopulationAsOfNote, after.PopulationAsOfNote);

            Diff("LSWDO Name", before.LswdoName, after.LswdoName);
            Diff("LSWDO Position", before.LswdoPosition, after.LswdoPosition);
            Diff("LSWDO Contact", before.LswdoContactNumber, after.LswdoContactNumber);
            Diff("LSWDO Email", before.LswdoEmail, after.LswdoEmail);

            Diff("SC Focal Name", before.ScFocalName, after.ScFocalName);
            Diff("SC Focal Contact", before.ScFocalContactNumber, after.ScFocalContactNumber);
            Diff("SC Focal Email", before.ScFocalEmail, after.ScFocalEmail);

            Diff("OSCA Head Name", before.OscaHeadName, after.OscaHeadName);
            Diff("OSCA Head Length of Service", before.OscaHeadLengthOfService, after.OscaHeadLengthOfService);
            Diff("OSCA Head Contact", before.OscaHeadContactNumber, after.OscaHeadContactNumber);
            Diff("OSCA Head Email", before.OscaHeadEmail, after.OscaHeadEmail);

            Diff("FSCAP President Name", before.FscapPresidentName, after.FscapPresidentName);
            Diff("FSCAP President Contact", before.FscapPresidentContactNumber, after.FscapPresidentContactNumber);
            Diff("FSCAP President Email", before.FscapPresidentEmail, after.FscapPresidentEmail);
            Diff("FSCAP President Length of Service", before.FscapPresidentLengthOfService, after.FscapPresidentLengthOfService);

            Diff("Has Senior Citizen Center", before.HasSeniorCitizenCenter, after.HasSeniorCitizenCenter);
            Diff("SCC Accredited", before.IsSccAccredited, after.IsSccAccredited);
            Diff("SCC Accreditation Validity", before.SccAccreditationValidity, after.SccAccreditationValidity);
            Diff("Without-SCC Resources Note", before.WithoutSccResourcesNote, after.WithoutSccResourcesNote);
            Diff("SCC Managed By", before.SccManagedBy, after.SccManagedBy);
            Diff("Services Offered", before.ServicesOffered, after.ServicesOffered);

            Diff("Has Cash Incentive", before.HasCashIncentive, after.HasCashIncentive);
            Diff("Cash Incentive Details", before.CashIncentiveDetails, after.CashIncentiveDetails);
            Diff("Has Supporting Ordinance", before.HasSupportingOrdinance, after.HasSupportingOrdinance);
            Diff("Ordinance Document Links", before.OrdinanceDocumentLinks, after.OrdinanceDocumentLinks);

            Diff("Has VAOP Help Desk", before.HasVaopHelpDesk, after.HasVaopHelpDesk);
            Diff("VAOP Referral Mechanism", before.VaopReferralMechanism, after.VaopReferralMechanism);

            Diff("Mayor Name", before.MayorName, after.MayorName);
            Diff("Mayor Office Email", before.MayorOfficeEmail, after.MayorOfficeEmail);

            if (before.PsgcCodeRegion != after.PsgcCodeRegion ||
                before.PsgcCodeProvince != after.PsgcCodeProvince ||
                before.PsgcCodeMunicipality != after.PsgcCodeMunicipality)
            {
                changes.Add("Location");
            }

            return changes;
        }

        private static string FormatValue(object? value) => value switch
        {
            null => "—",
            bool b => b ? "Yes" : "No",
            string s when string.IsNullOrWhiteSpace(s) => "—",
            _ => value.ToString() ?? "—"
        };
    }
}
