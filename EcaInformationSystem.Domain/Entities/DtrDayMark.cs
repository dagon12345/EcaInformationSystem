namespace EcaInformationSystem.Domain.Entities
{
    // One row per (UserId, Date, Slot). Slot = null is the WHOLE-DAY mark
    // (WFH, Holiday, or a full-day Note) — mutually exclusive with every
    // other mark on that date, same "day is exactly one thing" rule as
    // before. Slot = "AmIn" | "AmOut" | "PmIn" | "PmOut" is a per-COLUMN
    // Note attached to just that one punch cell — multiple of these can
    // coexist on the same date (e.g. a note on AmIn and a separate one on
    // PmOut), and they coexist with normal, unnoted punch cells for the
    // other slots. Setting either kind clears the other kind for that date
    // (see DtrDayMarkService) — a day is either "one whole-day thing" or
    // "some mix of individually-noted/unnoted columns", never both at once.
    // Deleted entirely when unmarked, rather than storing an explicit
    // "None" row.
    public class DtrDayMark
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }

        // "Wfh" | "Holiday" | "Note"
        public string MarkType { get; set; } = string.Empty;

        // Only populated when MarkType is "Note".
        public string? NoteText { get; set; }

        // null = whole-day mark. "AmIn" | "AmOut" | "PmIn" | "PmOut" = a
        // per-column Note (only ever set when MarkType is "Note" — Wfh/
        // Holiday are inherently whole-day and never carry a Slot). This is
        // the START of the note's span — a single-column note has
        // SlotEnd == Slot; a note covering e.g. AmOut through PmOut has
        // Slot="AmOut", SlotEnd="PmOut", and renders as one merged cell
        // spanning those 3 columns while AmIn keeps its own real punch cell.
        public string? Slot { get; set; }

        // Same value as Slot for a single-column note. Only meaningful when
        // Slot is non-null — always null for whole-day marks.
        public string? SlotEnd { get; set; }

        public DateTime UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }
    }
}
