using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    // Which municipality/province the invited focal covers — denormalized
    // (same convention as PdoJurisdiction itself, which also copies names
    // rather than FK-ing to Municipality) so both a PDO's own jurisdictions
    // AND an Admin/SuperAdmin's free pick from the full nationwide PSGC list
    // can populate this the same way, with no dependency on the inviter
    // already owning a PdoJurisdiction row.
    public class FocalInviteJurisdiction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid FocalInviteId { get; set; }
        public FocalInvite FocalInvite { get; set; } = default!;

        [Required]
        public int PsgcCodeMunicipality { get; set; }

        [MaxLength(200)]
        public string MunicipalityName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ProvinceName { get; set; } = string.Empty;
    }
}
