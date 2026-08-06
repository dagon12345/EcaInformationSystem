using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IUserTransactionTierService
    {
        Task<UserTransactionTierDto> GetTierAsync(string userName);
        Task<UserTransactionTierDto?> GetTierByUserIdAsync(Guid userId);
        Task<List<UserLeaderboardEntryDto>> GetLeaderboardAsync(string requestingUserName, int top = 100);
    }
}
