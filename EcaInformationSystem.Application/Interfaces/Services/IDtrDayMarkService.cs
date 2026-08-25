using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDtrDayMarkService
    {
        Task<List<DtrDayMarkDto>> GetForUserAsync(Guid userId, DateTime start, DateTime end);

        // slot: null sets the WHOLE-DAY mark (Wfh/Holiday/full-day Note) and
        // clears any per-column notes on that date. A non-null slot ("AmIn"|
        // "AmOut"|"PmIn"|"PmOut") sets/updates a Note starting at that
        // column — slotEnd (same 4 values, or null to mean "same as slot")
        // extends it across consecutive columns, e.g. slot="AmOut",
        // slotEnd="PmOut" covers AmOut+PmIn+PmOut as one note while AmIn
        // keeps its own real punch cell. Setting a slot/range note clears
        // the date's whole-day mark and any other note it now overlaps.
        Task SetAsync(Guid userId, DateTime date, string markType, string? noteText, string? slot, string? slotEnd, string? updatedByName);

        // slot identifies exactly which mark to remove: null = the whole-day
        // mark, a specific slot = just that column's note. Does not touch
        // any other mark on the same date.
        Task ClearAsync(Guid userId, DateTime date, string? slot);
    }
}
