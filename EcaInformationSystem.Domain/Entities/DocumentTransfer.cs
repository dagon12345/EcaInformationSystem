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

        // ── Batch/relay-level finding — separate from the Finance-only,
        // grantee-row-specific findings (DocumentGranteeRow.HasFinding). Any
        // role, at any relay stage, can optionally flag this transfer as a
        // finding (e.g. "missing attachment"). FindingJustification is its own
        // field, distinct from the general-purpose Note above, so a hand-off
        // note and the reason for a finding are never conflated. RaisedByRole
        // is captured automatically from the acting user's account role.
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
        public string? RaisedByRole { get; set; }
    }
}
