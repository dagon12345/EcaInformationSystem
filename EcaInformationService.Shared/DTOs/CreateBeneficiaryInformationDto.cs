using System.ComponentModel.DataAnnotations;
using EcaInformationService.Shared.DTOs;

namespace EcaInformationSystem.Shared.DTOs
{
    public class CreateBeneficiaryInformationDto
    {
        public DateTime? DateApplied { get; set; }
        public DateTime? DateEndorsed { get; set; }
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public DateTime? OscaIdDateIssued { get; set; }
        public int? NcscRrn { get; set; }
        [Required(ErrorMessage = "Last Name is required.")]
        public string? LastName { get; set; }
        [Required(ErrorMessage = "First Name is required.")]
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        [Required(ErrorMessage = "Birth Date is required.")]
        public DateTime BirthDate { get; set; }
        public string? PhoneNumber { get; set; }
        [Range(1, 2, ErrorMessage = "Please select Sex.")]
        public int Sex { get; set; }
        public bool IsIndigenousPeople { get; set; }
        public bool IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int PsgcCodeRegion { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Province.")]
        public int PsgcCodeProvince { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Municipality.")]
        public int PsgcCodeMunicipality { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Barangay.")]
        public int PsgcCodeBarangay { get; set; }
        public bool IsCompliant { get; set; }
        [Required(ErrorMessage = "Validator is required.")]
        public string Validator { get; set; } = string.Empty;
        [Required(ErrorMessage = "Validation Date is required.")]
        public DateTime ValidationDate { get; set; }
        [Range(0, 3, ErrorMessage = "Please select Payment Status.")]
        public int PaymentStatus { get; set; }
        public int ModeOfPayment { get; set; }
        public DateTime? PaymentDate { get; set; }
        public bool IsDeceased { get; set; }
        public DateTime? DateOfDeath { get; set; }
        public bool IsEligible { get; set; }
        public string? AssessmentRemarks { get; set; }
        public string? EligibilityRemarks { get; set; }
        public int? RemarkCategory { get; set; }
        public string? Remarks { get; set; }
        public bool IsDeleted { get; set; }
        //<summary>
        //  Set to true when the user has reviewed soft duplicated and confirmed they want to proceed
        // </summary>
        public bool  BypassSoftDuplicateCheck { get; set; }
    }
}
