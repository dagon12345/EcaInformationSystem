using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    // Id is null for a row an admin just added in the editor and hasn't saved
    // yet — the repository creates a new entry for those. A non-null Id that
    // matches an existing row updates it in place (including renaming its
    // UacsCode/UacsName for catalog revisions); an existing row whose Id is
    // absent from the submitted list is deleted, so removing a row from the
    // grid and saving deletes it.
    public record WfpEcaLineInput(Guid? Id, string UacsCode, string UacsName, decimal Allotment, decimal Obligation, string? Remarks);

    // Added/Updated/Removed let the caller log exactly what changed in this
    // save. "Updated" only includes rows whose actual field values differ
    // from what was stored — a save triggered purely by reordering rows
    // (SortOrder-only) doesn't count as an edit, so it's excluded here.
    public record WfpEcaSaveResult(List<WfpEcaEntry> AllEntries, List<WfpEcaEntry> Added, List<WfpEcaEntry> Updated, List<WfpEcaEntry> Removed);

    public interface IWfpEcaRepository
    {
        Task<List<WfpEcaEntry>> GetAsync(int regionCode, int fiscalYear);

        Task<WfpEcaSaveResult> UpsertAsync(int regionCode, int fiscalYear, IEnumerable<WfpEcaLineInput> lines, string userName);
    }
}
