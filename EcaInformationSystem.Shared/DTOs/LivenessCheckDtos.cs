namespace EcaInformationSystem.Shared.DTOs
{
    // Deliberately limited — shown on the anonymous public link, so it must
    // exclude payment, eligibility, bank, and remarks data.
    public class LivenessPublicViewDto
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int Sex { get; set; }
        public string? OscaIdNumber { get; set; }
        public string? TrackingNumber { get; set; }
        public string? RegionName { get; set; }
        public string? ProvinceName { get; set; }
        public string? MunicipalityName { get; set; }
        public string? BarangayName { get; set; }
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? ZipCode { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, Submitted, etc.
    }

    public class LivenessSubmitRequestDto
    {
        public string PhotoBase64 { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/jpeg";
    }

    public class LivenessCheckLinkDto
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class LivenessCheckHistoryItemDto
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = string.Empty;
        // Only meaningful while IsActive && Status == "Pending" — the link is
        // still viewable/copyable so the PDO doesn't need to regenerate (which
        // would invalidate it) just to retrieve it again.
        public string? Url { get; set; }
        public DateTime GeneratedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewNotes { get; set; }
        public bool HasPhoto { get; set; }
    }

    public class LivenessReviewRequestDto
    {
        public string? Notes { get; set; }
    }

    // Pushed over SignalR (LivenessNotificationHub) to Admins/SuperAdmins and
    // to PDOs whose jurisdiction covers the grantee's municipality, the
    // instant a grantee successfully submits their liveness photo.
    public class LivenessSubmittedNotificationDto
    {
        public Guid RecordId { get; set; }
        public Guid BeneficiaryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? MunicipalityName { get; set; }
        public DateTime SubmittedDate { get; set; }
    }

    // One row in the PDO/Admin notification bell's "Liveness" tab — a
    // grantee whose submitted photo is still awaiting review.
    public class LivenessPendingReviewItemDto
    {
        public Guid RecordId { get; set; }
        public Guid BeneficiaryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? MunicipalityName { get; set; }
        public DateTime SubmittedDate { get; set; }
    }
}
