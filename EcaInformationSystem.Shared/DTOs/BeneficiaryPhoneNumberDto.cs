namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryPhoneNumberDto
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
