namespace EcaInformationSystem.Shared.DTOs.Dtr
{
    // "Workday" = normal day with punch-derived AM/PM in/out; "Saturday"/"Sunday" render
    // as a single merged label row on the printed form, matching Civil Service Form No. 48.
    public enum DtrDayType
    {
        Workday = 0,
        Saturday = 1,
        Sunday = 2,

        // Never set by DtrService (punches can't tell us this) — the print
        // preview lets the preparer mark a workday as WFH before printing.
        // Persisted via DtrDayMark, not derived from punches.
        WorkFromHome = 3
    }

    public class DtrDayDto
    {
        public int Day { get; set; }
        public DtrDayType DayType { get; set; }

        // False for days outside the selected period (e.g. days 1-15 when the
        // filter is 16-31) — the form still shows the full month, but those
        // rows render as a single diagonal slash instead of AM/PM columns.
        public bool IsInPeriod { get; set; } = true;

        // Pre-formatted ("h:mm tt") — null/empty means the cell prints blank, same as the paper form.
        public string? AmTimeIn { get; set; }
        public string? AmTimeOut { get; set; }
        public string? PmTimeIn { get; set; }
        public string? PmTimeOut { get; set; }

        // Non-null (the underlying AttendanceLog.Id) whenever this slot has a
        // punch — for BOTH device-synced and manually-entered times, so the
        // UI can offer "clear this time" (then re-enter it) on any slot, not
        // just ones that were already manually entered.
        public int? AmTimeInLogId { get; set; }
        public int? AmTimeOutLogId { get; set; }
        public int? PmTimeInLogId { get; set; }
        public int? PmTimeOutLogId { get; set; }

        public int UndertimeHours { get; set; }
        public int UndertimeMinutes { get; set; }
    }

    public class DtrDocumentDto
    {
        public Guid UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string ForTheMonthLabel { get; set; } = string.Empty; // e.g. "July 1-15, 2026"

        // Freeform, matches the "Official hours for arrival and departure" boxes on the
        // paper form — no fixed office-wide schedule is stored anywhere, so these are
        // editable in the print preview rather than computed.
        public string RegularDaysHours { get; set; } = string.Empty;
        public string SaturdaysHours { get; set; } = string.Empty;

        public List<DtrDayDto> Days { get; set; } = new();
        public int TotalUndertimeHours { get; set; }
        public int TotalUndertimeMinutes { get; set; }

        public string ApprovedByName { get; set; } = "CESAR A. ADEGUE IV, PHD, CESE";
        public string ApprovedByPosition { get; set; } = "Regional Director";
    }

    // A saved WFH/Holiday/Note mark for one day — see DtrDayMark (Domain).
    public class DtrDayMarkDto
    {
        public DateTime Date { get; set; }
        public string MarkType { get; set; } = string.Empty; // "Wfh" | "Holiday" | "Note"
        public string? NoteText { get; set; }

        // null = whole-day mark. "AmIn" | "AmOut" | "PmIn" | "PmOut" = a
        // per-column Note starting at that cell — the other, un-covered
        // cells that day keep showing their real punch times/editors.
        public string? Slot { get; set; }

        // The last column this note covers (inclusive) — same value as Slot
        // for a single-column note, or a later slot to span consecutive
        // columns (e.g. Slot="AmOut", SlotEnd="PmOut" covers 3 columns).
        // Only meaningful when Slot is non-null.
        public string? SlotEnd { get; set; }
    }

    public class SetDtrDayMarkRequestDto
    {
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public string MarkType { get; set; } = string.Empty; // "Wfh" | "Holiday" | "Note"
        public string? NoteText { get; set; }
        public string? Slot { get; set; } // null | "AmIn" | "AmOut" | "PmIn" | "PmOut" — Note only
        public string? SlotEnd { get; set; } // null = same as Slot — Note only
    }

    // SuperAdmin/Finance filling in a punch the employee forgot to make.
    public class AddManualPunchRequestDto
    {
        public Guid UserId { get; set; }
        public DateTime PunchDateTime { get; set; }
    }

    // Lightweight row for the SuperAdmin "all users" attendance overview.
    public class DtrSummaryDto
    {
        public Guid UserId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string? BiometricUserId { get; set; }
        public int DaysWithPunches { get; set; }
        public int TotalUndertimeHours { get; set; }
        public int TotalUndertimeMinutes { get; set; }
    }
}
