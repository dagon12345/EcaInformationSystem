using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class UserSessionRepository : IUserSessionRepository
    {
        private readonly AppDbContext _context;
        public UserSessionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserSession session)
        {
            await _context.UserSessions.AddAsync(session);
        }

        public Task<UserSession?> GetByJtiAsync(string jti)
        {
            return _context.UserSessions.FirstOrDefaultAsync(x => x.Jti == jti);
        }

        public Task<List<UserSession>> GetActiveSessionsForUserAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            return _context.UserSessions
                .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
                .OrderByDescending(x => x.LastActiveAt)
                .ToListAsync();
        }

        public Task<UserSession?> GetByIdForUserAsync(Guid sessionId, Guid userId)
        {
            return _context.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId);
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
