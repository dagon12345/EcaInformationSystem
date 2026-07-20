using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class PasswordResetRequest
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        // 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Used (code redeemed)
        [Required]
        public int Status { get; set; } = 0;

        [Required]
        public DateTime RequestedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Remarks { get; set; }

        // ✅ Code is hashed at rest, same principle as MfaSecret — never store the
        // plain code once it's been shown to the admin.
        public string? CodeHash { get; set; }
        public DateTime? CodeExpiresAt { get; set; }
    }
}
