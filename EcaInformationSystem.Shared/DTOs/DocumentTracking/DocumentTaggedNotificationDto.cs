namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Pushed over a DocumentTracking hub ("DocumentTagged" event, wired up in a
    // later step) to whoever a document was just tagged/relayed to, so they know
    // they have something to accept without having to keep checking the page.
    public class DocumentTaggedNotificationDto
    {
        public Guid DocumentId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string TaggedByName { get; set; } = string.Empty;
        public DateTime RelayedAt { get; set; }
    }
}
