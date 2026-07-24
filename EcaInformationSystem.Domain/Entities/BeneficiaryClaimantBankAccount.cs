namespace EcaInformationSystem.Domain.Entities
{
    // 1:1 with BeneficiaryInformation, only populated when the grantee is
    // deceased. Independent from BeneficiaryBankAccount (the grantee's own
    // account) — the claimant is paid into their own account, not the
    // deceased grantee's.
    public class BeneficiaryClaimantBankAccount
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public int PreferredChannel { get; set; }
        public string? AccountNumber { get; set; }
        public string? MobileNumber { get; set; }
        public string? BankOrWalletName { get; set; }
        public string? GCashName { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
        public bool? IsJointAccount { get; set; }
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }

        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}