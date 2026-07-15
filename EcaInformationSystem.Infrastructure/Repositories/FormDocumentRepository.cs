using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class FormDocumentRepository : IFormDocumentRepository
    {
        private readonly AppDbContext _context;

        public FormDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FormDocument doc)
            => await _context.FormDocuments.AddAsync(doc);

        public async Task<FormDocument?> GetByIdAsync(Guid id)
            => await _context.FormDocuments
                .Include(x => x.Folder)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        public async Task<List<FormDocument>> GetAllAsync()
            => await _context.FormDocuments
                .AsNoTracking()
                .Include(x => x.Folder)
                .Where(x => !x.IsDeleted)
                .ToListAsync();

        public Task UpdateAsync(FormDocument doc)
        {
            _context.FormDocuments.Update(doc);
            return Task.CompletedTask;
        }

        public async Task EnsureFolderExistsAsync(Guid folderId)
        {
            var exists = await _context.FormFolders.AnyAsync(f => f.Id == folderId && !f.IsDeleted);
            if (!exists)
                throw new KeyNotFoundException("Target folder does not exist.");
        }

        // ✅ Called when a folder is deleted — detaches its documents rather than
        // deleting them. Bulk update, single round trip.
        public async Task DetachFromFolderAsync(Guid folderId)
        {
            var docs = await _context.FormDocuments
                .Where(d => d.FolderId == folderId && !d.IsDeleted)
                .ToListAsync();

            foreach (var d in docs)
                d.FolderId = null;
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}