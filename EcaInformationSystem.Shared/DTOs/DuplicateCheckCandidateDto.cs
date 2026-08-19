namespace EcaInformationSystem.Shared.DTOs
{
    public class DuplicateCheckCandidateDto
    {
        public Guid Id { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public DateTime BirthDate { get; set; }
        public string? OscaIdNumber { get; set; }
        public int? NcscRrn { get; set; }
        public int Province { get; set; }
        public int Municipality { get; set; }
        public int Barangay { get; set; }
    }
}
