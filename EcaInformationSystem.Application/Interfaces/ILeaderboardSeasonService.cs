using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface ILeaderboardSeasonService
    {
        Task<LeaderboardSeasonInfoDto> GetActiveSeasonInfoAsync();

        // Bootstraps season #1 on first-ever call (preserving the original
        // 2026-08-12 leaderboard start date so this feature doesn't wipe
        // standings that already exist) if no season has been created yet.
        Task<(int SeasonNumber, DateTime StartedAtUtc)> GetOrCreateActiveSeasonAsync();

        // Ends the active season — snapshots every user's rank/count onto
        // their profile (LastSeasonNumber/Rank/TransactionCount), credits a
        // win to Top-3 finishers, then opens the next season. `resetBy` is
        // the SuperAdmin's full name for a manual reset, null for automatic.
        Task<LeaderboardResetResultDto> ResetSeasonAsync(string? resetBy, bool isAutomatic);
    }
}
