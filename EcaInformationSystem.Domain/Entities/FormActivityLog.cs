namespace EcaInformationSystem.Domain.Entities
{
    public class FormActivityLog
    {
        public Guid Id { get; set; }

        // Both nullable — a folder-delete log has no FormDocumentId, a file-delete
        // log may still reference the (now-deleted) FolderId for context.
        public Guid? FolderId { get; set; }
        public Guid? FormDocumentId { get; set; }

        public string Action { get; set; } = string.Empty;      // e.g. "FolderCreated", "FolderDeleted", "FormDeleted"
        public string TargetName { get; set; } = string.Empty;  // folder/file name at time of action — survives deletion
        public string? Details { get; set; }

        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}