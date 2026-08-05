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
                NoteText = m.NoteText
            }).ToList();
        }

        public async Task SetAsync(Guid userId, DateTime date, string markType, string? noteText, string? updatedByName)
        {
            var mark = await _repository.GetAsync(userId, date);
            if (mark is null)
            {
                mark = new DtrDayMark { UserId = userId, Date = date.Date };
                await _repository.AddAsync(mark);
            }

            mark.MarkType = markType;
            mark.NoteText = markType == "Note" ? noteText : null;
            mark.UpdatedAt = DateTime.Now;
            mark.UpdatedByName = updatedByName;

            await _repository.SaveChangesAsync();
        }

        public async Task ClearAsync(Guid userId, DateTime date)
        {
            var mark = await _repository.GetAsync(userId, date);
            if (mark is null)
                return;

            await _repository.RemoveAsync(mark);
            await _repository.SaveChangesAsync();
        }
    }
}
