namespace EcaInformationSystem.Shared.DTOs.DocumentTracking
{
    // Lean grid-row projection — no Description/Routes, unlike TrackedDocumentDto.
    public class TrackedDocumentListItemDto
    {
        public Guid Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Guid CurrentHolderUserId { get; set; }
        public string CurrentHolderName { get; set; } = string.Empty;
        public DateTime? CurrentLegAcceptedAt { get; set; }

        // Numeric TrackedDocumentStatus value + a ready-to-render label, so the
        // client doesn't need its own copy of the enum to display status text.
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
    }
}
