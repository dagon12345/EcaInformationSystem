using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class SystemUpdateNoticeRepository : ISystemUpdateNoticeRepository
    {
        private readonly AppDbContext _context;

        public SystemUpdateNoticeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SystemUpdateNotice>> GetAllAsync()
        {
            return await _context.SystemUpdateNotices
                .AsNoTracking()
                .OrderByDescending(n => n.PublishedAt)
                .ToListAsync();
        }

        public async Task<SystemUpdateNotice?> GetLatestAsync()
        {
            return await _context.SystemUpdateNotices
                .AsNoTracking()
                .OrderByDescending(n => n.PublishedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(SystemUpdateNotice notice)
        {
            await _context.SystemUpdateNotices.AddAsync(notice);
            await _context.SaveChangesAsync();
        }
    }
}
