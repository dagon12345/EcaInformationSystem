namespace EcaInformationSystem.Domain.Entities
{
    // One row per possible-duplicate pair the user has taken action on. Pairs
    // are computed fresh on every scan (never stored), so a beneficiary GUID
    // pair is the only stable identity available — Record1Id/Record2Id here
    // are always stored normalized (lower GUID first) so a pair scanned as
    // (A, B) and later re-scanned as (B, A) still resolves to the same row.
    public class ResolvedDuplicatePair
    {
        public Guid Id { get; set; }

        public Guid Record1Id { get; set; }
        public Guid Record2Id { get; set; }

        public bool IsResolved { get; set; }
        public string? Remarks { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }

        public DateTime? UnresolvedAt { get; set; }
        public string? UnresolvedBy { get; set; }
    }
}
