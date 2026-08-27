namespace EcaInformationSystem.Domain.Entities
{
    // Records a "Known Duplicate" event — a 100% exact match (same
    // LastName+FirstName+MiddleName+BirthDate) that was deliberately saved
    // instead of hard-blocked at Create/Import. Deliberately its own table
    // rather than flags on BeneficiaryInformation: it must never show up in
    // the grid, a list, or any statistics/count — only inside the specific
    // grantee's own record, as history.
    public class BeneficiaryDuplicateHistory
    {
        public Guid Id { get; set; }

        // The newer record — the one that was saved despite matching an
        // existing grantee exactly.
        public Guid BeneficiaryInformationId { get; set; }

        // The pre-existing record it exactly matched.
        public Guid DuplicateOfId { get; set; }

        // "Create" (manual entry, BeneficiaryForm.razor) or "Import" (Excel
        // bulk import) — which flow produced this duplicate.
        public string Source { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }
}
