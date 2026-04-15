namespace EcaInformationSystem.Domain.Entities
{
    public class BeneficiaryInformation
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
        public int? RemarkCategory { get; set; }
        public string? Remarks { get; set; }
        public DateTime DateAdded { get; set; }
        public bool IsDeleted { get; set; }

        public void Update(string? batchCode, string? oscaIdNumber, int? ncscRn, string? lastName, string firstName, string? middleName, string? extensionName,
            DateTime birthDate, int sex, bool isIndigenousPeople, bool isPersonWithDisability, int? civilStatus, int? citizenship, 
            int region, int province, int municipality, int barangay, bool iscompliant, string validator, DateTime validationDate, int paymentStatus, int modeOfPayment,
            DateTime? paymentDate, bool isdeceased, DateTime? dateOfDeath, bool iseligible, int? remarkCategory, string? remarks, DateTime dateAdded)
        {
            BatchCode = batchCode;
            OscaIdNumber = oscaIdNumber;
            NcscRrn = ncscRn;
            LastName = lastName;
            FirstName = firstName;
            MiddleName = middleName;
            Extension = extensionName;
            BirthDate = birthDate;
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
            IsEligible = iseligible;
            RemarkCategory = remarkCategory;
            Remarks = remarks;
            DateAdded = dateAdded;
        }
    }
}
