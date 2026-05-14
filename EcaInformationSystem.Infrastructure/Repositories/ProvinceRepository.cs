using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

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
            var all = await _context.Provinces
                .Where(x => x.PsgcCodeRegion == psgcCodeRegion)
                .Select(x => new Province
                {
                    Id = x.Id,
                    PsgcCodeRegion = x.PsgcCodeRegion,
                    PsgcCodeProvince = x.PsgcCodeProvince,
                    Name = x.Name
                })
                .OrderBy(x => x.Name)
                .AsNoTracking()
                .ToListAsync();

            // Deduplicate by name — guards against legacy records that share a name
            // with PSGC-seeded entries that have a different PsgcCodeProvince.
            // Prefer the ORIGINAL entry (lowest Id) because beneficiary records
            // were created before PSGC seeding and reference that code.
            return all
                .GroupBy(x => x.Name!.Trim().ToUpperInvariant())
                .Select(g => g.OrderBy(x => x.Id).First())
                .OrderBy(x => x.Name);
        }

        public async Task<IEnumerable<Province>> GetAllProvinceAsync()
        {
            return await _context.Provinces.AsNoTracking().ToListAsync();
        }
    }
}
