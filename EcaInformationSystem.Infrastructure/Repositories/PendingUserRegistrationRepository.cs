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
        public async Task AddAsync(PendingUserRegistration user, CancellationToken cancellationToken = default)
        {
            await _context.PendingUserRegistrations.AddAsync(user, cancellationToken);
        }

        public async Task<PendingUserRegistration?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return await _context.PendingUserRegistrations
                .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
