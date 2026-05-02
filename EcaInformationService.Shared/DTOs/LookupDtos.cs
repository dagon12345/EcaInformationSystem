// EcaInformationSystem.Shared/DTOs/LookupDtos.cs
namespace EcaInformationSystem.Shared.DTOs
{
    public class RegionLookupDto
    {
        public int PsgcCodeRegion { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ProvinceLookupDto
    {
        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class MunicipalityLookupDto
    {
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class BarangayLookupDto
    {
        public int PsgcCodeMunicipality { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public string Name { get; set; } = string.Empty;
    }
    public class LogsLookupDto
    {
        public Guid Id { get; set; }
        public Guid BeneficiaryInformationId { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}