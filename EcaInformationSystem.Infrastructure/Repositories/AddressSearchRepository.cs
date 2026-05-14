using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Repositories;

public class AddressSearchRepository(AppDbContext db) : IAddressSearchRepository
{
    public async Task<IEnumerable<AddressSearchResultDto>> SearchAsync(
        string query, int maxResults = 15, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        return await (
            from b in db.Barangays
            join m in db.Municipalities on b.PsgcCodeMunicipality equals m.PsgcCodeMunicipality
            join p in db.Provinces on m.PsgcCodeProvince equals p.PsgcCodeProvince
            join r in db.Regions on p.PsgcCodeRegion equals r.PsgcCodeRegion
            where EF.Functions.Like(b.Name!, $"%{query}%")
               || EF.Functions.Like(m.Name!, $"%{query}%")
            orderby b.Name
            select new AddressSearchResultDto
            {
                PsgcCodeBarangay     = b.PsgcCodeBarangay,
                BarangayName         = b.Name ?? string.Empty,
                PsgcCodeMunicipality = m.PsgcCodeMunicipality,
                MunicipalityName     = m.Name ?? string.Empty,
                PsgcCodeProvince     = p.PsgcCodeProvince,
                ProvinceName         = p.Name ?? string.Empty,
                PsgcCodeRegion       = r.PsgcCodeRegion,
                RegionName           = r.Name ?? string.Empty
            }
        ).Take(maxResults).ToListAsync(cancellationToken);
    }
}
