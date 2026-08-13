// WHY THIS EXISTS:
// The grid never displays full Remarks/AssessmentRemarks/EligibilityRemarks
// text — only truncated previews. Yet your current query pulls those full
// text columns for every row at every page size, including 5000. This DTO
// is intentionally narrow: only what the grid actually renders. The full
// BeneficiaryInformationDto (with complete remarks) still exists and is
// still used by GetByIdAsync for the single-record Details view/Edit form —
// nothing about that path changes.

using System.Text.Json;
using EcaInformationSystem.Shared.Helpers;

namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryListItemDto
    {
        public Guid Id { get; set; }

        public int? Quarter { get; set; }
        public string? Batch { get; set; }
        public int? RefYear { get; set; }
        public string? RefCode { get; set; }

        public string? BatchCode { get; set; }
        public List<BeneficiaryPhoneNumberDto> PhoneNumbers { get; set; } = new();
        public string? LastName { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public int MilestoneYear { get; set; }
        public int Sex { get; set; }

        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public string? BarangayName { get; set; }
        public string? Validator { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
        public int PaymentStatus { get; set; }
        // ✅ NEW — Replacement Status of the CURRENT payment history entry (the
        // same entry PaymentStatus/PayrollQuarter/FiscalYear above are sourced
        // from). Lets the grid show a quick-glance indicator without a column of
        // its own; full replacement detail (who/what it's linked to) still lives
        // in the Payment History section of the offcanvas.
        public int? CurrentEntryReplacementStatus { get; set; }
        public int ModeOfPayment { get; set; }
        public bool IsEligible { get; set; }
        public bool IsCompliant { get; set; }
        public int? FindingStatus { get; set; }
        public int? CoStatus { get; set; }
        public DateTime? CoDateEndorsed { get; set; }
        public DateTime? CoDateApproved { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int PaymentHistoryCount { get; set; }
        public string? PaymentHistorySummary { get; set; }   // ✅ new — hover tooltip text
        public bool HasDocuments { get; set; }

        public string? EligibilityRemarksPreview { get; set; }
        public string? AssessmentRemarksPreview { get; set; }
        public string? FindingRemarksPreview { get; set; }

        public byte[]? RowVersion { get; set; }
        public DateTime? DateEndorsed { get; set; }
        public DateTime? DateApplied { get; set; }
        public int? NcscRrn { get; set; }
        public bool? IsLivenessVerified { get; set; }
        public DateTime? DateOfLiveness { get; set; }
        public bool? IsReadyForEft { get; set; }


        // ✅ Now passes RefCode — generated once, stored in DB
        public string? ReferenceNumber =>
            RegionRomanNumeralHelper.BuildReferenceNumber(
                Quarter, Batch, RefYear, RefCode, PsgcCodeRegion);

        // ✅ True when this grantee is 80+ but never crossed a qualifying
        // milestone — their 80th (and every later) birthday fell before the
        // ECA program's actual start date (March 17, 2024). Lets the grid
        // flag these permanently-ineligible rows at a glance instead of
        // looking identical to an ordinary "not yet eligible" grantee.
        public bool MissedProgramStartCutoff => EcaEligibilityHelper.MissedProgramStartCutoff(BirthDate);
    }
}