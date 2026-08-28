using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    // Header of a grouped "documents that arrived" entry — Province/Municipality/
    // Milestone Year/Date Received identify the batch; the actual grantee documents
    // live in Rows, and every hand-off between roles is appended to Transfers.
    public class ApplicationBatch
    {
        public Guid Id { get; set; }

        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int MilestoneYear { get; set; }
        public DateTime DateReceived { get; set; }

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        // Captured from the creator's account role at logging time — Viewer,
        // PDO, Admin, or SuperAdmin can all log a batch now, so "Returned to
        // Viewer"-style labels need this to say who the batch actually goes
        // back to instead of assuming Viewer.
        public string? CreatedByRole { get; set; }
        public DateTime CreatedAt { get; set; }

        public ApplicationTrackingStatus CurrentStatus { get; set; } = ApplicationTrackingStatus.EndorsedByViewer;

        public ApplicationPriority Priority { get; set; } = ApplicationPriority.Normal;

        // Whoever must act next. AcceptedAt is null while the leg is pending
        // acceptance — only CurrentHolderUserId can accept it, and only after
        // accepting can they advance the batch to its next leg.
        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public ICollection<ApplicationGranteeRow> Rows { get; set; } = new List<ApplicationGranteeRow>();
        public ICollection<ApplicationTransfer> Transfers { get; set; } = new List<ApplicationTransfer>();
    }
}
