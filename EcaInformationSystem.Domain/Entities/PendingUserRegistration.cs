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

        public ICollection<PdoJurisdiction> Jurisdictions {get; set;}
            = new List<PdoJurisdiction>();
    }
}
