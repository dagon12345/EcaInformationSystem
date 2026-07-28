using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class StickyNoteRepository : IStickyNoteRepository
    {
        private readonly AppDbContext _context;

        public StickyNoteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<StickyNote>> GetByUserAsync(Guid userId)
            => await _context.StickyNotes
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();

        public async Task<StickyNote?> GetByIdAsync(Guid id, Guid userId)
            => await _context.StickyNotes
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        public async Task AddAsync(StickyNote note)
            => await _context.StickyNotes.AddAsync(note);

        public void Remove(StickyNote note)
            => _context.StickyNotes.Remove(note);

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}
