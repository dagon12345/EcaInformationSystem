public class BulkUpdatePayrollQuarterRequestDto
{
    public List<Guid> Ids { get; set; } = new();
    public int? PayrollQuarter { get; set; }
    public Dictionary<Guid, byte[]>? RowVersions { get; set; }
}