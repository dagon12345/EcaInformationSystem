namespace EcaInformationSystem.Domain.Entities
{
    // One row per (UserId, Date) — mirrors the print view's own rule that a
    // day is exactly one of WFH / Holiday / a free-text note, never more
    // than one at once. Deleted entirely when a day is unmarked, rather than
    // storing an explicit "None" row.
    public class DtrDayMark
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }

        // "Wfh" | "Holiday" | "Note"
        public string MarkType { get; set; } = string.Empty;

        // Only populated when MarkType is "Note".
        public string? NoteText { get; set; }

        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }
}
