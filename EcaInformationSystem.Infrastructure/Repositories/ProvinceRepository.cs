using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class ProvinceRepository : IProvinceRepository
    {
        private readonly AppDbContext _context;

        public ProvinceRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Province>> GetAllAsync(int psgcCodeRegion)
        {
            return await _context.Provinces
                .Where(x => x.PsgcCodeRegion == psgcCodeRegion)
                .Select(x => new Province
                {
                    Id = x.Id,
                    PsgcCodeRegion = x.PsgcCodeRegion,
                    PsgcCodeProvince = x.PsgcCodeProvince,
                    Name = x.Name
                })
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
