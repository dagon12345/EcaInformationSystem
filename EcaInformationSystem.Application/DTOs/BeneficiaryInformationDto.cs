namespace EcaInformationSystem.Application.DTOs
{
    public class BeneficiaryInformationDto
    {
        public Guid Id { get; set; }
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public int? NcscRrn { get; set; }
        public string? LastName { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public int Sex { get; set; }
        public bool IsIndigenousPeople { get; set; }
        public bool IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int PsgcCodeRegion { get; set; }
        public string? Region { get; set; }
        public int PsgcCodeProvince { get; set; }
        public string? Province { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public string? Municipality { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public string? Barangay { get; set; }
        public bool IsCompliant { get; set; }
        public string Validator { get; set; } = string.Empty;
        public DateTime ValidationDate { get; set; }
        public int PaymentStatus { get; set; }
        public int ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public bool IsDeceased { get; set; }
        public DateTime? DateOfDeath { get; set; }
        public bool IsEligible { get; set; }
        public int? RemarkCategory { get; set; }
        public string? Remarks { get; set; }
        public DateTime DateAdded { get; set; }
        public bool IsDeleted { get; set; }
    }
}
