using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class FormActivityLogRepository : IFormActivityLogRepository
    {
        private readonly AppDbContext _context;

        public FormActivityLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FormActivityLog log)
        {
            await _context.FormActivityLogs.AddAsync(log);
            await _context.SaveChangesAsync(); // logs write immediately — never lost if a later step throws
        }

        public async Task<List<FormActivityLog>> GetRecentAsync(int take = 100)
            => await _context.FormActivityLogs
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();
    }
}