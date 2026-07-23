namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryClaimantBankAccountDto
    {
        public Guid? Id { get; set; }
        public int PreferredChannel { get; set; } = 0;
        public string? AccountNumber { get; set; }
        public string? BankOrWalletName { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
        public bool? IsJointAccount { get; set; }
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}