using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Shared.DTOs
{
    public class ImportBeneficiaryExcelSheetRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
