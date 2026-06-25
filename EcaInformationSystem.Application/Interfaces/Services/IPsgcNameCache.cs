namespace EcaInformationSystem.Application.Interfaces
{
    public interface IPsgcNameCache
    {
        string? GetRegionName(int code);
        string? GetProvinceName(int code);
        string? GetMunicipalityName(int code);
        string? GetBarangayName(int code);

        List<int> GetProvinceCodesByNameContains(string term);
        List<int> GetMunicipalityCodesByNameContains(string term);
        List<int> GetBarangayCodesByNameContains(string term);
        List<int> GetRegionCodesByNameContains(string term);
        Task RefreshAsync(CancellationToken ct = default);
    }
}