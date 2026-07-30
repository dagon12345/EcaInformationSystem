using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    // Header of a grouped "documents that arrived" entry — Province/Municipality/
    // Milestone Year/Date Received identify the batch; the actual grantee documents
    // live in Rows, and every hand-off between roles is appended to Transfers.
    public class DocumentBatch
    {
        public Guid Id { get; set; }

        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public DocumentTrackingStatus CurrentStatus { get; set; } = DocumentTrackingStatus.EndorsedByViewer;

        // Whoever must act next. AcceptedAt is null while the leg is pending
        // acceptance — only CurrentHolderUserId can accept it, and only after
        // accepting can they advance the batch to its next leg.
        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public ICollection<DocumentGranteeRow> Rows { get; set; } = new List<DocumentGranteeRow>();
        public ICollection<DocumentTransfer> Transfers { get; set; } = new List<DocumentTransfer>();
    }
}
