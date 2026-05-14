using System.Net.Http.Json;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcaInformationSystem.Infrastructure.Services;

/// <summary>
/// Seeds Philippine geography data from the PSGC API (psgc.gitlab.io).
///
/// INSERT-ONLY: records whose PSGC code already exists in the database are skipped,
/// so existing custom spellings (e.g. "STA. Cruz") are never overwritten.
///
/// Code conversion: PSGC API returns 9-digit string codes (e.g. "160000000").
/// The database stores 10-digit int codes (e.g. 1600000000). Conversion = apiCode × 10.
/// </summary>
public sealed class PsgcSeederService(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    ILogger<PsgcSeederService> logger) : IPsgcSeederService
{
    private const string BaseUrl = "https://psgc.gitlab.io/api";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var http = httpFactory.CreateClient("PsgcApi");

        // Load all existing PSGC codes into memory upfront to avoid N+1 DB queries.
        // This also ensures we never overwrite existing records (custom spellings preserved).
        logger.LogInformation("Loading existing PSGC codes from database...");
        var existingRegions       = await db.Regions.Select(r => r.PsgcCodeRegion).ToHashSetAsync(cancellationToken);
        var existingProvinces     = await db.Provinces.Select(p => p.PsgcCodeProvince).ToHashSetAsync(cancellationToken);
        var existingMunicipalities = await db.Municipalities.Select(m => m.PsgcCodeMunicipality).ToHashSetAsync(cancellationToken);
        var existingBarangays     = await db.Barangays.Select(b => b.PsgcCodeBarangay).ToHashSetAsync(cancellationToken);

        logger.LogInformation(
            "Existing counts — Regions: {R}, Provinces: {P}, Municipalities: {M}, Barangays: {B}",
            existingRegions.Count, existingProvinces.Count, existingMunicipalities.Count, existingBarangays.Count);

        var regions = await http.GetFromJsonAsync<PsgcRegionDto[]>($"{BaseUrl}/regions/", cancellationToken) ?? [];
        logger.LogInformation("PSGC API returned {Count} regions. Starting seed...", regions.Length);

        foreach (var region in regions)
        {
            var regionCode = ToDbCode(region.Code);

            if (!existingRegions.Contains(regionCode))
            {
                await db.Regions.AddAsync(new Region { PsgcCodeRegion = regionCode, Name = region.Name }, cancellationToken);
                existingRegions.Add(regionCode);
            }

            var provinces = await http.GetFromJsonAsync<PsgcProvinceDto[]>(
                $"{BaseUrl}/regions/{region.Code}/provinces/", cancellationToken) ?? [];

            foreach (var province in provinces)
            {
                var provinceCode = ToDbCode(province.Code);

                if (!existingProvinces.Contains(provinceCode))
                {
                    await db.Provinces.AddAsync(new Province
                    {
                        PsgcCodeRegion   = regionCode,
                        PsgcCodeProvince = provinceCode,
                        Name             = province.Name
                    }, cancellationToken);
                    existingProvinces.Add(provinceCode);
                }

                var municipalities = await http.GetFromJsonAsync<PsgcMunicipalityDto[]>(
                    $"{BaseUrl}/provinces/{province.Code}/cities-municipalities/", cancellationToken) ?? [];

                var newBarangays = new List<Barangay>();

                foreach (var municipality in municipalities)
                {
                    var municipalityCode = ToDbCode(municipality.Code);

                    if (!existingMunicipalities.Contains(municipalityCode))
                    {
                        await db.Municipalities.AddAsync(new Municipality
                        {
                            PsgcCodeProvince     = provinceCode,
                            PsgcCodeMunicipality = municipalityCode,
                            Name                 = municipality.Name
                        }, cancellationToken);
                        existingMunicipalities.Add(municipalityCode);
                    }

                    var barangays = await http.GetFromJsonAsync<PsgcBarangayDto[]>(
                        $"{BaseUrl}/cities-municipalities/{municipality.Code}/barangays/", cancellationToken) ?? [];

                    foreach (var barangay in barangays)
                    {
                        var barangayCode = ToDbCode(barangay.Code);

                        if (!existingBarangays.Contains(barangayCode))
                        {
                            newBarangays.Add(new Barangay
                            {
                                PsgcCodeMunicipality = municipalityCode,
                                PsgcCodeBarangay     = barangayCode,
                                Name                 = barangay.Name
                            });
                            existingBarangays.Add(barangayCode);
                        }
                    }
                }

                if (newBarangays.Count > 0)
                    await db.Barangays.AddRangeAsync(newBarangays, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("  Seeded province: {Province} ({Count} new barangays)", province.Name, newBarangays.Count);
            }
        }

        logger.LogInformation("PSGC seeding complete.");
    }

    // PSGC API returns 9-digit string codes. Database stores them as 10-digit ints (× 10).
    // Example: "160000000" (Caraga) → 1600000000
    private static int ToDbCode(string code) => int.Parse(code) * 10;

    private record PsgcRegionDto(string Code, string Name);
    private record PsgcProvinceDto(string Code, string Name);
    private record PsgcMunicipalityDto(string Code, string Name);
    private record PsgcBarangayDto(string Code, string Name);
}
