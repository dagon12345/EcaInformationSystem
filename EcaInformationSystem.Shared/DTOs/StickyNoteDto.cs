namespace EcaInformationSystem.Shared.DTOs
{
    // Free-text notes only — no separate "Checklist" type. Bullet ("- ") and
    // checklist ("- [ ] " / "- [x] ") lines are just plain markdown-style
    // prefixes inside Content, parsed/rendered client-side, the same way a
    // paper notepad lets you draw a checkbox next to any line you want.
    public class StickyNoteDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Color { get; set; } = "yellow";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class StickyNoteUpsertDto
    {
        // null = create a new note
        public Guid? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Color { get; set; } = "yellow";
    }
}
