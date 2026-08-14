using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.Helpers;

namespace EcaInformationSystem.Application.Services
{
    public class UserTransactionTierService : IUserTransactionTierService
    {
        private readonly ILogRepository _logRepo;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly ILeaderboardSeasonService _seasonService;

        public UserTransactionTierService(
            ILogRepository logRepo,
            IPendingUserRegistrationRepository userRepo,
            ILeaderboardSeasonService seasonService)
        {
            _logRepo = logRepo;
            _userRepo = userRepo;
            _seasonService = seasonService;
        }

        public async Task<UserTransactionTierDto> GetTierAsync(string userName)
        {
            var (seasonNumber, seasonStartUtc) = await _seasonService.GetOrCreateActiveSeasonAsync();
            var count = await _logRepo.CountUserTransactionsAsync(userName, seasonStartUtc);
            var current = TransactionTierHelper.GetCurrentTier(count);
            var next = TransactionTierHelper.GetNextTier(count);

            var user = await _userRepo.GetByUserNameAsync(userName);

            return new UserTransactionTierDto
            {
                UserName = userName,
                TransactionCount = count,
                TierLevel = current.Level,
                TierName = current.Name,
                TierMinCount = current.MinCount,
                NextTierMinCount = next?.MinCount,
                NextTierName = next?.Name,
                ProgressPercent = TransactionTierHelper.GetProgressPercent(count),
                SeasonNumber = seasonNumber,
                NextAutoResetAtUtc = Common.WeeklyResetScheduleHelper.NextSundayElevenFiftyNinePmUtc(DateTime.UtcNow),
                TotalWins = user?.LeaderboardTotalWins ?? 0,
                LastSeasonNumber = user?.LastSeasonNumber,
                LastSeasonRank = user?.LastSeasonRank,
                LastSeasonTransactionCount = user?.LastSeasonTransactionCount,
                MotivationMessage = BuildMotivationMessage(user?.LastSeasonRank, user?.LastSeasonTransactionCount)
            };
        }

        // Powers the "Transaction Tier" card on someone ELSE's profile page —
        // reuses the same merged counts the leaderboard is built from so the
        // rank shown there always matches the leaderboard's #position.
        public async Task<UserTransactionTierDto?> GetTierByUserIdAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null) return null;

            var (seasonNumber, seasonStartUtc) = await _seasonService.GetOrCreateActiveSeasonAsync();
            var ranked = await GetMergedRankedCountsAsync(seasonStartUtc);
            var index = ranked.FindIndex(m => m.User.Id == userId);
            var rank = index >= 0 ? index + 1 : (int?)null;
            var count = index >= 0 ? ranked[index].Count : 0;

            var current = TransactionTierHelper.GetCurrentTier(count);
            var next = TransactionTierHelper.GetNextTier(count);

            return new UserTransactionTierDto
            {
                UserName = user.UserName,
                Rank = rank,
                TransactionCount = count,
                TierLevel = current.Level,
                TierName = current.Name,
                TierMinCount = current.MinCount,
                NextTierMinCount = next?.MinCount,
                NextTierName = next?.Name,
                ProgressPercent = TransactionTierHelper.GetProgressPercent(count),
                SeasonNumber = seasonNumber,
                NextAutoResetAtUtc = Common.WeeklyResetScheduleHelper.NextSundayElevenFiftyNinePmUtc(DateTime.UtcNow),
                TotalWins = user.LeaderboardTotalWins,
                LastSeasonNumber = user.LastSeasonNumber,
                LastSeasonRank = user.LastSeasonRank,
                LastSeasonTransactionCount = user.LastSeasonTransactionCount,
                MotivationMessage = BuildMotivationMessage(user.LastSeasonRank, user.LastSeasonTransactionCount)
            };
        }

        public async Task<List<UserLeaderboardEntryDto>> GetLeaderboardAsync(string requestingUserName, int top = 100)
        {
            var (_, seasonStartUtc) = await _seasonService.GetOrCreateActiveSeasonAsync();
            var ranked = await GetMergedRankedCountsAsync(seasonStartUtc);

            var rank = 0;
            return ranked
                .Take(top)
                .Select(m =>
                {
                    rank++;
                    var tier = TransactionTierHelper.GetCurrentTier(m.Count);

                    return new UserLeaderboardEntryDto
                    {
                        Rank = rank,
                        UserId = m.User.Id,
                        UserName = m.User.UserName,
                        DisplayName = string.IsNullOrWhiteSpace(m.User.FullName) ? m.User.UserName : m.User.FullName,
                        Position = m.User.Position,
                        TransactionCount = m.Count,
                        TierLevel = tier.Level,
                        TierName = tier.Name,
                        IsMe = string.Equals(m.User.UserName, requestingUserName, StringComparison.OrdinalIgnoreCase)
                    };
                })
                .ToList();
        }

        // Nudges the viewer toward the specific behavior this whole feature
        // exists to encourage — correcting/following-up on grantee records —
        // rather than a generic "do more" message. Tailored to last week's
        // placement (per request: Top 3 = win/celebrate, 4th-and-beyond still
        // gets an encouraging push rather than nothing).
        private static string BuildMotivationMessage(int? lastSeasonRank, int? lastSeasonTransactionCount)
        {
            if (lastSeasonRank is null)
            {
                return "No ranked activity last week. Correcting incomplete grantee info, following up on missing " +
                       "documents, or verifying pending records all count — start this week strong!";
            }

            var count = lastSeasonTransactionCount ?? 0;

            return lastSeasonRank switch
            {
                1 => $"🏆 You were #1 last week with {count:N0} transactions — great work! Keep correcting and following up on grantee records to defend the top spot this week.",
                2 or 3 => $"🥉 You placed #{lastSeasonRank} last week with {count:N0} transactions — so close! A few more corrections or follow-ups on grantee records this week could take you to #1.",
                _ => $"You placed #{lastSeasonRank} last week with {count:N0} transactions. Every grantee record you correct, verify, or follow up on counts toward this week's rank — keep going, the Top 3 is within reach!"
            };
        }

        // `Log.UserName` isn't a foreign key — it's a free-text column, and
        // different call sites across the codebase write different things into
        // it for the same person (their login UserName in most places, but
        // their FullName display string in a few, e.g. Document Tracking's
        // activity log). Grouping directly on that raw string, like the
        // repository-level query does, therefore risks splitting one person's
        // activity into two separate leaderboard rows — and any raw name that
        // matches no account at all (e.g. because that account was later
        // hard-deleted) would show up as an orphaned "ghost" entry.
        //
        // This resolves every raw (UserName, Count) pair to the actual account
        // it belongs to — trying the login UserName first, then falling back to
        // FullName (only when that FullName is unique across accounts, so two
        // different people who happen to share a display name never get merged
        // into each other) — and merges counts for the same account together.
        // Anything left unresolved belongs to no current account and is dropped
        // rather than shown as deleted-user noise.
        private async Task<List<(PendingUserRegistration User, int Count)>> GetMergedRankedCountsAsync(DateTime seasonStartUtc)
        {
            var counts = await _logRepo.GetTransactionCountsByUserAsync(seasonStartUtc);
            // Focal contacts get view/comment/like/share-only feed access and
            // aren't internal staff — excluded from the leaderboard entirely,
            // same exclusion LeaderboardSeasonService.GetRankedAccountsAsync applies.
            var users = (await _userRepo.GetAllAsync())
                .Where(u => u.Role != "Focal")
                .ToList();

            var byUserName = users.ToDictionary(u => u.UserName, u => u, StringComparer.OrdinalIgnoreCase);
            var byFullName = users
                .Where(u => !string.IsNullOrWhiteSpace(u.FullName))
                .GroupBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var merged = new Dictionary<Guid, (PendingUserRegistration User, int Count)>();

            foreach (var c in counts)
            {
                if (!byUserName.TryGetValue(c.UserName, out var user) &&
                    !byFullName.TryGetValue(c.UserName, out user))
                {
                    continue; // no current account matches this raw name — dropped
                }

                merged[user.Id] = merged.TryGetValue(user.Id, out var existing)
                    ? (user, existing.Count + c.Count)
                    : (user, c.Count);
            }

            return merged.Values.OrderByDescending(m => m.Count).ToList();
        }
    }
}
