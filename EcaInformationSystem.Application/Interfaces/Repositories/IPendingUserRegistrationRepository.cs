using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IPendingUserRegistrationRepository
    {
        Task<PendingUserRegistration?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<PendingUserRegistration?> GetByIdAsync(
           Guid id, CancellationToken cancellationToken = default);
        Task<List<PendingUserRegistration>> GetAllAsync(
           CancellationToken cancellationToken = default);
        Task AddAsync(PendingUserRegistration user, CancellationToken cancellationToken = default);
        // Hard delete — removes the account and its jurisdictions/profile picture.
        Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        // ── Jurisdiction ────────────────────────────────────────────────────
        Task<List<PdoJurisdiction>> GetJurisdictionsByUserIdAsync(Guid userId);
        Task<PendingUserRegistration?> GetPdoByMunicipalityAsync(int psgcCodeMunicipality);
        Task ReplaceJurisdictionsAsync(Guid userId, List<PdoJurisdiction> jurisdictions);
        Task<List<int>> GetAssignedMunicipalityCodesAsync(Guid userId);

    }
}
