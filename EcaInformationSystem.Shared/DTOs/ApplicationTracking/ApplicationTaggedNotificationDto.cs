namespace EcaInformationSystem.Shared.DTOs.ApplicationTracking
{
    // Pushed over ApplicationTrackingHub ("ApplicationTagged" event) to whoever a
    // batch was just tagged/relayed to, so they know they have something to
    // accept without having to keep checking the page.
    public class ApplicationTaggedNotificationDto
    {
        public Guid BatchId { get; set; }
        public string ProvinceName { get; set; } = string.Empty;
        public string MunicipalityName { get; set; } = string.Empty;
        public int MilestoneYear { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string TaggedByName { get; set; } = string.Empty;
        public DateTime RelayedAt { get; set; }
    }
}
