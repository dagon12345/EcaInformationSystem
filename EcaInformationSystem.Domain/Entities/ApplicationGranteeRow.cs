namespace EcaInformationSystem.Domain.Entities
{
    // A single grantee's document inside an ApplicationBatch. Names are freeform —
    // not linked to BeneficiaryInformation — since this tracks the physical
    // document, not the grantee record itself.
    public class ApplicationGranteeRow
    {
        public Guid Id { get; set; }
        public Guid ApplicationBatchId { get; set; }
        public ApplicationBatch? ApplicationBatch { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Extension { get; set; }

        public int SortOrder { get; set; }

        // Set by Finance when returning the batch to a PDO for findings —
        // flags exactly which grantee(s) in the grouped list have a finding,
        // independent of the batch's overall relay status.
        public bool HasFinding { get; set; }
        public string? FindingNote { get; set; }
        public DateTime? FindingSetAt { get; set; }
        public string? FindingSetByName { get; set; }
        public DateTime? FindingResolvedAt { get; set; }

        // ── Eligibility — set by the PDO responsible for this grantee's
        // municipality (see JurisdictionGuardService), only while the batch
        // card is open. Null until a PDO has actually gone through it —
        // shown as "-" in the UI, never assumed true or false by default.
        public bool? IsEligible { get; set; }
        public DateTime? Birthdate { get; set; }
        public string? Sex { get; set; }
        // Compulsory whenever IsEligible is explicitly false — enforced in
        // ApplicationTrackingService.UpdateEligibilityAsync, not just the UI.
        public string? IneligibilityReason { get; set; }
    }
}
