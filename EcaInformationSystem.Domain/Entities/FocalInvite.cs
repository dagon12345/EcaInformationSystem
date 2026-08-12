using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // A PDO-initiated invite for an external partner-LGU "focal" contact —
    // the PDO pre-fills who they are and which of their own jurisdiction
    // municipalities the focal covers; the focal only ever sets a username
    // and password. Same lifecycle shape as PasswordResetRequest.
    public class FocalInvite
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid InvitedByUserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ContactNote { get; set; }

        // One-way SHA-256 hash of the invite code (looked up by re-hashing
        // whatever the accept page submits) — never store the plain code.
        // Unlike PasswordResetRequest.CodeHash there's no need to ever
        // redisplay it, so a reversible IDataProtector isn't needed here.
        [Required]
        public string CodeHash { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        // 0 = Pending, 1 = Accepted, 2 = Revoked, 3 = Expired
        [Required]
        public int Status { get; set; } = 0;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }
        public Guid? ResultingUserId { get; set; }

        public ICollection<FocalInviteJurisdiction> Jurisdictions { get; set; } = new List<FocalInviteJurisdiction>();
    }
}
