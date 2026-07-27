using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAnnualGranteeTargetService
    {
        Task<AnnualTargetComparisonDto> GetComparisonAsync(int regionCode, int fiscalYear);

        Task<AnnualGranteeTargetDto> UpsertAsync(int regionCode, int fiscalYear, int[] monthlyTargets, string userName);
    }
}
