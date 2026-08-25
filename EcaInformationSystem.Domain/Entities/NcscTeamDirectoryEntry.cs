using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // Nationwide directory of NCSC ECA team members (field/regional offices) —
    // one row per PERSON, unlike SeniorCitizenDirectoryEntry which is one row
    // per municipality. Spans every region (not just Caraga), so — unlike
    // Senior Citizen Directory — it is NOT region-scoped for editing: only
    // Admin/SuperAdmin add/edit/delete/import, everyone else is view-only.
    public class NcscTeamDirectoryEntry
    {
        public Guid Id { get; set; }

        public int PsgcCodeRegion { get; set; }

        // Free text rather than an enum — mirrors the source roster's varied
        // role titles (Regional Director, PDO V-Plantilla, PDO III-COS,
        // ADAS I, CO-SPBD Regional Monitor, ...) without hardcoding them.
        public string? Position { get; set; }

        public string? FullName { get; set; }
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }

        // ── Bookkeeping ───────────────────────────────────────────────────
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
    }
}
