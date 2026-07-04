using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Application.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        public string Position { get; set; } = string.Empty;
        [Required]
        public DateTime BirthDate { get; set; }
        public int? Region { get; set; } // PSGC region code
        [Required]
        public string UserName { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer";

    }
}
