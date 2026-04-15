using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;

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
    }
}
