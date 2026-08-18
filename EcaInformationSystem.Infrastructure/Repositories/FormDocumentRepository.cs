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

        // Listing/search only need metadata — projecting the columns explicitly
        // (rather than .Include(x => x.Folder) on the full entity) keeps the
        // multi-MB FileData column out of the query entirely, instead of
        // fetching every file's bytes from the DB just to show a title.
        public async Task<List<FormDocument>> GetAllForListingAsync()
            => await _context.FormDocuments
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => new FormDocument
                {
                    Id = x.Id,
                    FolderId = x.FolderId,
                    Folder = x.Folder,
                    Title = x.Title,
                    Description = x.Description,
                    Category = x.Category,
                    OriginalFileName = x.OriginalFileName,
                    ContentType = x.ContentType,
                    FileSizeBytes = x.FileSizeBytes,
                    UploadedBy = x.UploadedBy,
                    UploadedAt = x.UploadedAt,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedAt = x.UpdatedAt,
                    // ✅ These 3 were missing from this explicit projection —
                    // uploads/edits saved them to the DB fine, but every listing
                    // (the Forms Gateway cards, and GridView's payroll lookup)
                    // read them back as null because this Select() never
                    // touched the columns at all.
                    PayrollQuarter = x.PayrollQuarter,
                    FiscalYear = x.FiscalYear,
                    PsgcCodeRegion = x.PsgcCodeRegion,
                    PsgcCodeProvince = x.PsgcCodeProvince,
                    PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                    MilestoneYear = x.MilestoneYear,
                    IsDeleted = x.IsDeleted
                })
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