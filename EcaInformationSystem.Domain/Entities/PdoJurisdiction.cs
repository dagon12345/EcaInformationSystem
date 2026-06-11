using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class PdoJurisdiction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public int PsgcCodeMunicipality { get; set; }
        [MaxLength(200)]
        public string MunicipalityName { get; set; } = string.Empty;
        [MaxLength(200)]
        public string ProvinceName { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        [MaxLength(200)]
        public string AssignedBy { get; set; } = string.Empty;

        public PendingUserRegistration User { get; set; } = default!;
    }
}