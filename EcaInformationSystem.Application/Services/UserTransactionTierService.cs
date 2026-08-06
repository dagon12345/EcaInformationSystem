using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.Helpers;

namespace EcaInformationSystem.Application.Services
{
    public class UserTransactionTierService : IUserTransactionTierService
    {
        private readonly ILogRepository _logRepo;
        private readonly IPendingUserRegistrationRepository _userRepo;

        public UserTransactionTierService(ILogRepository logRepo, IPendingUserRegistrationRepository userRepo)
        {
            _logRepo = logRepo;
            _userRepo = userRepo;
        }

        public async Task<UserTransactionTierDto> GetTierAsync(string userName)
        {
            var count = await _logRepo.CountUserTransactionsAsync(userName);
            var current = TransactionTierHelper.GetCurrentTier(count);
            var next = TransactionTierHelper.GetNextTier(count);

            return new UserTransactionTierDto
            {
                UserName = userName,
                TransactionCount = count,
                TierLevel = current.Level,
                TierName = current.Name,
                TierMinCount = current.MinCount,
                NextTierMinCount = next?.MinCount,
                NextTierName = next?.Name,
                ProgressPercent = TransactionTierHelper.GetProgressPercent(count)
            };
        }

        // Powers the "Transaction Tier" card on someone ELSE's profile page —
        // reuses the same ranked counts the leaderboard is built from so the
        // rank shown there always matches the leaderboard's #position.
        public async Task<UserTransactionTierDto?> GetTierByUserIdAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null) return null;

            var counts = await _logRepo.GetTransactionCountsByUserAsync();
            var index = counts.FindIndex(c => string.Equals(c.UserName, user.UserName, StringComparison.OrdinalIgnoreCase));
            var rank = index >= 0 ? index + 1 : (int?)null;
            var count = index >= 0 ? counts[index].Count : 0;

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
                ProgressPercent = TransactionTierHelper.GetProgressPercent(count)
            };
        }

        public async Task<List<UserLeaderboardEntryDto>> GetLeaderboardAsync(string requestingUserName, int top = 100)
        {
            var counts = await _logRepo.GetTransactionCountsByUserAsync();
            var users = await _userRepo.GetAllAsync();
            var userLookup = users.ToDictionary(u => u.UserName, u => u, StringComparer.OrdinalIgnoreCase);

            var rank = 0;
            return counts
                .Take(top)
                .Select(c =>
                {
                    rank++;
                    userLookup.TryGetValue(c.UserName, out var user);
                    var tier = TransactionTierHelper.GetCurrentTier(c.Count);

                    return new UserLeaderboardEntryDto
                    {
                        Rank = rank,
                        UserId = user?.Id,
                        UserName = c.UserName,
                        DisplayName = user?.FullName ?? c.UserName,
                        Position = user?.Position,
                        TransactionCount = c.Count,
                        TierLevel = tier.Level,
                        TierName = tier.Name,
                        IsMe = string.Equals(c.UserName, requestingUserName, StringComparison.OrdinalIgnoreCase)
                    };
                })
                .ToList();
        }
    }
}
