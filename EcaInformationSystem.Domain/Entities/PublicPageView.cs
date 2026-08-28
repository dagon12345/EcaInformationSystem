namespace EcaInformationSystem.Domain.Entities
{
    // One row per public marketing page (Features, About, Regional Offices,
    // Directory of Officials, Developer) — a simple hit counter, not a
    // per-visitor log. Client-side dedupes "already counted this session"
    // via localStorage before ever calling the increment endpoint, so this
    // stays a single UPDATE per page per browser session, not a growing table.
    public class PublicPageView
    {
        public Guid Id { get; set; }
        public string PageKey { get; set; } = string.Empty;
        public int ViewCount { get; set; }
        public DateTime LastViewedAt { get; set; }
    }
}
