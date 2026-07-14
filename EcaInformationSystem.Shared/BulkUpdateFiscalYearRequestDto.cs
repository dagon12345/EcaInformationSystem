public class BulkUpdateFiscalYearRequestDto
{
    public List<Guid> Ids { get; set; } = new();
    public int? FiscalYear { get; set; }
    public Dictionary<Guid, byte[]>? RowVersions { get; set; }
}