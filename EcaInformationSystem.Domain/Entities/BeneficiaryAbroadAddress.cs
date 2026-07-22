// Domain/Entities/BeneficiaryAbroadAddress.cs
namespace EcaInformationSystem.Domain.Entities
{
    // 1:1. Purely additive detail alongside the local PSGC-coded address —
    // only populated/shown when PlaceOfSubmission == Abroad.
    public class BeneficiaryAbroadAddress
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
    }
}