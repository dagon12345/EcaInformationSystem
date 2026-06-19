using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Shared.DTOs
{
    public class UpdateBeneficiaryExcelRequestDto
    {
        public IFormFile? File { get; set; }
        public string? SheetName { get; set; }
    }
}
