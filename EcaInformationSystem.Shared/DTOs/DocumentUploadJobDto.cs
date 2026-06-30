namespace EcaInformationSystem.Shared.DTOs
{
    public enum UploadJobStatus
    {
        Queued,
        Uploading,
        Retrying,
        Done,
        Failed  // only for 4xx — permanent failure
    }

    public class DocumentUploadJobDto
    {
        public Guid JobId { get; set; } = Guid.NewGuid();
        public Guid BeneficiaryId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public UploadJobStatus Status { get; set; } = UploadJobStatus.Queued;
        public int AttemptCount { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime QueuedAt { get; set; } = DateTime.Now;
        public bool IsCamera { get; set; } // true = upload-from-camera, false = regular PDF upload

        // ── Payload — held in memory for the lifetime of the tab ──────────
        // For camera jobs: list of (bytes, filename, contentType) per photo
        // For PDF jobs: list of (bytes, filename) per file
        public List<UploadFilePayload> Files { get; set; } = new();
    }
    public class UploadFilePayload
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
