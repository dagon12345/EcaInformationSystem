using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class PasswordResetRequestRepository : IPasswordResetRequestRepository
    {
        private readonly AppDbContext _context;
        public PasswordResetRequestRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
            => await _context.PasswordResetRequests.AddAsync(request, cancellationToken);

        public async Task<PasswordResetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.PasswordResetRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<PasswordResetRequest?> GetLatestPendingByUserNameAsync(string userName, CancellationToken cancellationToken = default)
            => await _context.PasswordResetRequests
                .Where(x => x.UserName == userName && x.Status == 0)
                .OrderByDescending(x => x.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<PasswordResetRequest?> GetLatestApprovedByUserNameAsync(string userName, CancellationToken cancellationToken = default)
            => await _context.PasswordResetRequests
                .Where(x => x.UserName == userName && x.Status == 1)
                .OrderByDescending(x => x.ResolvedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<List<PasswordResetRequest>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _context.PasswordResetRequests
                .OrderByDescending(x => x.RequestedAt)
                .ToListAsync(cancellationToken);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);
    }
}

