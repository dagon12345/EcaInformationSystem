namespace EcaInformationSystem.Domain.Entities
{
    public class FormDocument
    {
        public Guid Id { get; set; }
        // ✅ NEW — null means "Uncategorized" / root level
        public Guid? FolderId { get; set; }
        public FormFolder? Folder { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }          // e.g. "COE", "Liquidation", "Payroll"

        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
