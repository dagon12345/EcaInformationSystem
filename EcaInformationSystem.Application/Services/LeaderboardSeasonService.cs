using EcaInformationSystem.Application.Common;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class LeaderboardSeasonService : ILeaderboardSeasonService
    {
        // The original leaderboard-start date (see the now-removed
        // LogRepository.TransactionCountingStartUtc constant this replaces).
        // Used only to seed Season #1 so standings already earned since that
        // date carry over instead of being wiped by this feature shipping.
        private static readonly DateTime OriginalLeaderboardStartUtc = new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

        private readonly ILeaderboardSeasonRepository _seasonRepo;
        private readonly ILogRepository _logRepo;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IPostService _postService;

        public LeaderboardSeasonService(
            ILeaderboardSeasonRepository seasonRepo,
            ILogRepository logRepo,
            IPendingUserRegistrationRepository userRepo,
            IPostService postService)
        {
            _seasonRepo = seasonRepo;
            _logRepo = logRepo;
            _userRepo = userRepo;
            _postService = postService;
        }

        public async Task<(int SeasonNumber, DateTime StartedAtUtc)> GetOrCreateActiveSeasonAsync()
        {
            var active = await _seasonRepo.GetActiveSeasonAsync();
            if (active is not null)
                return (active.SeasonNumber, active.StartedAtUtc);

            var season = new LeaderboardSeason
            {
                Id = Guid.NewGuid(),
                SeasonNumber = 1,
                StartedAtUtc = OriginalLeaderboardStartUtc,
                EndedAtUtc = null,
                ResetType = "Automatic",
                ResetBy = null
            };
            await _seasonRepo.AddAsync(season);
            await _seasonRepo.SaveChangesAsync();

            return (season.SeasonNumber, season.StartedAtUtc);
        }

        public async Task<LeaderboardSeasonInfoDto> GetActiveSeasonInfoAsync()
        {
            var (seasonNumber, startedAtUtc) = await GetOrCreateActiveSeasonAsync();

            return new LeaderboardSeasonInfoDto
            {
                SeasonNumber = seasonNumber,
                StartedAtUtc = startedAtUtc,
                NextAutoResetAtUtc = WeeklyResetScheduleHelper.NextSundayElevenFiftyNinePmUtc(DateTime.UtcNow)
            };
        }

        public async Task<LeaderboardResetResultDto> ResetSeasonAsync(string? resetBy, bool isAutomatic)
        {
            // Ensures an active season exists (bootstraps if this is the very
            // first reset ever) rather than assuming one is already there.
            await GetOrCreateActiveSeasonAsync();
            var active = await _seasonRepo.GetActiveSeasonAsync()
                ?? throw new InvalidOperationException("Failed to resolve the active leaderboard season.");

            var ranked = await GetRankedAccountsAsync(active.StartedAtUtc);
            var rankByUserId = ranked
                .Select((entry, index) => (entry.User, entry.Count, Rank: index + 1))
                .ToDictionary(x => x.User.Id, x => (x.Rank, x.Count));

            // Every account gets its LastSeason* fields refreshed this reset —
            // including accounts with zero qualifying activity, so a profile
            // doesn't keep showing a stale rank from two seasons ago.
            var allUsers = await _userRepo.GetAllAsync();
            foreach (var user in allUsers)
            {
                user.LastSeasonNumber = active.SeasonNumber;

                if (rankByUserId.TryGetValue(user.Id, out var result))
                {
                    user.LastSeasonRank = result.Rank;
                    user.LastSeasonTransactionCount = result.Count;
                    if (result.Rank <= 3)
                        user.LeaderboardTotalWins += 1;
                }
                else
                {
                    user.LastSeasonRank = null;
                    user.LastSeasonTransactionCount = 0;
                }
            }

            var now = DateTime.UtcNow;
            active.EndedAtUtc = now;
            active.ResetType = isAutomatic ? "Automatic" : "Manual";
            active.ResetBy = isAutomatic ? null : resetBy;

            var newSeason = new LeaderboardSeason
            {
                Id = Guid.NewGuid(),
                SeasonNumber = active.SeasonNumber + 1,
                StartedAtUtc = now,
                EndedAtUtc = null,
                ResetType = "Automatic",
                ResetBy = null
            };
            await _seasonRepo.AddAsync(newSeason);

            // Single SaveChanges — the modified `active` season, every modified
            // user, and the new season row all live on the same DbContext
            // instance behind these two repositories, so one flush persists all of it.
            await _seasonRepo.SaveChangesAsync();

            var topThree = ranked
                .Take(3)
                .Select((entry, index) => new LeaderboardTopFinisherDto
                {
                    Rank = index + 1,
                    UserId = entry.User.Id,
                    DisplayName = string.IsNullOrWhiteSpace(entry.User.FullName) ? entry.User.UserName : entry.User.FullName,
                    TransactionCount = entry.Count
                })
                .ToList();

            // Announce the outcome in the feed — skipped when nobody had any
            // qualifying activity this season (nothing worth celebrating).
            PostDto? podiumPost = topThree.Count > 0
                ? await _postService.CreateLeaderboardPodiumPostAsync(active.SeasonNumber, topThree)
                : null;

            return new LeaderboardResetResultDto
            {
                EndedSeasonNumber = active.SeasonNumber,
                NewSeasonNumber = newSeason.SeasonNumber,
                ResetType = active.ResetType,
                ResetBy = active.ResetBy,
                ResetAtUtc = now,
                TopThree = topThree,
                PodiumPost = podiumPost
            };
        }

        // Same raw-name → account resolution/merge as
        // UserTransactionTierService.GetMergedRankedCountsAsync, duplicated
        // here (rather than shared) since this service also needs to iterate
        // ALL accounts afterward — not just the ranked ones — to clear stale
        // LastSeason* fields on zero-activity accounts.
        private async Task<List<(PendingUserRegistration User, int Count)>> GetRankedAccountsAsync(DateTime seasonStartUtc)
        {
            var counts = await _logRepo.GetTransactionCountsByUserAsync(seasonStartUtc);
            // Focal contacts get view/comment/like/share-only feed access and
            // aren't internal staff — they're excluded from the leaderboard
            // entirely, same as this reset already excludes zero-activity noise.
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
                    continue;
                }

                merged[user.Id] = merged.TryGetValue(user.Id, out var existing)
                    ? (user, existing.Count + c.Count)
                    : (user, c.Count);
            }

            return merged.Values.OrderByDescending(m => m.Count).ToList();
        }
    }
}
