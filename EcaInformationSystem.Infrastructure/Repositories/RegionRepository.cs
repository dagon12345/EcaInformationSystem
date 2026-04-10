using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class RegionRepository : IRegionRepository
    {
        private readonly AppDbContext _context;
        public RegionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Region>> GetAllAsync()
        {
            var regions = await _context.Regions.ToListAsync();
            return regions;
        }
    }
    
}
