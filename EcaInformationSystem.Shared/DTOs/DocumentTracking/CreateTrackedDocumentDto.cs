namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    public class CreateTrackedDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Who this document is initially tagged to.
        public Guid RecipientUserId { get; set; }
        public string? Note { get; set; }
        public bool IsFinding { get; set; }
        public string? FindingJustification { get; set; }
    }
}
