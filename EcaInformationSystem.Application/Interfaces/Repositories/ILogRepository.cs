
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface ILogRepository
    {
        Task AddAsync(Log log);
        Task AddRangeAsync(IEnumerable<Log> logs);
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
        Task<(List<LogEntryDto> Items, int TotalCount)> GetAllLogsAsync(LogFilterDto filter);
        Task<int> CountAllAsync();
        Task SaveChangesAsync(); // ✅ NEW
        Task<int> CountUserTransactionsAsync(string userName, DateTime seasonStartUtc);
        Task<List<(string UserName, int Count)>> GetTransactionCountsByUserAsync(DateTime seasonStartUtc);

        // ✅ NEW — activity breakdown (logins, data created/edited, documents
        // tracked) + the raw set of days this user had any qualifying
        // transaction, for the profile page's stat row and streak badge.
        Task<UserActivityStatsDto> GetUserActivityStatsAsync(string userName, string? fullName, DateTime seasonStartUtc);

        // ✅ NEW — same "which raw Log.UserName had activity on which day"
        // shape as GetTransactionCountsByUserAsync, so the leaderboard can
        // compute each row's streak with the same account-resolution/merge
        // logic it already uses for transaction counts.
        Task<List<(string UserName, DateTime Date)>> GetActiveDatesByUserAsync(DateTime seasonStartUtc);
    }
}
