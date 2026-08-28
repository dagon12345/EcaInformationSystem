using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class PublicPageViewRepository : IPublicPageViewRepository
    {
        private readonly AppDbContext _context;
        public PublicPageViewRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> IncrementAndGetCountAsync(string pageKey)
        {
            var row = await _context.PublicPageViews.FirstOrDefaultAsync(x => x.PageKey == pageKey);
            if (row is null)
            {
                row = new PublicPageView { Id = Guid.NewGuid(), PageKey = pageKey, ViewCount = 0 };
                await _context.PublicPageViews.AddAsync(row);
            }

            row.ViewCount++;
            row.LastViewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return row.ViewCount;
        }

        public async Task<int> GetCountAsync(string pageKey)
        {
            var row = await _context.PublicPageViews.AsNoTracking().FirstOrDefaultAsync(x => x.PageKey == pageKey);
            return row?.ViewCount ?? 0;
        }
    }
}
