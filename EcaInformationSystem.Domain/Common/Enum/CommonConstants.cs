using System.Dynamic;

namespace EcaInformationSystem.Domain.Common.Enum
{
    public static class CommonConstants
    {
        // Add to CommonConstants.cs
        public const string DuplicateScanCacheVersionKey = "dup_scan_version";
        public const string EligibilityRemarks = "Eligibility Remarks";
        public const string SummaryCacheVersionKey = "beneficiary-summary-version";
        public const string NoDataAvailableToExport = "No data available to export.";
        public const string Grantees = "Grantees";
        public const string NCSC = "NATIONAL COMMISSION OF SENIOR CITIZENS";
        public const string Act = "Expanded Centenarian Act";
        public const string RegionalOfficeCaraga = "Regional Office Caraga";
        public const string ListOfValidatedPaid = "List of Validated/Paid Beneficiaries for FY";
        public const string RepublicOfThePhilippines = "Republic of the Philippines";
        public const string NcscAddress = "The Upper Class Tower, Quezon Avenue cor. Scout Reyes St., Diliman, Quezon City 1117";
        public const string NcscWebsite = "Official website: www.ncsc.gov.ph";
        //Headers
        public const string BatchCode = "BATCH CODE";
        public const string Number = "No.";
        public const string OscaIdNumber = "OSCA ID NUMBER";
        public const string OscaIdDateIssued = "OSCA ID DATE ISSUED";
        public const string IP = "INDIGENOUS PEOPLE";
        public const string PWD = "PERSON WITH DISABILITY";
        public const string NcscAssessment = "NCSC ASSESSMENT";
        public const string NcscRrn = "NCSC RRN";
        public const string LastName = "LAST NAME";
        public const string FirstName = "FIRST NAME";
        public const string MiddleName = "MIDDLE NAME";
        public const string Extension = "EXTENSION";
        public const string FullNameOfBeneficiary = "FULL NAME OF BENEFICIARY";
        public const string Ext = "Ext.";
        public const string BirthMonth = "BIRTH MONTH";
        public const string BirthDay = "BIRTH DAY";
        public const string BirthYear = "BIRTH YEAR";
        public const string PayrollBirthdate = "BDate\n(mm/dd/yyyy)";
        public const string Age = "AGE";
        public const string Sex = "SEX";
        public const string Region = "REGION";
        public const string Province = "PROVINCE";
        public const string Municipality = "MUNICIPALITY/CITY";
        public const string Barangay = "BARANGAY";
        public const string ComplianceToDocumentaryRequirements = "COMPLIANCE TO DOCUMENTARY REQUIREMENTS";
        public const string NameOfValidator = "NAME OF VALIDATOR";
        public const string ValidationDate = "VALIDATION DATE";
        public const string Remarks = "REMARKS";
        public const string ContactNumber = "CONTACT NUMBER";
        public const string DateApplied = "DATE APPLIED";
        public const string DateEndorsed = "DATE ENDORSED";
        public const string Compliance = "COMPLIANCE";
        public const string Male = "MALE";
        public const string Female = "FEMALE";
        public const string BirthDate = "BIRTH DATE";
        public const string CivilStatus = "CIVIL STATUS";
        public const string Citizenship = "CITIZENSHIP";
        public const string Validator = "VALIDATOR";
        public const string Compliant = "COMPLIANT";
        public const string NonCompliant = "NON-COMPLIANT";
        public const string PaymentStatus = "PAYMENT STATUS";
        public const string ModeOfPayment = "MODE OF PAYMENT";
        public const string PaymentDate = "PAYMENT DATE";
        public const string EnterName = "(Enter Name)";
        public const string EnterPosition = "(Enter Position)";
        public const string SignatureOverPrintedName = "Signature over Printed Name";
        public const string PrintedNameAndSignatureOf = "Printed Name and Signature of";
        public const string Thumbmark = "Thumbmark";
        public const string PreparedBy = "Prepared by:";
        public const string NotedBy = "Noted by:";
        public const string ApprovedBy = "Approved by:";
        public const string IsDeceased = "IS DECEASED";
        public const string Page = "Page ";
        public const string Of = " of ";
        public const string DuplicateFound = "Duplicate beneficiary found. Same name, birth date, OSCA ID, and RRN already exist.";
        public const string CreatedBeneficiary = "Created beneficiary record for";
        public const string GranteeNotFound = "Grantee not found";
        public const string AssessmentRemarks = "Compliant Remarks";
        public const string RemarkCategory = "Remark Category";
        public const string LogSoftDelete = "Soft deleted beneficiary record";
        public const string Unknown = "Unknown";
        public const string NoRecordsSelected = "No records selected";
        public const string InvalidPaymentStatus = "Invalid payment status. Must be 1 (Unpaid) or 2 (Paid).";
        public const string PaymentDateRequiredForPaidStatus = "Payment date is required when payment status is set to Paid.";
        public const string PaidDate = "Paid (Date:";

        public const string Paid = "Paid";
        public const string Unpaid = "Unpaid";
        public const string Pending = "Pending";
        public const string BulkPaymentStatusUpdatedTo = "Bulk payment status updated to:";
        public const string UpdatedBeneficiaryChanges = "Updated beneficiary record with changes:";
        public const string NoneOfTheRecordsFound = "None of the selected records were found.";
        public const string NoBatch = "NO BATCH";

        //Civil status
        public const string Single = "SINGLE";
        public const string Married = "MARRIED";
        public const string Widowed = "WIDOWED";
        public const string LiveIn = "LIVE IN";
        //Citizenship
        public const string Filipino = "FILIPINO";
        public const string DualCitizenship = "DUAL CITIZENSHIP";
        //Mode of payment
        public const string CashAdvanceBySdo = "Cash Advance by SDO";
        public const string BankTransfer = "Bank Transfer";
        //Remark Category
        public const string DeceasedPriorReachingMilestoneAge = "Deceased prior reaching milestone age";
        public const string OutOfTownOrCountry = "Out of town/Country";
        public const string IncompleteRequiredDocuments = "Incomplete required documents";
        public const string InconsistentDocuments = "Inconsistent documents";
        public const string CannotBeReachedOrLocated = "Cannot be reached/Located";
        public const string DidNotReachTheMilestoneAge = "Did not reach the milestone age";
        public const string LackingOfDocumentsOrRequirements = "Lacking of documents/Requirements";
        public const string ForCorrection = "For Correction";
        public const string Waived = "Waived";
        public const string LackingProofOfRelationship = "Lacking Proof of Relationship";
        public const string NoShow = "No show";
        public const string DoubleApplicationWithDifferentSurnameUsed = "Double Application with Different Surename used";
        public const string ForCGDIslandMunicipality = "For CGD Island Municipality";
        //Fonts
        public const string Arial = "Arial";
        public const string TimesNewRoman = "Times New Roman";
        public const string Appendix40 = "Appendix  40";

        public const string RegionalOfficeProvinceOf = "Regional Office XIII, Province of";
        public const string CashGiftPayroll = "Cash Gift Payroll";
        public const string Apurpose = "A. PURPOSE:";
        public const string PayrollPurpose = "Cash gift payout for Octogenarians, Nonagenarians, and Centenarians pursuant to R.A. No. 11982 - Expanded Centenarian Act.";
        public const string CgpNo = "CGP No.:";
        public const string DefaultOrderNo = "0001";
        public const string Amount = "Amount";
        public const string AmountReceived = "Amount\nReceived";
        public const string BeneficiaryAuthRepresentative = "Beneficiary / Authorized\nRepresentative";
        public const string ForAuthRep = "For Authorized Representative\n(Relationship/Witness)";
        public const string DateOfDeath = "Date of Death";
        public const string DateOfApplication = "Date of\nApplication";
        public const string DateReceived = "Date\nReceived";
        public const string NumberFormat = "#,##0.00";
        public const string SubTotal = "SUBTOTAL";
        public const string ImportTemplateForFy = "Import Template for FY";
        public const string Instructions = "Instructions";

        public const string WorksheetNotFound = "Worksheet not found.";
        public const string Yes = "YES";
        public const string No = "No";
        public const string NoUpperCase = "NO";
        public const string Duplicate = "Duplicate";
        public const string None = "N/A";
        public const string City = "City";

        public const string CityOf = "City of";
        public const string MunicipalityOf = "Municipality of";
        public const string ProvinceOf = "Province of";

        //Colors
        public const string NavyColor = "#1F3864";
        public const string AmberYellow = "#FFF3CD";

        public const string RecordNotFoundInDatabase = "Record not found in database. Update not allowed.";
        public const string Record = "Record";
        public const string General = "General";
        public const string ExcelUpdate = "Excel Update:";
        public const string Eligible = "ELIGIBLE";
        public const string Ineligible = "INELIGIBLE";

        //Excel error messages
        public const string InvalidExcelUploaded = "Invalid Excel file uploaded. Please ensure the file is in the correct format and try again.";
        public const string InvalidExcelFile = "Invalid Excel file. Please ensure the file is in the correct format and try again.";
        public const string OnlyExcelFilesAllowed = "Only Excel files (.xlsx) are allowed. Please upload a valid Excel file.";
        public const string ExcelFileExtension = ".xlsx";
        public const string PleaseSelectAWorksheet = "Please select a worksheet from the uploaded Excel file.";
        public const string ExcelSheet1DoesNotContain = "Sheet1 does not contain data rows.";
        public const string InvalidFileName = "Invalid file name.";

        //Other Messages
        public const string FirstNameRequired = "First name is required.";
        public const string InvalidBirthDate = "Invalid Birth Date from Month/Day/Year values.";
        public const string InvalidDateFormat = "Invalid date format. Example valid format: 'March 17, 2026'.";
        public const string InvalidRrn = "Invalid NCSC RRN.";
        public const string RemoveSpecialCharactersFromName = "Please remove special characters from the name fields.";
        public const string RegionNotFound = "Region not found.";
        public const string ProvinceNotFound = "Province not found.";
        public const string MunicipalityNotFound = "Municipality/City not found.";
        public const string BarangayNotFound = "Barangay not found.";
        public const string PossibleMatch = "Possible match:";
        public const string CheckSpelling = "Check spelling and spacing.";
        public const string NcscAssessmentNotFound = "Please enter Eligible or InEligible only.";
        public const string DuplicateRecordFound = "Duplicate record found within the uploaded file.";
        public const string DuplicateRecordExistInDatabase = "Duplicate record already exists in the database.";
        public const string ImportedBeneficiaryFromExcel = "Imported beneficiary record from Excel";
        public const string NoPaidRecordsMessage = "No paid records found for the selected filters.";

        public const string RA = "RA 11982 Cash Gift Distribution for the";
        public const string CY = "CY";
        public const string ETAL = "ET AL.";
        public const string final = "final";
        public const string CashDrPage = "CashDR_Page";

        public const string CashDisbursementsRecord = "CASH DISBURSEMENTS RECORD";
        public const string SCDSP = "SENIOR CITIZENS WELFARE DEVELOPMENT SERVICES PROGRAM";
        public const string Implentation = "Implementation of the Expanded Centenarian Act Pursuant to R.A. 11982";
        public const string Unit = "Region / Organization Unit:";
        public const string OrgCode = "New ORG Code:";
        public const string FundCluster = " Fund Cluster : ";
        public const string SheetNo = "Sheet No. : ";
        public const string AccountableOfficer = "Accountable Officer";
        public const string OfficialDesignation = "Official Designation";
        public const string Station = "Station";

        //CDR
        public const string Date = "Date";
        public const string Column2Header = "ADA/Check/\nDV/Payroll/\nReference No. ";
        public const string Payee = "Payee";
        public const string UACS = "UACS Object Code ";
        public const string NatureOfPayment = "Nature of Payment";
        public const string CashAdvanceReceived = "Cash Advance Received/ (Refunded)";
        public const string Disbursement = "Disbursement";
        public const string Balance = "Cash Advance Balance";
        public const string Dv = "DV:";
        public const string DatePlaceHolder = "MMMM dd, yyyy";
        public const string DatePlaceHolderUpperCase = "MMMM D, YYYY";
        public const string ConstantUacs = "50214990-00";
        public const string Cluster = "NCSC CLUSTER 8 RO 13 (CARAGA)";
        public const string TreasuryConstant = "1990103000";
        public const string Unclaimed = "UNCLAIMED RA 11982 CASH GIFTS";
        public const string CertificationSpaced = "C E R T I F I C A T I O N";
        public const string Null = "null";
        public const string BeneficiaryPaginated = "beneficiary-paginated";
        public const string BeneficiarySummary = "beneficiary-summary";
        public const string NameAndSignatureOfDisbursingOfficer = "Name and Signature of Disbursing Officer";
        public const string V1 = "v1";
        public const string Status = "Status";
        public const string Default = "default";
    }
}
