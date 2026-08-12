using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class VoiceCallLogRepository : IVoiceCallLogRepository
    {
        private readonly AppDbContext _context;

        public VoiceCallLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VoiceCallLog log, CancellationToken cancellationToken = default)
            => await _context.VoiceCallLogs.AddAsync(log, cancellationToken);

        public async Task<VoiceCallLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.VoiceCallLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<List<VoiceCallLog>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.VoiceCallLogs
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync(cancellationToken);

        public async Task<List<VoiceCallLog>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => await _context.VoiceCallLogs
                .AsNoTracking()
                .Where(x => x.CallerId == userId || x.CalleeId == userId)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync(cancellationToken);

        public void Remove(VoiceCallLog log)
            => _context.VoiceCallLogs.Remove(log);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);
    }
}
