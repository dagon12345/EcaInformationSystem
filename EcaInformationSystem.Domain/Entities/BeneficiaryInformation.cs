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
        public int Sex { get; set; }
        public bool? IsIndigenousPeople { get; set; }
        public bool? IsPersonWithDisability { get; set; }
        public int? CivilStatus { get; set; }
        public int? Citizenship { get; set; }
        public int Region { get; set; }
        public int Province { get; set; }
        public int Municipality { get; set; }
        public int Barangay { get; set; }
        public bool IsCompliant { get; set; }
        public string Validator { get; set; } = string.Empty;
        public DateTime ValidationDate { get; set; }
        public Guid? CurrentPaymentHistoryId { get; set; }
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
        public int? CoStatus { get; set; }
        public DateTime? CoDateEndorsed { get; set; }
        public DateTime? CoDateApproved { get; set; }
        public bool IsDeleted { get; set; }
        public BeneficiaryFinding? Finding { get; set; }
        public int? CgpPageNumber { get; set; }
        public string? CgpPrefix { get; set; }
        public Guid? CgpGenerationId { get; set; }

        // ══════════════════════════════════════════════════════════════════
        // ✅ NEW — Annex A (2026) alignment fields
        // ══════════════════════════════════════════════════════════════════

        // Section C.1 — distinct from RefCode (which is PDO-internal numbering).
        // Manually entered, format e.g. "Region-LGU-ECA-year-month-000".
        public string? TrackingNumber { get; set; }

        // Section A — Data Privacy Consent (true = Consent, false = Dissent)
        public bool? DataPrivacyConsent { get; set; }

        // Section B — 1 = Local (within PH), 2 = Abroad
        public int? PlaceOfSubmission { get; set; }

        // Section C.9.1 — free-text address detail, alongside the existing
        // PSGC code fields (Region/Province/Municipality/Barangay above)
        public string? HouseNumber { get; set; }
        public string? StreetName { get; set; }
        public string? ZipCode { get; set; }

        // Section C.13 / C.14 — detail text shown only when the corresponding
        // boolean (IsPersonWithDisability / IsIndigenousPeople) is true
        public string? DisabilityType { get; set; }
        public string? EthnicityName { get; set; }

        // Section C.12 — shown only when Citizenship == 2 (Dual)
        public string? DualCitizenshipDetails { get; set; }

        // CivilStatus == 5 (Others) — free-text detail
        // NOTE: CivilStatus codes stay 1=Single, 2=Widowed, 3=Married,
        // 4=CommonLaw (relabeled from "Live-in", same integer — no data fix
        // needed), 5=Others (new).
        public string? CivilStatusOtherDetail { get; set; }

        // Section G — Attestation. No signature image is stored; this is
        // purely an "I attest" flag + the date the encoder recorded it.
        public bool IsSignedDeclaration { get; set; }
        public DateTime? DateSigned { get; set; }
        // ✅ NEW — Liveness verification: periodic proof the grantee is still alive,
        // tracked independently of IsDeceased/DateOfDeath (which records an actual
        // death). Both nullable — unanswered until someone performs the check.
        public bool? IsLivenessVerified { get; set; }
        public DateTime? DateOfLiveness { get; set; }

        // ✅ NEW — Ready for Electronic Fund Transfer: manual checkbox indicating the
        // grantee has met the requirements for EFT payout. Nullable/unbound — purely
        // an informational marker, no automated validation tied to it.
        public bool? IsReadyForEft { get; set; }

        // ── Navigation properties for the new 1:1 / 1:many sub-entities ──
        public BeneficiaryBankAccount? BankAccount { get; set; }
        public BeneficiaryAbroadAddress? AbroadAddress { get; set; }
        public BeneficiaryClaimant? Claimant { get; set; }
        public BeneficiaryVerificationChecklist? VerificationChecklist { get; set; }
        public ICollection<BeneficiaryFamilyMember> FamilyMembers { get; set; } = new List<BeneficiaryFamilyMember>();
        // ✅ NEW — replaces the old single PhoneNumber scalar; grantees can have
        // more than one contact number (their own + a caregiver's, etc.).
        public ICollection<BeneficiaryPhoneNumber> PhoneNumbers { get; set; } = new List<BeneficiaryPhoneNumber>();
        // NEW navigation property, alongside your other sub-entity navs
        public BeneficiaryClaimantBankAccount? ClaimantBankAccount { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = default!;

        public void Update(int? quarter, string? batch, int? refYear, string? refCode, DateTime? dateApplied, DateTime? dateEndorsed, string? batchCode, string? oscaIdNumber, DateTime? oscaIdDateIssued, int? ncscRn, string? lastName, string firstName, string? middleName, string? extensionName,
            DateTime birthDate, int sex, bool? isIndigenousPeople, bool? isPersonWithDisability, int? civilStatus, int? citizenship,
            int region, int province, int municipality, int barangay, bool iscompliant, string validator, DateTime validationDate, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int modeOfPayment,
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
            PayrollQuarter = payrollQuarter;
            FiscalYear = fiscalYear;
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

        public void SetFiscalYear(int? fiscalYear)
        {
            FiscalYear = fiscalYear;
        }

        // ✅ NEW — mirrors the pattern of SetFiscalYear: keeps the new Annex A
        // fields out of the already-long Update() signature. Called explicitly
        // from the service layer alongside Update().
        public void UpdateAnnexADetails(
            string? trackingNumber, bool? dataPrivacyConsent, int? placeOfSubmission,
            string? houseNumber, string? streetName, string? zipCode,
            string? disabilityType, string? ethnicityName, string? dualCitizenshipDetails,
            string? civilStatusOtherDetail, bool isSignedDeclaration, DateTime? dateSigned, bool? isLivenessVerified,
            DateTime? dateOfLiveness, bool? isReadyForEft)
        {
            TrackingNumber = trackingNumber;
            DataPrivacyConsent = dataPrivacyConsent;
            PlaceOfSubmission = placeOfSubmission;
            HouseNumber = houseNumber;
            StreetName = streetName;
            ZipCode = zipCode;
            DisabilityType = disabilityType;
            EthnicityName = ethnicityName;
            DualCitizenshipDetails = dualCitizenshipDetails;
            CivilStatusOtherDetail = civilStatusOtherDetail;
            IsSignedDeclaration = isSignedDeclaration;
            DateSigned = dateSigned;
            IsLivenessVerified = isLivenessVerified;
            DateOfLiveness = dateOfLiveness;
            IsReadyForEft = isReadyForEft;
        }
    }
}