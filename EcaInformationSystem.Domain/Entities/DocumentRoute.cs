using EcaInformationSystem.Domain.Common.Enum;

namespace EcaInformationSystem.Domain.Entities
{
    // Append-only log of every hand-off a TrackedDocument goes through — this is
    // what renders as its routing/relay history.
    public class DocumentRoute
    {
        public Guid Id { get; set; }
        public Guid TrackedDocumentId { get; set; }
        public TrackedDocument? TrackedDocument { get; set; }

        public DocumentRouteAction Action { get; set; }

        public Guid FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public Guid ToUserId { get; set; }
        public string ToUserName { get; set; } = string.Empty;

        public DateTime RelayedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public string? Note { get; set; }

        // Any user, on any hand-off, can optionally flag this route entry as a
        // finding (e.g. "missing attachment"). FindingJustification is its own
        // field, distinct from the general-purpose Note above, so a hand-off note
        // and the reason for a finding are never conflated. RaisedByRole is
        // captured automatically from the acting user's account role.
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
        public string? RaisedByRole { get; set; }
    }
}
