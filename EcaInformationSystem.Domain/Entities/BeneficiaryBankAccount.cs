// Domain/Entities/BeneficiaryBankAccount.cs
namespace EcaInformationSystem.Domain.Entities
{
    // 1:1 with BeneficiaryInformation. Doubles as the claimant's payout
    // account when the grantee is deceased — no separate claimant bank table.
    public class BeneficiaryBankAccount
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public BeneficiaryInformation Beneficiary { get; set; } = default!;

        public int PreferredChannel { get; set; }
        // 1 = Landbank, 2 = Other Bank, 3 = EMI, 4 = PSP (Palawan Pawnshop)

        public string? AccountNumber { get; set; }
        public string? BankOrWalletName { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
        public bool? IsJointAccount { get; set; }

        // Abroad-only fields
        public string? SwiftCode { get; set; }
        public string? Iban { get; set; }

        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }
}