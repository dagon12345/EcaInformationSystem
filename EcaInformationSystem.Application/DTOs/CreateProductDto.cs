using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Application.DTOs
{
    public class CreateProductDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }
    }
}
