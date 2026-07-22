// Shared/DTOs/BeneficiaryAbroadAddressDto.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryAbroadAddressDto
    {
        public Guid? Id { get; set; }
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
    }
}