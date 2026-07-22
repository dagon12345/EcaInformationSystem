// Shared/DTOs/BeneficiaryBankAccountDto.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryBankAccountDto
    {
        public Guid? Id { get; set; } // null until first saved
        public int PreferredChannel { get; set; } = 0; // 0 = Not Set, 1=Landbank, 2=OtherBank, 3=EMI, 4=PSP
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