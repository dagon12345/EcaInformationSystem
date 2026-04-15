using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class Log
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string Activity { get; set; } = string.Empty;
        [Required]
        public string UserName { get; set; } = string.Empty;
        [Required]
        public DateTime CreatedAt { get; set; }
        [Required]
        public Guid BeneficiaryInformationId { get; set; }
    }
}
