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

            // ✅ One-time, self-healing migration — the "Checklist" note type
            // (a separate ChecklistItemsJson column) was retired in favor of
            // plain "- [ ] "/"- [x] " lines inside Content, rendered/toggled
            // client-side. Any legacy checklist note still carrying the old
            // Type gets its items folded into Content here, the first time
            // it's fetched after the change, and persisted so it only ever
            // happens once per note.
            var legacyNotes = notes.Where(n => n.Type == "Checklist").ToList();
            if (legacyNotes.Count > 0)
            {
                foreach (var note in legacyNotes)
                {
                    note.Content = ConvertLegacyChecklistToContent(note.ChecklistItemsJson);
                    note.Type = "Note";
                    note.ChecklistItemsJson = null;
                }

                await _repo.SaveChangesAsync();
            }

            return notes.Select(ToDto).ToList();
        }

        public async Task<StickyNoteDto> UpsertAsync(Guid userId, StickyNoteUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

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
            note.Type = "Note";
            note.Color = color;
            note.Content = dto.Content?.Trim();
            note.ChecklistItemsJson = null;

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

        private static string ConvertLegacyChecklistToContent(string? checklistItemsJson)
        {
            if (string.IsNullOrWhiteSpace(checklistItemsJson))
                return string.Empty;

            List<LegacyChecklistItem>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<LegacyChecklistItem>>(checklistItemsJson);
            }
            catch (JsonException)
            {
                return string.Empty; // corrupt legacy data — nothing sensible to recover
            }

            if (items is null || items.Count == 0) return string.Empty;

            return string.Join('\n', items.Select(i =>
                $"- [{(i.IsChecked ? "x" : " ")}] {i.Text}"));
        }

        private class LegacyChecklistItem
        {
            public string Text { get; set; } = string.Empty;
            public bool IsChecked { get; set; }
        }

        private static StickyNoteDto ToDto(StickyNote n) => new()
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            Color = n.Color,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };
    }
}
