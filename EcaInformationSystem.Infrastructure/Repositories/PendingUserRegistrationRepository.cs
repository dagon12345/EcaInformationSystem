using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class PendingUserRegistrationRepository : IPendingUserRegistrationRepository
    {
        private readonly AppDbContext _context;
        public PendingUserRegistrationRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(
            PendingUserRegistration user, CancellationToken cancellationToken = default)
            => await _context.PendingUserRegistrations.AddAsync(user, cancellationToken);


        public async Task<List<PendingUserRegistration>> GetAllAsync(
           CancellationToken cancellationToken = default)
           => await _context.PendingUserRegistrations
               .Include(x => x.Jurisdictions)
               .OrderByDescending(x => x.RequestedAt)
               .ToListAsync(cancellationToken);

        public async Task<List<int>> GetAssignedMunicipalityCodesAsync(Guid userId)
            => await _context.PdoJurisdictions
                .Where(x => x.UserId == userId)
                .Select(x => x.PsgcCodeMunicipality)
                .ToListAsync();

        public async Task<PendingUserRegistration?> GetByIdAsync(
            Guid id, CancellationToken cancellationToken = default)
            => await _context.PendingUserRegistrations
                .Include(x => x.Jurisdictions)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task<PendingUserRegistration?> GetByUserNameAsync(
            string userName, CancellationToken cancellationToken = default)
            => await _context.PendingUserRegistrations
                .Include(x => x.Jurisdictions)
                .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        public async Task<List<PdoJurisdiction>> GetJurisdictionsByUserIdAsync(Guid userId)
             => await _context.PdoJurisdictions
                 .Where(x => x.UserId == userId)
                 .ToListAsync();

        // "The" PDO covering a municipality — a Focal has exactly one, so this
        // takes the first active PDO match (defensive if more than one is ever
        // assigned). Used to auto-create the Focal's 1:1 chat room on invite
        // acceptance.
        public async Task<PendingUserRegistration?> GetPdoByMunicipalityAsync(int psgcCodeMunicipality)
        {
            var pdoUserId = await _context.PdoJurisdictions
                .Where(j => j.PsgcCodeMunicipality == psgcCodeMunicipality)
                .Join(_context.PendingUserRegistrations.Where(u => u.Role == "PDO" && !u.IsDeactivated),
                    j => j.UserId, u => u.Id, (j, u) => u.Id)
                .FirstOrDefaultAsync();

            return pdoUserId == default ? null : await GetByIdAsync(pdoUserId);
        }

        public async Task ReplaceJurisdictionsAsync(
          Guid userId, List<PdoJurisdiction> newJurisdictions)
        {
            // ✅ Delete all existing then insert the new set
            // Simple and avoids diff logic — jurisdiction sets are small
            var existing = await _context.PdoJurisdictions
                .Where(x => x.UserId == userId)
                .ToListAsync();

            _context.PdoJurisdictions.RemoveRange(existing);
            await _context.PdoJurisdictions.AddRangeAsync(newJurisdictions);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // Explicit cleanup rather than relying on cascade — PdoJurisdiction has a
            // configured cascade FK, but UserProfilePicture does not (PendingUserRegistrations
            // has no DB-level PK constraint), so it would otherwise orphan.
            var jurisdictions = await _context.PdoJurisdictions
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);
            _context.PdoJurisdictions.RemoveRange(jurisdictions);

            var picture = await _context.UserProfilePictures
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (picture is not null)
                _context.UserProfilePictures.Remove(picture);

            var user = await _context.PendingUserRegistrations
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is not null)
                _context.PendingUserRegistrations.Remove(user);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
             => await _context.SaveChangesAsync(cancellationToken);
    }
}
