namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    public class TrackedDocumentDto
    {
        public Guid Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;

        public List<DocumentRouteDto> Routes { get; set; } = new();
    }
}
