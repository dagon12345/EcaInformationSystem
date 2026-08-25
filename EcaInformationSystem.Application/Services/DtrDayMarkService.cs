using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Services
{
    public class DtrDayMarkService : IDtrDayMarkService
    {
        private readonly IDtrDayMarkRepository _repository;

        public DtrDayMarkService(IDtrDayMarkRepository repository) => _repository = repository;

        public async Task<List<DtrDayMarkDto>> GetForUserAsync(Guid userId, DateTime start, DateTime end)
        {
            var marks = await _repository.GetForUserAsync(userId, start, end);
            return marks.Select(m => new DtrDayMarkDto
            {
                Date = m.Date,
                MarkType = m.MarkType,
                NoteText = m.NoteText,
                Slot = m.Slot,
                SlotEnd = m.SlotEnd
            }).ToList();
        }

        public async Task SetAsync(Guid userId, DateTime date, string markType, string? noteText, string? slot, string? slotEnd, string? updatedByName)
        {
            var existingForDate = await _repository.GetAllForDateAsync(userId, date);

            if (slot is null)
            {
                // Whole-day mark — clears every per-column note on this date,
                // same "day is one thing" rule as before.
                await _repository.RemoveRangeAsync(existingForDate.Where(m => m.Slot is not null).ToList());
            }
            else
            {
                // A ranged note (Slot..SlotEnd) must not overlap any OTHER
                // existing per-column note on this date — e.g. setting
                // AmOut→PmOut has to absorb/replace a pre-existing lone PmOut
                // note, not leave it dangling as an orphaned second mark for
                // a column this new range now covers. Also clears the
                // whole-day mark, if any (mutual exclusion, same as above).
                var effectiveEnd = slotEnd ?? slot;
                var newStart = DtrSlotOrder.IndexOf(slot);
                var newEnd = DtrSlotOrder.IndexOf(effectiveEnd);

                var toRemove = existingForDate.Where(m =>
                    m.Slot is null ||
                    (m.Slot != slot && DtrSlotOrder.Overlaps(
                        newStart, newEnd,
                        DtrSlotOrder.IndexOf(m.Slot), DtrSlotOrder.IndexOf(m.SlotEnd ?? m.Slot))));
                await _repository.RemoveRangeAsync(toRemove.ToList());
            }

            var mark = await _repository.GetAsync(userId, date, slot);
            if (mark is null)
            {
                mark = new DtrDayMark { UserId = userId, Date = date.Date, Slot = slot };
                await _repository.AddAsync(mark);
            }

            mark.MarkType = markType;
            mark.NoteText = markType == "Note" ? noteText : null;
            mark.SlotEnd = slot is not null ? (slotEnd ?? slot) : null;
            mark.UpdatedAt = DateTime.Now;
            mark.UpdatedByName = updatedByName;

            await _repository.SaveChangesAsync();
        }

        public async Task ClearAsync(Guid userId, DateTime date, string? slot)
        {
            var mark = await _repository.GetAsync(userId, date, slot);
            if (mark is null)
                return;

            await _repository.RemoveAsync(mark);
            await _repository.SaveChangesAsync();
        }
    }
}
