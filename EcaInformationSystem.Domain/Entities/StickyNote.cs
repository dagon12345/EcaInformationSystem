namespace EcaInformationSystem.Domain.Entities
{
    // Strictly private — always scoped to UserId server-side, with no admin or
    // public read path. This is a personal notepad/checklist, not shared content.
    public class StickyNote
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        // "Note" (free text, uses Content) or "Checklist" (uses ChecklistItemsJson)
        public string Type { get; set; } = "Note";
        public string? Content { get; set; }

        // JSON array of { text, isChecked } — a lightweight child list doesn't
        // pull its weight for something this small and always loaded whole.
        public string? ChecklistItemsJson { get; set; }

        public string Color { get; set; } = "yellow";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
