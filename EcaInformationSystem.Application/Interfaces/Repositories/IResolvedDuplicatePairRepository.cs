using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IResolvedDuplicatePairRepository
    {
        // Keyed by normalized (lower GUID, higher GUID) pair.
        Task<Dictionary<(Guid, Guid), ResolvedDuplicatePair>> GetForPairsAsync(IEnumerable<(Guid Record1Id, Guid Record2Id)> pairs);

        Task<ResolvedDuplicatePair> ResolveAsync(Guid record1Id, Guid record2Id, string? remarks, string resolvedBy);

        Task<ResolvedDuplicatePair> UnresolveAsync(Guid record1Id, Guid record2Id, string unresolvedBy);
    }
}
