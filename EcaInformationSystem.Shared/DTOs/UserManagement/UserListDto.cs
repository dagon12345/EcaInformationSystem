// Shared/DTOs/UserManagement/UserListDto.cs
namespace EcaInformationSystem.Shared.DTOs.UserManagement
{
    public class UserListDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ApprovalStatus { get; set; }  // 0=Pending 1=Approved 2=Rejected
        public bool IsActivated { get; set; }
        public DateTime RequestedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? Remarks { get; set; }
        public int? Region { get; set; }
        public string? RegionName { get; set; } // resolved name, e.g. "Caraga"
        public List<JurisdictionDto> Jurisdictions { get; set; } = new();
    }

    public class JurisdictionDto
    {
        public Guid Id { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
    }

    public class ApproveUserRequestDto
    {
        public string Role { get; set; } = "Viewer";  // Viewer | PDO | Admin | SuperAdmin
        public string? Remarks { get; set; }
    }

    public class RejectUserRequestDto
    {
        public string? Remarks { get; set; }
    }

    public class AssignJurisdictionRequestDto
    {
        // List of municipality PSGC codes to assign
        public List<int> MunicipalityCodes { get; set; } = new();
    }
}