using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    // Append-only log of every hand-off a DocumentBatch goes through — this is
    // what renders as "status and date relayed" on the batch card.
    public class DocumentTransfer
    {
        public Guid Id { get; set; }
        public Guid DocumentBatchId { get; set; }
        public DocumentBatch? DocumentBatch { get; set; }

        public DocumentTrackingStatus Status { get; set; }

        public Guid FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public Guid ToUserId { get; set; }
        public string ToUserName { get; set; } = string.Empty;

        public DateTime RelayedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public string? Note { get; set; }
    }
}
