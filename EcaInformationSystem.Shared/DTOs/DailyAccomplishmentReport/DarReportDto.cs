namespace EcaInformationSystem.Shared.DTOs.DailyAccomplishmentReport
{
    public class DarEntryDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string? EssentialFunctionsOverride { get; set; }
        public List<string> AccomplishmentBullets { get; set; } = new();
        public bool IsWorkFromHome { get; set; }
    }

    public class DarReportDto
    {
        public Guid Id { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string PreparedByName { get; set; } = string.Empty;
        public string PreparedByPosition { get; set; } = string.Empty;
        public string NotedByName { get; set; } = string.Empty;
        public string NotedByPosition { get; set; } = string.Empty;
        public bool UseSharedEssentialFunctions { get; set; } = true;
        public string? SharedEssentialFunctions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<DarEntryDto> Entries { get; set; } = new();
    }

    // Summary row for the report list page — no Entries, keeps the list light.
    public class DarReportListItemDto
    {
        public Guid Id { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // null Id = create a new report; non-null = update that report.
    public class DarReportUpsertDto
    {
        public Guid? Id { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string PreparedByName { get; set; } = string.Empty;
        public string PreparedByPosition { get; set; } = string.Empty;
        public string NotedByName { get; set; } = string.Empty;
        public string NotedByPosition { get; set; } = string.Empty;
        public bool UseSharedEssentialFunctions { get; set; } = true;
        public string? SharedEssentialFunctions { get; set; }
        public List<DarEntryDto> Entries { get; set; } = new();
    }
}
