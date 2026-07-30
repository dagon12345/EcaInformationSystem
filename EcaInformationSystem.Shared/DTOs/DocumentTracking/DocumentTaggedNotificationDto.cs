namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Pushed over DocumentTrackingHub ("DocumentTagged" event) to whoever a
    // batch was just tagged/relayed to, so they know they have something to
    // accept without having to keep checking the page.
    public class DocumentTaggedNotificationDto
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
