using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface ILogRepository
    {
        Task AddAsync(Log log);
        Task AddRangeAsync(IEnumerable<Log> logs);
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
    }
}
