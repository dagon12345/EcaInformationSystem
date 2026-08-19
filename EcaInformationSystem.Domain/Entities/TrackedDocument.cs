using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    // A single generic tracked document — no batch/rows concept, unlike
    // ApplicationBatch. It is handed from person to person via free-form routing
    // (any current holder can relay to any other user, not a fixed pipeline of
    // stages); every hand-off is appended to Routes.
    public class TrackedDocument
    {
        public Guid Id { get; set; }

        // Auto-generated, unique, human-searchable lookup code, e.g. "DT-20260818-000042".
        public string SerialNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Whoever must act next. CurrentLegAcceptedAt is null while the current
        // leg is pending acceptance — only CurrentHolderUserId can accept it, and
        // only after accepting can they relay, return, or complete the document.
        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public TrackedDocumentStatus Status { get; set; } = TrackedDocumentStatus.InTransit;

        public ICollection<DocumentRoute> Routes { get; set; } = new List<DocumentRoute>();
    }
}
