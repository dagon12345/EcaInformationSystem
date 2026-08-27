namespace EcaInformationSystem.Shared.DTOs
{
    // Result of a PDO/Admin confirming the "turned 80 + Filipino" auto-eligibility
    // flip for a batch of candidate grantees. Every id is re-validated server-side
    // (never trusted from the client) and lands in exactly one bucket:
    // Updated (flipped to Eligible), SkippedOutsideJurisdiction (a PDO tried to
    // confirm a record outside their assigned municipalities), or
    // SkippedNoLongerValid (criteria no longer match — e.g. someone else already
    // resolved it, or the birth date/citizenship changed since it was detected).
    public class ConfirmAutoEligibilityResultDto
    {
        public List<Guid> UpdatedIds { get; set; } = new();
        public List<Guid> SkippedOutsideJurisdictionIds { get; set; } = new();
        public List<Guid> SkippedNoLongerValidIds { get; set; } = new();

        // Kept for anything that consumes the flat "skipped" list without
        // caring why (e.g. removing resolved rows from a pending-review list).
        public List<Guid> SkippedIds => SkippedOutsideJurisdictionIds.Concat(SkippedNoLongerValidIds).ToList();
    }
}
