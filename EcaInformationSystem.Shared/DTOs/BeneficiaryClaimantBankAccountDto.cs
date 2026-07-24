namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryClaimantBankAccountDto
    {
        public Guid? Id { get; set; }
        public int PreferredChannel { get; set; } = 0;
        public string? AccountNumber { get; set; }
        public string? MobileNumber { get; set; } // 11-digit PH mobile number, e.g. 09171234567 — GCash Mobile Number
        public string? BankOrWalletName { get; set; } // Bank Name
        public string? GCashName { get; set; } // Registered GCash account name, separate from BankOrWalletName
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
        public bool? IsJointAccount { get; set; }
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}