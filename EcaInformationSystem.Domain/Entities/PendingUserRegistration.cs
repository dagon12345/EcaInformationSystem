using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class PendingUserRegistration
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [MaxLength(150)]
        public string Position { get; set; } = string.Empty;
        [Required]
        public DateTime BirthDate { get; set; }
        public int? Region { get; set; }
        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        [Required]
        public bool IsActivated { get; set; }
        [Required]
        public int ApprovalStatus { get; set; }
        [Required]
        public DateTime RequestedAt { get; set; }
        [Required]
        public DateTime ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? Remarks { get; set; }
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer";
        public DateTime? LastSeenAt { get; set; }
        public int FailedLoginCount { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public bool IsMfaEnabled { get; set; } = false;
        public string? MfaSecret { get; set; }
        public bool MfaSetupComplete { get; set; } = false;
        public bool MfaPromptShown { get; set; } = false;
        // Links this user to their ZKTeco device PIN (AttendanceLog.BiometricUserId) for DTR generation.
        [MaxLength(50)]
        public string? BiometricUserId { get; set; }
        // Disables login without discarding approval history/role — used when
        // an employee resigns or is otherwise offboarded; reversible.
        public bool IsDeactivated { get; set; } = false;
        public DateTime? DeactivatedAt { get; set; }
        public string? DeactivatedBy { get; set; }
        public ICollection<PdoJurisdiction> Jurisdictions {get; set;}
            = new List<PdoJurisdiction>();
    }
}
