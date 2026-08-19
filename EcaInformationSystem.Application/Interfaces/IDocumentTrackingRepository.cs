using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IDocumentTrackingRepository
    {
        Task<(List<TrackedDocument> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search);
        // Tracked (not AsNoTracking) — callers mutate the returned entity in place
        // and call SaveChangesAsync themselves.
        Task<TrackedDocument?> GetByIdAsync(Guid id);
        Task AddAsync(TrackedDocument document);
        // Explicitly tracks a new route as Added via the DbSet directly —
        // unambiguous, unlike adding to an already-tracked parent's loaded
        // navigation collection (document.Routes.Add(...)), which produced a
        // spurious "Modified" state and a DbUpdateConcurrencyException when
        // relaying an existing (not brand-new) document in the renamed
        // ApplicationTracking feature this one mirrors.
        void AttachNewRoute(DocumentRoute route);
        Task DeleteAsync(TrackedDocument document);
        Task<int> GetNextSerialSequenceAsync();
        Task SaveChangesAsync();
    }
}
