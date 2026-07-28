namespace EcaInformationSystem.Shared.DTOs
{
    public class StickyNoteChecklistItemDto
    {
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
    }

    public class StickyNoteDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;

        // "Note" or "Checklist"
        public string Type { get; set; } = "Note";
        public string? Content { get; set; }
        public List<StickyNoteChecklistItemDto> ChecklistItems { get; set; } = new();
        public string Color { get; set; } = "yellow";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class StickyNoteUpsertDto
    {
        // null = create a new note
        public Guid? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = "Note";
        public string? Content { get; set; }
        public List<StickyNoteChecklistItemDto> ChecklistItems { get; set; } = new();
        public string Color { get; set; } = "yellow";
    }
}
