namespace EcaInformationSystem.Domain.Entities
{
    public enum LivenessCheckStatus
    {
        Pending = 0,
        Submitted = 1,
        Verified = 2,
        Rejected = 3
    }

    // One row per generated liveness link/attempt. History is kept across
    // regenerations (IsActive marks the current live one) so PDOs can see
    // prior rejected/expired attempts for a grantee.
    public class LivenessCheckRecord
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public LivenessCheckStatus Status { get; set; }
        public string GeneratedByUserId { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public byte[]? PhotoData { get; set; }
        public string? PhotoContentType { get; set; }
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? ReviewNotes { get; set; }

        public BeneficiaryInformation BeneficiaryInformation { get; set; } = default!;
    }
}
