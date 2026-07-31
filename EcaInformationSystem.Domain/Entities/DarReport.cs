namespace EcaInformationSystem.Domain.Entities
{
    // Semi-monthly (every-15-days) accomplishment report for a Contract of Service
    // employee. Strictly private — always scoped to UserId server-side, same as
    // StickyNote; no admin or "view other user's reports" path.
    public class DarReport
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Snapshotted from the user's profile at creation, editable, never
        // retroactively overwritten by later profile edits.
        public string PreparedByName { get; set; } = string.Empty;
        public string PreparedByPosition { get; set; } = string.Empty;
        public string NotedByName { get; set; } = string.Empty;
        public string NotedByPosition { get; set; } = string.Empty;

        // When true, every entry's effective "Essential Functions" text falls
        // back to SharedEssentialFunctions unless it has its own override.
        public bool UseSharedEssentialFunctions { get; set; } = true;
        public string? SharedEssentialFunctions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<DarEntry> Entries { get; set; } = new List<DarEntry>();
    }
}
