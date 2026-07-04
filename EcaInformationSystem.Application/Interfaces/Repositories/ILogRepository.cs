
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
    }
}
