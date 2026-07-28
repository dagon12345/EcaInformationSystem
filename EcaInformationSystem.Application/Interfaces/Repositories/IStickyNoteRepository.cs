using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces.Repositories
{
    public interface IStickyNoteRepository
    {
        Task<List<StickyNote>> GetByUserAsync(Guid userId);

        // Scoped to userId — a note belonging to someone else simply doesn't
        // resolve, rather than requiring a separate ownership check upstream.
        Task<StickyNote?> GetByIdAsync(Guid id, Guid userId);

        Task AddAsync(StickyNote note);
        void Remove(StickyNote note);
        Task SaveChangesAsync();
    }
}
