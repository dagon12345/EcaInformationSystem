namespace EcaInformationSystem.Shared.DTOs
{
    // Lightweight grantee entry for the "select replacement grantee" picker
    // in the Replacement Status flow — avoids pulling the full list DTO.
    public class BeneficiaryLookupDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? BatchCode { get; set; }
        public string? MunicipalityName { get; set; }
        public int? ReplacementStatus { get; set; }
    }
}
