namespace EcaInformationSystem.Shared.DTOs
{
    // Self-service view of the current user's own profile.
    public class UserProfileDto
    {
        public Guid Id { get; set; }

        // Read-only — never editable, shown for reference only.
        public string UserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }

        public string Role { get; set; } = string.Empty;
        public int? Region { get; set; }
        public string? RegionName { get; set; }
        // Assigned municipality/ies (from PdoJurisdiction) — mainly relevant for
        // PDO/Focal accounts, empty for everyone else. A Focal covers exactly
        // one, but this stays a list to match how PDO jurisdictions already work.
        public List<string> MunicipalityNames { get; set; } = new();

        public bool IsMfaEnabled { get; set; }
        public bool HasProfilePicture { get; set; }
    }

    public class UpdateProfileRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
    }

    // What another user sees when viewing someone else's profile — deliberately
    // narrower than UserProfileDto: no Username, no BirthDate, no MFA status.
    public class PublicUserProfileDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? RegionName { get; set; }
        public List<string> MunicipalityNames { get; set; } = new();
        public bool HasProfilePicture { get; set; }
    }
}
