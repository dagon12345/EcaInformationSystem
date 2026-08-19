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
    }

    public class CreateApplicationGranteeRowDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Extension { get; set; }
    }
}
