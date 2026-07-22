namespace EcaInformationSystem.Domain.Entities
{
    public class Log
    {
        public Guid Id { get; set; }

        // ✅ CHANGED — nullable. System-level events (exports, downloads,
        // uploads, logins) aren't tied to one beneficiary, so this FK is no
        // longer mandatory. Existing per-beneficiary log calls are unaffected —
        // passing a Guid still implicitly satisfies Guid?.
        public Guid? BeneficiaryInformationId { get; set; }

        public string Activity { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // ✅ NEW — lets the Logs UI filter by event type. Defaults to
        // "Beneficiary" so every existing call site (AddLogAsync inside
        // BeneficiaryInformationService) keeps classifying correctly without
        // any code change on its end.
        public string Category { get; set; } = "Beneficiary";
    }
}