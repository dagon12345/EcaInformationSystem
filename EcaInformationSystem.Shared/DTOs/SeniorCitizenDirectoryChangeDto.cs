namespace EcaInformationSystem.Shared.DTOs
{
    // Broadcast over SignalR whenever a row is added/edited/deleted, so every
    // connected viewer's grid updates live without needing a manual reload.
    public class SeniorCitizenDirectoryChangeDto
    {
        public Guid Id { get; set; }
        public string ChangeType { get; set; } = string.Empty; // "Created" | "Updated" | "Deleted"
        public SeniorCitizenDirectoryListItemDto? Entry { get; set; } // null for Deleted
        public string ChangedBy { get; set; } = string.Empty;
    }
}
