namespace EcaInformationSystem.Shared.DTOs;

public class AddressSearchResultDto
{
    public int PsgcCodeBarangay { get; set; }
    public string BarangayName { get; set; } = string.Empty;
    public int PsgcCodeMunicipality { get; set; }
    public string MunicipalityName { get; set; } = string.Empty;
    public int PsgcCodeProvince { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public int PsgcCodeRegion { get; set; }
    public string RegionName { get; set; } = string.Empty;
}
