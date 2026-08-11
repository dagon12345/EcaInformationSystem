namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Generic payload for the deterministic relay actions (return-to-viewer,
    // distribute-to-pdo, endorse-to-finance, forward-to-viewer-scanning).
    // ToUserId is ignored for return-to-viewer (it always targets the batch's
    // original creator).
    public class RelayDocumentDto
    {
        public Guid ToUserId { get; set; }
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }

    // Finance's "send back to PDO for findings" action — flags specific
    // grantee rows in the grouped list as having a finding.
    public class ReturnForFindingsDto
    {
        public Guid ToUserId { get; set; }
        public string? Note { get; set; }
        public List<Guid> GranteeRowIds { get; set; } = new();
    }

    // Used by actions that don't need a ToUserId (return-to-viewer, complete).
    public class RelayNoteDto
    {
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }

    // Admin/SuperAdmin — correct a typo in an existing relay history entry's
    // Note/finding, without disturbing who it was sent to/from or the batch's
    // workflow stage.
    public class UpdateTransferNoteDto
    {
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }
}
