using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface ILeaderboardSeasonRepository
    {
        // The one row with EndedAtUtc == null, or null if no season has ever
        // been created yet (first-ever call — caller bootstraps season #1).
        Task<LeaderboardSeason?> GetActiveSeasonAsync();
        Task<int> GetLatestSeasonNumberAsync(); // 0 if none exist yet
        Task AddAsync(LeaderboardSeason season);
        Task SaveChangesAsync();
    }
}
