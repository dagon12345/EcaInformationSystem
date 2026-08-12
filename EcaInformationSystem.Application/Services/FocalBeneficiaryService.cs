using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    // Read-only, single-municipality view for external partner-LGU "Focal"
    // contacts. The municipality is NEVER trusted from the caller — it's
    // always resolved from the Focal's own PdoJurisdiction rows (the same
    // ones assigned when they were invited — see FocalInviteService), so
    // there's no way to request another municipality's data by tampering
    // with the request body. Delegates the actual query to the existing,
    // battle-tested IBeneficiaryInformationService, then narrows the
    // response to a deliberately small field set before it ever leaves
    // this service.
    public class FocalBeneficiaryService : IFocalBeneficiaryService
    {
        private readonly IBeneficiaryInformationService _beneficiaryService;
        private readonly IPendingUserRegistrationRepository _userRepo;

        public FocalBeneficiaryService(IBeneficiaryInformationService beneficiaryService, IPendingUserRegistrationRepository userRepo)
        {
            _beneficiaryService = beneficiaryService;
            _userRepo = userRepo;
        }

        public async Task<FocalBeneficiaryPageDto> GetPagedAsync(Guid focalUserId, FocalBeneficiaryFilterDto filter)
        {
            var jurisdictions = await _userRepo.GetJurisdictionsByUserIdAsync(focalUserId);
            if (jurisdictions.Count == 0)
            {
                return new FocalBeneficiaryPageDto
                {
                    MunicipalityName = "No municipality assigned",
                    Items = new PagedResultDto<FocalBeneficiaryListItemDto> { PageNumber = filter.PageNumber, PageSize = filter.PageSize }
                };
            }

            // Business rule: a Focal has exactly one municipality. Defensive
            // against the schema technically allowing more — if it ever does,
            // scope to all of them rather than silently dropping data.
            var municipalityCodes = jurisdictions.Select(j => j.PsgcCodeMunicipality).Distinct().ToList();
            var primary = jurisdictions.First();

            var beneficiaryFilter = new BeneficiaryFilterDto
            {
                FullName = filter.SearchName,
                // ⚠️ GetPagedListAsync's query only ever reads PaymentStatuses
                // (plural, multi-select) — the singular PaymentStatus field is
                // dead in that code path (and missing from its cache key too),
                // so a filter built with the singular field is silently
                // ignored. Use the plural field here even for a single value.
                PaymentStatuses = filter.PaymentStatus.HasValue ? new List<int> { filter.PaymentStatus.Value } : new(),
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                PsgcCodeMunicipalities = municipalityCodes
            };

            var result = await _beneficiaryService.GetPagedListAsync(beneficiaryFilter);

            return new FocalBeneficiaryPageDto
            {
                MunicipalityName = primary.MunicipalityName,
                ProvinceName = primary.ProvinceName,
                Items = new PagedResultDto<FocalBeneficiaryListItemDto>
                {
                    TotalCount = result.TotalCount,
                    PageNumber = result.PageNumber,
                    PageSize = result.PageSize,
                    Items = result.Items.Select(ToListItem).ToList()
                }
            };
        }

        public async Task<FocalBeneficiaryDetailDto> GetDetailAsync(Guid focalUserId, Guid beneficiaryId)
        {
            var jurisdictions = await _userRepo.GetJurisdictionsByUserIdAsync(focalUserId);
            var allowedCodes = jurisdictions.Select(j => j.PsgcCodeMunicipality).ToHashSet();

            var beneficiary = await _beneficiaryService.GetByIdAsync(beneficiaryId)
                ?? throw new KeyNotFoundException("Record not found.");

            // Looks like "not found" rather than "forbidden" — doesn't confirm
            // or deny that a record outside their scope exists at all.
            if (!allowedCodes.Contains(beneficiary.PsgcCodeMunicipality))
                throw new KeyNotFoundException("Record not found.");

            // Municipality/Province come from the Focal's own jurisdiction
            // record (already resolved plain text, no guessing) rather than
            // the beneficiary DTO's raw JsonElement fields — every record a
            // Focal can see is in this same municipality anyway.
            var jurisdiction = jurisdictions.First(j => j.PsgcCodeMunicipality == beneficiary.PsgcCodeMunicipality);

            return new FocalBeneficiaryDetailDto
            {
                Id = beneficiary.Id,
                LastName = beneficiary.LastName ?? string.Empty,
                FirstName = beneficiary.FirstName,
                MiddleName = beneficiary.MiddleName,
                Extension = beneficiary.Extension,
                BirthDate = beneficiary.BirthDate,
                Age = beneficiary.Age,
                SexLabel = MapSexLabel(beneficiary.Sex),
                CivilStatusLabel = MapCivilStatusLabel(beneficiary.CivilStatus),
                BarangayName = ExtractName(beneficiary.Barangay),
                MunicipalityName = jurisdiction.MunicipalityName,
                ProvinceName = jurisdiction.ProvinceName,
                BatchCode = beneficiary.BatchCode,
                OscaIdNumber = beneficiary.OscaIdNumber,
                MilestoneYear = beneficiary.MilestoneYear,
                IsCompliant = beneficiary.IsCompliant,
                ComplianceLabel = MapComplianceLabel(beneficiary.IsCompliant, beneficiary.AssessmentRemarks),
                AssessmentRemarks = beneficiary.AssessmentRemarks,
                FindingLabel = MapFindingLabel(beneficiary.FindingStatus),
                FindingRemarks = beneficiary.FindingRemarks,
                PaymentStatusLabel = MapPaymentStatusLabel(beneficiary.PaymentStatus)
            };
        }

        private static FocalBeneficiaryListItemDto ToListItem(BeneficiaryListItemDto b) => new()
        {
            Id = b.Id,
            FullName = $"{b.LastName}, {b.FirstName}{(string.IsNullOrWhiteSpace(b.MiddleName) ? "" : " " + b.MiddleName)}{(string.IsNullOrWhiteSpace(b.Extension) ? "" : " " + b.Extension)}",
            BarangayName = b.BarangayName,
            BirthDate = b.BirthDate,
            Age = b.Age,
            SexLabel = MapSexLabel(b.Sex),
            IsCompliant = b.IsCompliant,
            ComplianceLabel = MapComplianceLabel(b.IsCompliant, b.AssessmentRemarksPreview),
            AssessmentRemarksPreview = b.AssessmentRemarksPreview,
            FindingLabel = MapFindingLabel(b.FindingStatus),
            FindingRemarksPreview = b.FindingRemarksPreview,
            PaymentStatusLabel = MapPaymentStatusLabel(b.PaymentStatus)
        };

        // Same labeling convention as GridView.razor's GetComplianceLabel/
        // GetFindingStatusText/GetPaymentStatusText, kept in sync deliberately
        // so a grantee's status reads identically whether staff or a Focal
        // is looking at it.
        private static string MapComplianceLabel(bool isCompliant, string? assessmentRemarks) =>
            isCompliant && !string.IsNullOrWhiteSpace(assessmentRemarks) ? "Yes (w/ Minor Findings)" : isCompliant ? "Yes" : "No";

        private static string MapFindingLabel(int? status) => status switch
        {
            0 => "N/A",
            1 => "Solved",
            2 => "Unresolved",
            _ => "N/A"
        };

        private static string MapPaymentStatusLabel(int? status) => status switch
        {
            0 => "N/A",
            1 => "Unpaid",
            2 => "Paid",
            3 => "Pending",
            _ => "Unknown"
        };

        private static string MapSexLabel(int sex) => sex switch
        {
            1 => "Male",
            2 => "Female",
            _ => "Unknown"
        };

        private static string MapCivilStatusLabel(int? status) => status switch
        {
            1 => "Single",
            2 => "Widowed",
            3 => "Married",
            4 => "Common-Law",
            5 => "Others",
            _ => "None"
        };

        // ⚠️ Was throwing InvalidOperationException (→ 500 on the detail
        // endpoint, surfaced client-side as the offcanvas erroring out) when
        // Barangay/Municipality/Province held a JsonElement with ValueKind
        // Undefined — TryGetProperty throws on that kind rather than just
        // returning false, so it must be checked before calling it, and the
        // whole thing kept defensive since these fields' exact shape isn't
        // guaranteed.
        private static string? ExtractName(System.Text.Json.JsonElement? element)
        {
            if (!element.HasValue) return null;

            try
            {
                var value = element.Value;
                if (value.ValueKind == System.Text.Json.JsonValueKind.Object && value.TryGetProperty("name", out var nameProp))
                    return nameProp.GetString();
                if (value.ValueKind == System.Text.Json.JsonValueKind.String)
                    return value.GetString();
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
