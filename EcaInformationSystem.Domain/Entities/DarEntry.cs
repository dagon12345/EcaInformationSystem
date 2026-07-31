namespace EcaInformationSystem.Domain.Entities
{
    // One calendar day within a DarReport's period. Date/day-of-week are never
    // typed by hand — Date drives everything and DayOfWeek is derived from it,
    // never stored.
    public class DarEntry
    {
        public Guid Id { get; set; }
        public Guid DarReportId { get; set; }
        public DarReport? DarReport { get; set; }

        public DateTime Date { get; set; }

        // Null falls back to the parent report's SharedEssentialFunctions —
        // only set when this specific day's duties differ from the rest.
        public string? EssentialFunctionsOverride { get; set; }

        // Newline-delimited bullet lines — split/joined at the DTO boundary.
        // No JSON-list precedent for something this small elsewhere in the app.
        public string? AccomplishmentText { get; set; }

        // A day is either worked from home or not — renders a bold "Work from
        // home" label above this day's bullets in print.
        public bool IsWorkFromHome { get; set; }
    }
}
