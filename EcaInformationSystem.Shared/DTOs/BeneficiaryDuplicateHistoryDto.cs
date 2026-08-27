namespace EcaInformationSystem.Shared.DTOs
{
    // One Known Duplicate event involving a specific grantee, from that
    // grantee's own point of view — see BeneficiaryDuplicateHistory.
    public class BeneficiaryDuplicateHistoryDto
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // "Create" or "Import"

        // True when THIS grantee is the newer record that was saved despite
        // matching OtherRecordId exactly; false when this grantee is the
        // pre-existing record and OtherRecordId was saved as its duplicate.
        public bool IsTheNewerRecord { get; set; }

        public Guid OtherRecordId { get; set; }
        public string OtherRecordFullName { get; set; } = string.Empty;
        public DateTime OtherRecordBirthDate { get; set; }
    }
}
