using System.ComponentModel.DataAnnotations;

namespace EcaInformationSystem.Domain.Entities
{
    public class BeneficiaryInformation
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
        public int? NcscRrn { get; set; }
        public string? LastName { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Extension { get; set; }
        public DateTime BirthDate { get; set; }
        public string? PhoneNumber { get; set; }
        public int Sex { get; set; }
        public bool IsIndigenousPeople { get; set; }
        public bool IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int Region { get; set; }
        public int Province { get; set; }
        public int Municipality { get; set; }
        public int Barangay { get; set; }
        public bool IsCompliant { get; set; }
        public string Validator { get; set; } =string.Empty;
        public DateTime ValidationDate { get; set; }
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
        public int? CoStatus { get; set; }
        public DateTime? CoDateEndorsed { get; set; }
        public DateTime? CoDateApproved { get; set; }
        public bool IsDeleted { get; set; }
        public BeneficiaryFinding? Finding { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;
        public void Update(int? quarter, string? batch, int? refYear, string? refCode, DateTime? dateApplied, DateTime? dateEndorsed, string? batchCode, string? oscaIdNumber, DateTime? oscaIdDateIssued, int? ncscRn, string? lastName, string firstName, string? middleName, string? extensionName,
            DateTime birthDate, string? phoneNumber, int sex, bool isIndigenousPeople, bool isPersonWithDisability, int? civilStatus, int? citizenship, 
            int region, int province, int municipality, int barangay, bool iscompliant, string validator, DateTime validationDate, int paymentStatus, int modeOfPayment,
            DateTime? paymentDate, bool isdeceased, DateTime? dateOfDeath, bool isEligible, string? assessmentRemarks, string? eligibilityRemarks, int? remarkCategory, string? remarks
            , int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved)
        {
            Quarter = quarter;
            Batch = batch;
            RefYear = refYear;
            RefCode = refCode;
            DateApplied = dateApplied;
            DateEndorsed = dateEndorsed;
            BatchCode = batchCode;
            OscaIdNumber = oscaIdNumber;
            OscaIdDateIssued = oscaIdDateIssued;
            NcscRrn = ncscRn;
            LastName = lastName;
            FirstName = firstName;
            MiddleName = middleName;
            Extension = extensionName;
            BirthDate = birthDate;
            PhoneNumber = phoneNumber;
            Sex = sex;
            IsIndigenousPeople = isIndigenousPeople;
            IsPersonWithDisability = isPersonWithDisability;
            CivilStatus = civilStatus;
            Citizenship = citizenship;
            Region = region;
            Province = province;
            Municipality = municipality;
            Barangay = barangay;
            IsCompliant = iscompliant;
            Validator = validator;
            ValidationDate = validationDate;
            PaymentStatus = paymentStatus;
            ModeOfPayment = modeOfPayment;
            PaymentDate = paymentDate;
            IsDeceased = isdeceased;
            DateOfDeath = dateOfDeath;
            IsEligible = isEligible;
            AssessmentRemarks = assessmentRemarks;
            EligibilityRemarks = eligibilityRemarks;
            RemarkCategory = remarkCategory;
            Remarks = remarks;
            CoStatus = coStatus;
            CoDateEndorsed = coDateEndorsed;
            CoDateApproved = coDateApproved;
        }
    }
}
