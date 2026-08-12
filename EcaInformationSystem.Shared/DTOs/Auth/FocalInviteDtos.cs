namespace EcaInformationSystem.Shared.DTOs.Auth
{
    // A PDO's own jurisdiction row — used to populate the "which province does
    // this focal cover" checklist when creating an invite.
    public class MyJurisdictionDto
    {
        public Guid Id { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
    }

    // The municipality(ies) a focal covers, submitted directly by the inviter —
    // a PDO picks from their own MyJurisdictionDto list, an Admin/SuperAdmin
    // picks from the full nationwide api/Province + api/Municipality lookup
    // (same source UserManagement.razor's jurisdiction-assignment screen uses).
    public class FocalMunicipalitySelection
    {
        public int PsgcCodeMunicipality { get; set; }
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
    }

    public class CreateFocalInviteRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? ContactNote { get; set; }
        public List<FocalMunicipalitySelection> Municipalities { get; set; } = new();
    }

    public class FocalInviteResultDto
    {
        public Guid InviteId { get; set; }
        public string Code { get; set; } = string.Empty;      // plain — shown once to the PDO
        public string InviteUrl { get; set; } = string.Empty; // full link the PDO copies/sends manually
        public DateTime ExpiresAt { get; set; }
    }

    // A pending invite the inviter created earlier — the plain code itself is
    // never retrievable (only its hash is stored), so this is deliberately
    // just enough to recognize which invite it was; getting the link back
    // requires regenerating it (see RegenerateFocalInviteLink), which issues
    // a fresh code and invalidates the old one.
    public class FocalInviteSummaryDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNote { get; set; }
        public string MunicipalityNames { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    // What the public accept-invite page shows before asking for a password.
    public class FocalInvitePreviewDto
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<string> ProvinceNames { get; set; } = new();
    }

    public class AcceptFocalInviteRequest
    {
        public string Code { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
