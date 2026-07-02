namespace EcaInformationSystem.Application.DTOs
{
    public class BulkUpdatePaymentStatusRequestDto
    {
        public List<Guid> Ids { get; set; } = new();
        public int PaymentStatus { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? PayrollQuarter { get; set; }
    }
}
