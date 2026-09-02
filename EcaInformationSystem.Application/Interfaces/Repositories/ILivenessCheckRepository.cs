using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface ILivenessCheckRepository
    {
        Task AddAsync(LivenessCheckRecord record);
        Task DeleteAsync(LivenessCheckRecord record);
        Task<LivenessCheckRecord?> GetByIdAsync(Guid id);
        Task<LivenessCheckRecord?> GetByTokenAsync(string token);
        // Only one liveness check record ever exists per beneficiary — see
        // LivenessCheckService.GenerateLinkAsync.
        Task<LivenessCheckRecord?> GetByBeneficiaryIdAsync(Guid beneficiaryId);
        Task<List<LivenessCheckRecord>> GetHistoryByBeneficiaryIdAsync(Guid beneficiaryId);
        Task<List<(LivenessCheckRecord Record, BeneficiaryInformation Beneficiary)>> GetSubmittedForReviewAsync(List<int>? allowedMunicipalityCodes);
        Task<BeneficiaryInformation?> GetBeneficiaryAsync(Guid beneficiaryId);
        Task SaveChangesAsync();
    }
}
