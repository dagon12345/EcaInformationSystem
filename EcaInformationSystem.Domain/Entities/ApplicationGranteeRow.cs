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
    }
}
