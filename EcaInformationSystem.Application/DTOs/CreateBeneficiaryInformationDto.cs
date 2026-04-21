using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Application.DTOs
{
    public class CreateBeneficiaryInformationDto
    {
        public DateTime? DateApplied { get; set; }
        public DateTime? DateEndorsed { get; set; }
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public DateTime? OscaIdDateIssued { get; set; }
        public int? NcscRrn { get; set; }
        public string? LastName { get; set; }
        [Required]
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        [Required]
        public DateTime BirthDate { get; set; }
        public string? PhoneNumber { get; set; }
        public int Sex { get; set; }
        public bool IsIndigenousPeople { get; set; }
        public bool IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int PsgcCodeRegion { get; set; }
        public int PsgcCodeProvince { get; set; }
        public int PsgcCodeMunicipality { get; set; }
        public int PsgcCodeBarangay { get; set; }
        public bool IsCompliant { get; set; }
        [Required]
        public string Validator { get; set; } = string.Empty;
        [Required]
        public DateTime ValidationDate { get; set; }
        [Required]
        public int PaymentStatus { get; set; }
        public int ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public bool IsDeceased { get; set; }
        public DateTime? DateOfDeath { get; set; }
        public bool IsEligible { get; set; }
        public string? AssessmentRemarks { get; set; }
        public int? RemarkCategory { get; set; }
        public string? Remarks { get; set; }
        public bool IsDeleted { get; set; }
    }
}
