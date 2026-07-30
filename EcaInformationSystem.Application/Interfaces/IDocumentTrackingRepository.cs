using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IDocumentTrackingRepository
    {
        Task<List<DocumentBatch>> GetAllAsync();
        // Tracked (not AsNoTracking) — callers mutate the returned entity in place
        // and call SaveChangesAsync themselves.
        Task<DocumentBatch?> GetByIdAsync(Guid id);
        Task AddAsync(DocumentBatch batch);
        // Explicitly tracks a new transfer as Added via the DbSet directly —
        // unambiguous, unlike adding to an already-tracked parent's loaded
        // navigation collection (batch.Transfers.Add(...)), which is what
        // produced a spurious "Modified" state and a DbUpdateConcurrencyException
        // when relaying an existing (not brand-new) batch.
        void AttachNewTransfer(DocumentTransfer transfer);
        // Same rationale as AttachNewTransfer — adding a row to an existing,
        // already-tracked batch's loaded Rows collection has the same bug.
        void AttachNewRow(DocumentGranteeRow row);
        Task DeleteAsync(DocumentBatch batch);
        Task SaveChangesAsync();
    }
}
