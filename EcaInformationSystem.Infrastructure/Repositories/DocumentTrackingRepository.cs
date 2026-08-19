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

        public async Task<(List<TrackedDocument> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search)
        {
            var query = _context.TrackedDocuments.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(d => d.SerialNumber.ToLower().Contains(term) || d.Title.ToLower().Contains(term));
            }

            query = query.OrderByDescending(d => d.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<TrackedDocument?> GetByIdAsync(Guid id)
        {
            return await _context.TrackedDocuments
                .Include(d => d.Routes)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task AddAsync(TrackedDocument document)
        {
            await _context.TrackedDocuments.AddAsync(document);
            await _context.SaveChangesAsync();
        }

        public void AttachNewRoute(DocumentRoute route)
        {
            _context.DocumentRoutes.Add(route);
        }

        public async Task DeleteAsync(TrackedDocument document)
        {
            _context.TrackedDocuments.Remove(document);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetNextSerialSequenceAsync()
        {
            var results = await _context.Database
                .SqlQuery<int>($"SELECT NEXT VALUE FOR DocumentTrackingSerialSeq AS Value")
                .ToListAsync();
            return results[0];
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
