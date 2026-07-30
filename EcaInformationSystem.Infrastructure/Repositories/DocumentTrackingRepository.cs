using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class DocumentTrackingRepository : IDocumentTrackingRepository
    {
        private readonly AppDbContext _context;

        public DocumentTrackingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DocumentBatch>> GetAllAsync()
        {
            // Newest-added-first — CreatedAt (actual insert time) is the primary
            // sort, not DateReceived (an admin-entered business date that can be
            // backdated and so doesn't reliably reflect insert order).
            return await _context.DocumentBatches
                .AsNoTracking()
                .Include(b => b.Rows)
                .Include(b => b.Transfers)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<DocumentBatch?> GetByIdAsync(Guid id)
        {
            return await _context.DocumentBatches
                .Include(b => b.Rows)
                .Include(b => b.Transfers)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task AddAsync(DocumentBatch batch)
        {
            await _context.DocumentBatches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }

        public void AttachNewTransfer(DocumentTransfer transfer)
        {
            _context.DocumentTransfers.Add(transfer);
        }

        public void AttachNewRow(DocumentGranteeRow row)
        {
            _context.DocumentGranteeRows.Add(row);
        }

        public async Task DeleteAsync(DocumentBatch batch)
        {
            _context.DocumentBatches.Remove(batch);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
