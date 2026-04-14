namespace EcaInformationSystem.Application.DTOs
{
    public class BeneficiaryImportErrorRowDto
    {
        public List<string> Values { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
