namespace EcaInformationSystem.Shared.DTOs
{
    // Metadata only — used for the listing grid. Never carries FileData.
    public class FormDocumentDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class FormDocumentUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
    }
}
