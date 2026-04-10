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
    }
}
