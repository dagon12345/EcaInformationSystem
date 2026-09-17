namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    public class ApplicationGranteeRowDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public int SortOrder { get; set; }

        public bool HasFinding { get; set; }
        public string? FindingNote { get; set; }
        public DateTime? FindingSetAt { get; set; }
        public string? FindingSetByName { get; set; }
        public DateTime? FindingResolvedAt { get; set; }

        // Null until the responsible PDO has actually set it — the UI shows
        // "-" rather than assuming a default.
        public bool? IsEligible { get; set; }
        public DateTime? Birthdate { get; set; }
        // Derived from Birthdate at the time of mapping (see
        // ApplicationTrackingService.ComputeAge) — not stored, so it's
        // always current as of whenever this DTO was produced.
        public int? Age { get; set; }
        public string? Sex { get; set; }
        public string? IneligibilityReason { get; set; }
    }

    public class CreateApplicationGranteeRowDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Extension { get; set; }
    }

    // PUT .../rows/{rowId}/eligibility — a PDO (within jurisdiction for the
    // batch's municipality), Admin, or SuperAdmin filling in the Eligible/
    // Birthday/Sex/Reason columns from the expanded batch card.
    public class UpdateGranteeEligibilityDto
    {
        public bool? IsEligible { get; set; }
        public DateTime? Birthdate { get; set; }
        public string? Sex { get; set; }
        // Required by the server whenever IsEligible is explicitly false.
        public string? IneligibilityReason { get; set; }
    }
}
