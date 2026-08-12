namespace EcaInformationSystem.Shared.DTOs
{
    public class DirectoryPersonDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "PDO" or "Focal"
        public bool IsOnline { get; set; }
        // Municipalities this person covers within this province (comma-joined
        // when more than one) — a Focal always has exactly one; a PDO may cover
        // several within the same province.
        public string? MunicipalityName { get; set; }
    }

    public class ProvinceDirectoryGroupDto
    {
        public string ProvinceName { get; set; } = string.Empty;
        public List<DirectoryPersonDto> People { get; set; } = new();
    }
}
