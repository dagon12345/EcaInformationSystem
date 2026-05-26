namespace EcaInformationService.Shared.DTOs
{
    public class BulkUpdateEligibilityBatchCodeRequestDto
    {
        public List<Guid> Ids {get; set;} = new();
        public bool? IsEligible { get; set; } // null = don't change
        public string? BatchCode { get; set; } // null = don't change, "" = clear
    }
}