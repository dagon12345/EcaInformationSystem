using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IFocalInviteRepository
    {
        Task AddAsync(FocalInvite invite, CancellationToken cancellationToken = default);
        Task<FocalInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<FocalInvite?> GetPendingByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);
        Task<List<FocalInvite>> GetByInviterAsync(Guid pdoUserId, CancellationToken cancellationToken = default);
        // ✅ NEW — every accepted invite's InvitedByUserId/ResultingUserId pair,
        // for building "which PDO (or Admin) invited this focal" groupings
        // (e.g. the provincial directory's PDO-branching view). Deliberately
        // narrow — callers only need the mapping, not full invite records.
        Task<List<(Guid InvitedByUserId, Guid ResultingUserId)>> GetAcceptedInviterMapAsync(CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
