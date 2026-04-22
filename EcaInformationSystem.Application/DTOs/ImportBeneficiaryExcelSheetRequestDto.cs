using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Application.DTOs
{
    public class ImportBeneficiaryExcelSheetRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
