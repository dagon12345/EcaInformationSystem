namespace EcaInformationSystem.Shared.DTOs.Common
{
    // Lightweight user entry for "tag this person" pickers — any authenticated
    // role may fetch this list (unlike the full UserManagement roster).
    public class UserLookupDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
