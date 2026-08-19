using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class ApplicationTrackingRepository : IApplicationTrackingRepository
    {
        private readonly AppDbContext _context;

        public ApplicationTrackingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ApplicationBatch>> GetAllAsync()
        {
            // Newest-added-first — CreatedAt (actual insert time) is the primary
            // sort, not DateReceived (an admin-entered business date that can be
            // backdated and so doesn't reliably reflect insert order).
            return await _context.ApplicationBatches
                .AsNoTracking()
                .Include(b => b.Rows)
                .Include(b => b.Transfers)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<ApplicationBatch?> GetByIdAsync(Guid id)
        {
            return await _context.ApplicationBatches
                .Include(b => b.Rows)
                .Include(b => b.Transfers)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task AddAsync(ApplicationBatch batch)
        {
            await _context.ApplicationBatches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }

        public void AttachNewTransfer(ApplicationTransfer transfer)
        {
            _context.ApplicationTransfers.Add(transfer);
        }

        public void AttachNewRow(ApplicationGranteeRow row)
        {
            _context.ApplicationGranteeRows.Add(row);
        }

        public async Task DeleteAsync(ApplicationBatch batch)
        {
            _context.ApplicationBatches.Remove(batch);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
