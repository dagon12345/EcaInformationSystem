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

    // ✅ NEW — one PDO's branch within a province: the PDO themselves, plus
    // every Focal they invited whose own municipality falls within this same
    // province. PdoUserId == null is the "Admin-Invited" bucket — focals
    // invited by an Admin/SuperAdmin rather than a jurisdiction-bound PDO,
    // who therefore don't branch under any single PDO.
    public class PdoDirectoryBranchDto
    {
        public Guid? PdoUserId { get; set; }
        public string PdoName { get; set; } = string.Empty;
        // ✅ NEW — "PDO" or "Admin" for a real branch, null for the generic
        // Admin-Invited bucket (PdoUserId also null in that case). Lets the
        // UI label the branch header correctly instead of assuming "PDO".
        public string? BranchRole { get; set; }
        public bool IsOnline { get; set; }
        public List<DirectoryPersonDto> Focals { get; set; } = new();
    }

    public class ProvinceDirectoryGroupDto
    {
        public string ProvinceName { get; set; } = string.Empty;
        public List<DirectoryPersonDto> People { get; set; } = new();
        // ✅ NEW — the branching structure DirectoryListing.razor renders.
        public List<PdoDirectoryBranchDto> PdoBranches { get; set; } = new();
    }
}
