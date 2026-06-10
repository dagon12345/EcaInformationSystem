namespace EcaInformationService.Shared.DTOs
{
    public class BeneficiaryDocumentDto
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public long OriginalFileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;

        // ✅ Computed display fields — set by service, used by razor
        public string? FileSizeDisplay { get; set; }
        public string? CompressionDisplay { get; set; }

        public string OriginalSizeDisplay => OriginalFileSizeBytes switch
        {
            < 1024 => $"{OriginalFileSizeBytes} B",
            < 1048576 => $"{OriginalFileSizeBytes / 1024.0:F1} KB",
            _ => $"{OriginalFileSizeBytes / 1048576.0:F1} MB"
        };
    }
}