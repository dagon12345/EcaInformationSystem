using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class MunicipalityRepository : IMunicipalityRepository
    {
        private readonly AppDbContext _context;

        public MunicipalityRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Municipality>> GetByProvinceCodeAsync(int psgcCodeProvince)
        {
            return await _context.Municipalities
                .Where(m => m.PsgcCodeProvince == psgcCodeProvince)
                .Select(x => new Municipality
                {
                    Id = x.Id,
                    PsgcCodeProvince = x.PsgcCodeProvince,
                    PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                    Name = x.Name
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Municipality>> GetAllMunicipalityAsync()
        {
            return await _context.Municipalities.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Municipality>> GetByProvinceIdsAsync(IEnumerable<int> provinceIds)
        {
            var ids = provinceIds?
               .Where(x => x > 0)
               .Distinct()
               .ToList() ?? new List<int>();

            if (!ids.Any())
                return Enumerable.Empty<Municipality>();

            return await _context.Municipalities
                .AsNoTracking()
                .Where(x => ids.Contains(x.PsgcCodeProvince))
                .OrderBy(x => x.Name)
                .ToListAsync();
        }
    }
}
