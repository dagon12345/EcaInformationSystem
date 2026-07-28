using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IAnnualGranteeTargetRepository
    {
        Task<AnnualGranteeTarget?> GetAsync(int regionCode, int fiscalYear);

        Task<AnnualGranteeTarget> UpsertAsync(int regionCode, int fiscalYear, int[] quarterlyTargets, string userName);

        // Key = payroll quarter (1-4, the stored BeneficiaryPaymentHistory.PayrollQuarter
        // column — not derived from PaymentDate), value = count of DISTINCT beneficiaries
        // paid in that quarter, scoped to the given region/fiscal year. Quarters with zero
        // paid grantees are simply absent from the dictionary.
        Task<Dictionary<int, int>> GetQuarterlyPaidCountsAsync(int regionCode, int fiscalYear);
    }
}
