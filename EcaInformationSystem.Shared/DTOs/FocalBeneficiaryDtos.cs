namespace EcaInformationSystem.Shared.DTOs
{
    // Deliberately narrow — NOT BeneficiaryInformationDto/BeneficiaryListItemDto,
    // which carry bank accounts, claimant/family data, and phone numbers. A
    // Focal is an external partner-LGU contact with read-only access to
    // exactly one municipality; this is the safe subset for that. Assessment/
    // finding remarks ARE included (detail view only) — a Focal verifying a
    // grantee in person needs to know *why* something's flagged, not just
    // the status label.
    public class FocalBeneficiaryListItemDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? BarangayName { get; set; }
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public string SexLabel { get; set; } = string.Empty;
        public bool IsCompliant { get; set; }
        public string ComplianceLabel { get; set; } = string.Empty;
        public string? AssessmentRemarksPreview { get; set; }
        public string FindingLabel { get; set; } = string.Empty;
        public string? FindingRemarksPreview { get; set; }
        public string PaymentStatusLabel { get; set; } = string.Empty;
    }

    public class FocalBeneficiaryDetailDto
    {
        public Guid Id { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public string SexLabel { get; set; } = string.Empty;
        public string? CivilStatusLabel { get; set; }
        public string? BarangayName { get; set; }
        public string? MunicipalityName { get; set; }
        public string? ProvinceName { get; set; }
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public int MilestoneYear { get; set; }
        public bool IsCompliant { get; set; }
        public string ComplianceLabel { get; set; } = string.Empty;
        // Why — so a Focal verifying this grantee on the ground knows what's
        // actually being flagged, not just "Yes (w/ Minor Findings)".
        public string? AssessmentRemarks { get; set; }
        public string FindingLabel { get; set; } = string.Empty;
        public string? FindingRemarks { get; set; }
        public string PaymentStatusLabel { get; set; } = string.Empty;
    }

    // No municipality field — it's never client-supplied, always resolved
    // server-side from the Focal's own assigned jurisdiction.
    public class FocalBeneficiaryFilterDto
    {
        public string? SearchName { get; set; }
        public int? PaymentStatus { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class FocalBeneficiaryPageDto
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
        public PagedResultDto<FocalBeneficiaryListItemDto> Items { get; set; } = new();
    }
}
