using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class FormFolderRepository : IFormFolderRepository
    {
        private readonly AppDbContext _context;

        public FormFolderRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FormFolder folder)
            => await _context.FormFolders.AddAsync(folder);

        public async Task<FormFolder?> GetByIdAsync(Guid id)
            => await _context.FormFolders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        public async Task<List<FormFolder>> GetAllAsync()
            => await _context.FormFolders.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync();

        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null)
        {
            var normalized = name.Trim().ToLower();
            var query = _context.FormFolders.Where(f => !f.IsDeleted && f.Name.ToLower() == normalized);

            if (excludeId.HasValue)
                query = query.Where(f => f.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<int> CountDocumentsInFolderAsync(Guid folderId)
            => await _context.FormDocuments.CountAsync(d => d.FolderId == folderId && !d.IsDeleted);

        public async Task<List<FormFolder>> GetChildFoldersAsync(Guid parentFolderId)
            => await _context.FormFolders
                .Where(f => f.ParentFolderId == parentFolderId && !f.IsDeleted)
                .ToListAsync();

        public Task UpdateAsync(FormFolder folder)
        {
            _context.FormFolders.Update(folder);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}