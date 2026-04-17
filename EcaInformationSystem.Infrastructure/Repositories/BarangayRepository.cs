using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories
{
    public class BarangayRepository : IBarangayRepository
    {
        private readonly AppDbContext _context;
        public BarangayRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Barangay>> GetBarangaysAsync()
        {
            return await _context.Barangays.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Barangay>> GetByMunicipalityCodeAsync(int psgcCodeMunicipality)
        {
            return await _context.Barangays
                .Where(x => x.PsgcCodeMunicipality == psgcCodeMunicipality)
                .Select(x => new Barangay
                {
                    Id = x.Id,
                    PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                    PsgcCodeBarangay = x.PsgcCodeBarangay,
                    Name = x.Name
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Barangay>> GetByMunicipalityIdsAsync(IEnumerable<int> municipalityIds)
        {
            var ids = municipalityIds?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any())
                return Enumerable.Empty<Barangay>();

            return await _context.Barangays
                .AsNoTracking()
                .Where(x => ids.Contains(x.PsgcCodeMunicipality))
                .OrderBy(x => x.Name)
                .ToListAsync();
        }
    }
}
