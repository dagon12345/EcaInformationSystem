using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IApplicationTrackingRepository
    {
        Task<List<ApplicationBatch>> GetAllAsync();
        // Tracked (not AsNoTracking) — callers mutate the returned entity in place
        // and call SaveChangesAsync themselves.
        Task<ApplicationBatch?> GetByIdAsync(Guid id);
        Task AddAsync(ApplicationBatch batch);
        // Explicitly tracks a new transfer as Added via the DbSet directly —
        // unambiguous, unlike adding to an already-tracked parent's loaded
        // navigation collection (batch.Transfers.Add(...)), which is what
        // produced a spurious "Modified" state and a DbUpdateConcurrencyException
        // when relaying an existing (not brand-new) batch.
        void AttachNewTransfer(ApplicationTransfer transfer);
        // Same rationale as AttachNewTransfer — adding a row to an existing,
        // already-tracked batch's loaded Rows collection has the same bug.
        void AttachNewRow(ApplicationGranteeRow row);
        Task DeleteAsync(ApplicationBatch batch);
        Task SaveChangesAsync();
    }
}
