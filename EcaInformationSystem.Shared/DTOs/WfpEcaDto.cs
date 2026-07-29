namespace EcaInformationSystem.Shared.DTOs
{
    public class WfpEcaLineDto
    {
        // Null means this line hasn't been saved yet — either it's from the
        // default catalog template shown before anything's been entered, or
        // it's a row the admin just added in the editor this session.
        public Guid? Id { get; set; }
        public string UacsCode { get; set; } = string.Empty;
        public string UacsName { get; set; } = string.Empty;
        public decimal Allotment { get; set; }
        public decimal Obligation { get; set; }
        public decimal Balance { get; set; }
        public string? Remarks { get; set; }
    }

    public class WfpEcaDto
    {
        public int RegionCode { get; set; }
        public string? RegionName { get; set; }
        public int FiscalYear { get; set; }

        public List<WfpEcaLineDto> Lines { get; set; } = new();

        public decimal TotalAllotment { get; set; }
        public decimal TotalObligation { get; set; }
        public decimal TotalBalance { get; set; }

        // Obligation / Allotment * 100, 0 when Allotment is 0 — avoids a
        // divide-by-zero before any figures have been entered for the year.
        public decimal ObligatedPercentage { get; set; }

        public DateTime? DateSet { get; set; }
        public string? SetBy { get; set; }
        public DateTime? DateModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public class UpsertWfpEcaLineDto
    {
        // Null for a row added in the editor this session (not yet persisted);
        // set to an existing row's Id to update it in place, including renaming
        // UacsCode/UacsName for catalog revisions.
        public Guid? Id { get; set; }
        public string UacsCode { get; set; } = string.Empty;
        public string UacsName { get; set; } = string.Empty;
        public decimal Allotment { get; set; }
        public decimal Obligation { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpsertWfpEcaDto
    {
        public int FiscalYear { get; set; }
        public List<UpsertWfpEcaLineDto> Lines { get; set; } = new();
    }
}
