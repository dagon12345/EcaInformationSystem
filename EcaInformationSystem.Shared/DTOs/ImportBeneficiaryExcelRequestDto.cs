using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Shared.DTOs
{
    public class ImportBeneficiaryExcelRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
        [Required(ErrorMessage = "SheetName is required.")]
        public string SheetName { get; set; } = string.Empty;
    }
}
