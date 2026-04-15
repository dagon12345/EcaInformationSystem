using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IPendingUserRegistrationRepository
    {
        Task<PendingUserRegistration?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task AddAsync(PendingUserRegistration user, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);

    }
}
