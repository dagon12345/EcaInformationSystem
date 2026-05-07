namespace EcaInformationSystem.Shared.DTOs
{
    public class LiquidationFilterDto
    {
        public int? PsgcCodeProvince { get; set; }
        public DateTime? PaymentDateFrom { get; set; }
        public DateTime? PaymentDateTo { get; set; }
        // PaymentStatus is always 2 — hardcoded in service, not exposed
    }
}