using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IAnnualGranteeTargetRepository
    {
        Task<AnnualGranteeTarget?> GetAsync(int regionCode, int fiscalYear);

        Task<AnnualGranteeTarget> UpsertAsync(int regionCode, int fiscalYear, int[] monthlyTargets, string userName);

        // Key = calendar month (1-12) of PaymentHistory.PaymentDate, value = count of
        // DISTINCT beneficiaries paid that month, scoped to the given region/year.
        // Months with zero paid grantees are simply absent from the dictionary.
        Task<Dictionary<int, int>> GetMonthlyPaidCountsAsync(int regionCode, int fiscalYear);
    }
}
