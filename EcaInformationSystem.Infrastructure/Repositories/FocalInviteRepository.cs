using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class FocalInviteRepository : IFocalInviteRepository
    {
        private readonly AppDbContext _context;

        public FocalInviteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FocalInvite invite, CancellationToken cancellationToken = default)
            => await _context.FocalInvites.AddAsync(invite, cancellationToken);

        public async Task<FocalInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.FocalInvites
                .Include(x => x.Jurisdictions)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<FocalInvite?> GetPendingByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default)
            => await _context.FocalInvites
                .Include(x => x.Jurisdictions)
                .FirstOrDefaultAsync(x => x.CodeHash == codeHash && x.Status == 0, cancellationToken);

        public async Task<List<FocalInvite>> GetByInviterAsync(Guid pdoUserId, CancellationToken cancellationToken = default)
            => await _context.FocalInvites
                .AsNoTracking()
                .Where(x => x.InvitedByUserId == pdoUserId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        public async Task<List<(Guid InvitedByUserId, Guid ResultingUserId)>> GetAcceptedInviterMapAsync(CancellationToken cancellationToken = default)
            => (await _context.FocalInvites
                .AsNoTracking()
                .Where(x => x.Status == 1 && x.ResultingUserId.HasValue)
                .Select(x => new { x.InvitedByUserId, ResultingUserId = x.ResultingUserId!.Value })
                .ToListAsync(cancellationToken))
                .Select(x => (x.InvitedByUserId, x.ResultingUserId))
                .ToList();

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);
    }
}
