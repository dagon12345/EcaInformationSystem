using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EcaInformationSystem.Shared.Helpers;

namespace EcaInformationSystem.Shared.DTOs
{
    public class BeneficiaryInformationDto
    {
        public Guid Id { get; set; }
        public int? Quarter { get; set; }
        public string? Batch { get; set; }
        public int? RefYear { get; set; }
        public string? RefCode { get; set; }
        public DateTime? DateApplied { get; set; }
        public DateTime? DateEndorsed { get; set; }
        public string? BatchCode { get; set; }
        public string? OscaIdNumber { get; set; }
        public DateTime? OscaIdDateIssued { get; set; }
        [Required(ErrorMessage = "NCSC Registration Reference Number is required.")]
        public int? NcscRrn { get; set; }
        // Includes Ñ/ñ — a standard letter in Philippine names (e.g. "Ñoño",
        // "Piñero") that plain [A-Za-z] excludes.
        [Required(ErrorMessage = "Last Name is required.")]
        [RegularExpression(@"^[A-Za-zÑñ\s\-]*$", ErrorMessage = "Last Name can only contain letters, spaces, and dashes.")]
        public string? LastName { get; set; }
        [Required(ErrorMessage = "First Name is required.")]
        [RegularExpression(@"^[A-Za-zÑñ\s\-]*$", ErrorMessage = "First Name can only contain letters, spaces, and dashes.")]
        public string FirstName { get; set; } = string.Empty;
        [RegularExpression(@"^[A-Za-zÑñ\s\-]*$", ErrorMessage = "Middle Name can only contain letters, spaces, and dashes.")]
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        [Required(ErrorMessage = "Birth Date is required.")]
        public DateTime BirthDate { get; set; }
        // ✅ Replaces the old single PhoneNumber scalar — grantees can have multiple
        // contact numbers. At least one required; each must be a valid PH mobile
        // number, enforced in the client (see BeneficiaryForm.razor) since nested
        // list items aren't covered by <DataAnnotationsValidator/>.
        public List<BeneficiaryPhoneNumberDto> PhoneNumbers { get; set; } = new();
        public int Age { get; set; }
        public int MilestoneYear { get; set; }
        [Range(1, 2, ErrorMessage = "Please select Sex.")]
        public int Sex { get; set; }
        public bool? IsIndigenousPeople { get; set; }
        public bool? IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int PsgcCodeRegion { get; set; }
        public JsonElement? Region { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Province.")]
        public int PsgcCodeProvince { get; set; }
        public JsonElement? Province { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Municipality.")]
        public int PsgcCodeMunicipality { get; set; }
        public JsonElement? Municipality { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please select Barangay.")]
        public int PsgcCodeBarangay { get; set; }
        public JsonElement? Barangay { get; set; }
        public bool IsCompliant { get; set; }
        [Required(ErrorMessage = "Validator is required.")]
        public string Validator { get; set; } = string.Empty;
        [Required(ErrorMessage = "Validation Date is required.")]
        public DateTime ValidationDate { get; set; }
        public int? PayrollQuarter { get; set; }
        public int? FiscalYear { get; set; }
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
        public DateTime DateAdded { get; set; }
        public bool IsDeleted { get; set; }
        //For Beneficiary Findings
        public int? FindingStatus { get; set; }
        public string? FindingRemarks { get; set; }

        // Add these helper properties so your UI code doesn't have to change
        public string RegionName => GetJsonString(Region);
        public string ProvinceName => GetJsonString(Province);
        public string MunicipalityName => GetJsonString(Municipality);
        public string BarangayName => GetJsonString(Barangay);

        public int? CoStatus { get; set; }
        public DateTime? CoDateEndorsed { get; set; }
        public DateTime? CoDateApproved { get; set; }
        public byte[]? RowVersion { get; set; }
        public bool HasDocuments { get; set; }

        public int? CgpPageNumber { get; set; }
        public Guid? CgpGenerationId { get; set; }
        public string? CgpPrefix { get; set; }
        [Required(ErrorMessage = "Tracking Number is required.")]
        // ✅ NEW — Annex A (2026) flat fields
        public string? TrackingNumber { get; set; }
        public bool? DataPrivacyConsent { get; set; }
        public int? PlaceOfSubmission { get; set; }
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? ZipCode { get; set; }
        public string? DisabilityType { get; set; }
        public string? EthnicityName { get; set; }
        public string? DualCitizenshipDetails { get; set; }
        public string? CivilStatusOtherDetail { get; set; }
        public bool IsSignedDeclaration { get; set; }
        public DateTime? DateSigned { get; set; }
        public bool? IsLivenessVerified { get; set; }
        public DateTime? DateOfLiveness { get; set; }
        public bool? IsReadyForEft { get; set; }
        // ✅ NEW — nested sub-entities (null-safe: absent until saved)
        public List<BeneficiaryFamilyMemberDto> FamilyMembers { get; set; } = new();
        public BeneficiaryBankAccountDto? BankAccount { get; set; }
        public BeneficiaryAbroadAddressDto? AbroadAddress { get; set; }
        public BeneficiaryClaimantDto? Claimant { get; set; }
        public BeneficiaryVerificationChecklistDto? VerificationChecklist { get; set; }
        // NEW — alongside BankAccount, AbroadAddress, Claimant, VerificationChecklist
        public BeneficiaryClaimantBankAccountDto? ClaimantBankAccount { get; set; }
        private string GetJsonString(JsonElement? element)
        {
            if (element == null) return string.Empty;
            return element.Value.ValueKind switch
            {
                JsonValueKind.String => element.Value.GetString() ?? "",
                JsonValueKind.Number => element.Value.GetRawText(),
                _ => ""
            };
        }

        // ✅ Now passes RefCode — generated once, stored in DB
        public string? ReferenceNumber =>
            RegionRomanNumeralHelper.BuildReferenceNumber(
                Quarter, Batch, RefYear, RefCode, PsgcCodeRegion);
    }
}
