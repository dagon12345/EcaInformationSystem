namespace EcaInformationSystem.Shared.DTOs
{
    public class CoeSettingsDto
    {
        public List<Guid> Ids { get; set; } = new();

        //Header text
        public string OfficeAddress { get; set; } = "Ground Floor, Samping Avenue, J.C. Aquino Avenue, Butuan City 8600";
        public string OfficeEmail { get; set; } = "ro13@ncsc.gov.ph";
        public string OfficeWebsite { get; set; } = "www.ncsc.gov.ph";

        //Certification body text - dynamic amount, in case of policy change
        public decimal CashGiftAmount { get; set; } = 10000.00m;

        //Signatories - dynamic, matches "Prepared by / Approved by"
        public string PreparedByName { get; set; } = "LALEINE R. BANZON";
        public string PreparedByPosition { get; set; } = "Project Development Officer I";
        public string ApprovedByName { get; set; } = "CESAR A. ADEGUE IV, PHD, CESE";
        public string ApprovedByPosition { get; set; } = "Concurrent Regional Director";
    }

    //One certificate = one municipality. The PDF groups multiple
    //Milestone-year tables under one municipality header/footer.
    public class CoePreviewGroupDto
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public string ProvinceName { get; set; } = string.Empty;
        public List<CoeYearGroupDto> YearGroups  { get; set; } = new();
        public int TotalRecords => YearGroups.Sum(y => y.Records.Count);
    }

    public class CoeYearGroupDto
    {
        public int MilestoneYear { get; set; }
        public List<CoeRecordRowDto> Records { get; set; } = new();
    }
    public class CoeRecordRowDto
    {
        public int RowNumber { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int Age { get; set; }
        public string BarangayName { get; set; } = string.Empty;
    }
}