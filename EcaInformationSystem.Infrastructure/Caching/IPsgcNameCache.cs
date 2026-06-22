namespace EcaInformationSystem.Infrastructure.Caching
{
    public interface IPsgcNameCache
    {
        string? GetRegionName(int code);
        string? GetProvinceName(int code);
        string? GetMunicipalityName(int code);
        string? GetBarangayName(int code);
        // Infrastructure/Caching/IPsgcNameCache.cs
        // Add these four methods to the interface.

        List<int> GetProvinceCodesByNameContains(string term);
        List<int> GetMunicipalityCodesByNameContains(string term);
        List<int> GetBarangayCodesByNameContains(string term);
        List<int> GetRegionCodesByNameContains(string term);
        Task RefreshAsync(CancellationToken ct = default);
    }
}