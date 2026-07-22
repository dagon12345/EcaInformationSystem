// Shared/DTOs/BeneficiaryClaimantDto.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryClaimantDto
    {
        public Guid? Id { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public string? ContactNumber { get; set; }
        public string? RelationshipToDeceased { get; set; }
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? Barangay { get; set; }
        public string? CityMunicipality { get; set; }
        public string? Province { get; set; }
        public string? ZipCode { get; set; }
    }
}