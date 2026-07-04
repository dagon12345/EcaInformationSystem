public class LogEntryDto
{
    public Guid Id { get; set; }
    public Guid BeneficiaryInformationId { get; set; }
    public string BeneficiaryName { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}