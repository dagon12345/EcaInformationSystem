namespace EcaInformationSystem.Domain.Entities
{
    public class BeneficiaryDocument
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public long FileSizeBytes { get; set; }
        public long OriginalFileSizeBytes { get; set; }
        public string ContentType { get; set; } = "application/pdf";
        public DateTime UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }

        public BeneficiaryInformation BeneficiaryInformation { get; set; } = default!;
    }
}