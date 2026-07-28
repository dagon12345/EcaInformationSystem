using System.Text.Json;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services
{
    public class StickyNoteService : IStickyNoteService
    {
        private readonly IStickyNoteRepository _repo;

        private static readonly HashSet<string> AllowedColors = new(StringComparer.OrdinalIgnoreCase)
        {
            "yellow", "pink", "blue", "green", "purple", "orange"
        };

        public StickyNoteService(IStickyNoteRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<StickyNoteDto>> GetMineAsync(Guid userId)
        {
            var notes = await _repo.GetByUserAsync(userId);
            return notes.Select(ToDto).ToList();
        }

        public async Task<StickyNoteDto> UpsertAsync(Guid userId, StickyNoteUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

            var type = dto.Type == "Checklist" ? "Checklist" : "Note";
            var color = AllowedColors.Contains(dto.Color) ? dto.Color.ToLowerInvariant() : "yellow";

            StickyNote note;
            if (dto.Id.HasValue)
            {
                note = await _repo.GetByIdAsync(dto.Id.Value, userId)
                    ?? throw new KeyNotFoundException("Note not found.");
                note.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                note = new StickyNote
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                await _repo.AddAsync(note);
            }

            note.Title = dto.Title.Trim();
            note.Type = type;
            note.Color = color;

            if (type == "Checklist")
            {
                note.Content = null;
                note.ChecklistItemsJson = JsonSerializer.Serialize(dto.ChecklistItems ?? new());
            }
            else
            {
                note.Content = dto.Content?.Trim();
                note.ChecklistItemsJson = null;
            }

            await _repo.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task DeleteAsync(Guid userId, Guid noteId)
        {
            var note = await _repo.GetByIdAsync(noteId, userId);
            if (note is null) return; // already gone — deleting is idempotent from the caller's view

            _repo.Remove(note);
            await _repo.SaveChangesAsync();
        }

        private static StickyNoteDto ToDto(StickyNote n) => new()
        {
            Id = n.Id,
            Title = n.Title,
            Type = n.Type,
            Content = n.Content,
            ChecklistItems = string.IsNullOrWhiteSpace(n.ChecklistItemsJson)
                ? new List<StickyNoteChecklistItemDto>()
                : JsonSerializer.Deserialize<List<StickyNoteChecklistItemDto>>(n.ChecklistItemsJson) ?? new(),
            Color = n.Color,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };
    }
}
