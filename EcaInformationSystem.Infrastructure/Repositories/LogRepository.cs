using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class LogRepository : ILogRepository
    {
        private readonly AppDbContext _context;
        public LogRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Log log)
        {
            await _context.Logs.AddAsync(log);
        }

        public async Task AddRangeAsync(IEnumerable<Log> logs)
        {
            await _context.Logs.AddRangeAsync(logs);
        }

        public async Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId)
        {
            var query = from log in _context.Logs

                        join b in _context.BeneficiaryInformations on log.BeneficiaryInformationId
                        equals b.Id into logBeneficiary
                        from beneficiary in logBeneficiary.DefaultIfEmpty()
                        where log.BeneficiaryInformationId == beneficiaryId
                        select new LogSummaryResultDto
                        {
                            Id = log.Id,
                            Activity = log.Activity,
                            UserName = log.UserName,
                            CreatedAt = log.CreatedAt,
                            BeneficiaryInformationId = beneficiary != null ? beneficiary.Id : Guid.Empty
                        };
            var result =  await query
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
            return result;

        }
    }
}
