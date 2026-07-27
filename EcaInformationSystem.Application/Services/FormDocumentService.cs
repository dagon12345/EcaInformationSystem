using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Services
{
    public class FormDocumentService : IFormDocumentService
    {
        private readonly IFormDocumentRepository _repo;
        private readonly IFormActivityLogRepository _logRepo;

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/msword",
            "application/vnd.ms-excel"
        };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public FormDocumentService(IFormDocumentRepository repo, IFormActivityLogRepository logRepo)
        {
            _repo = repo;
            _logRepo = logRepo;
        }

        public async Task<List<FormDocumentDto>> GetAllAsync()
        {
            var docs = await _repo.GetAllAsync();
            return docs.OrderByDescending(d => d.UploadedAt).Select(ToDto).ToList();
        }

        // ✅ NEW — server-side search. Kept simple (in-memory Contains after a
        // narrow DB fetch) since form counts are expected to stay in the low
        // hundreds; revisit with a proper SQL LIKE/full-text index if this grows.
        public async Task<List<FormDocumentDto>> SearchAsync(FormDocumentSearchDto filter)
        {
            var docs = await _repo.GetAllAsync();

            IEnumerable<FormDocument> query = docs;

            if (filter.UncategorizedOnly)
            {
                query = query.Where(d => d.FolderId == null);
            }
            else if (filter.FolderId.HasValue)
            {
                query = query.Where(d => d.FolderId == filter.FolderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLowerInvariant();
                query = query.Where(d =>
                    d.Title.ToLowerInvariant().Contains(term) ||
                    (d.Description != null && d.Description.ToLowerInvariant().Contains(term)) ||
                    (d.Category != null && d.Category.ToLowerInvariant().Contains(term)) ||
                    d.OriginalFileName.ToLowerInvariant().Contains(term));
            }

            query = filter.SortBy == "Name"
                ? (filter.SortAscending
                    ? query.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(d => d.Title, StringComparer.OrdinalIgnoreCase))
                : (filter.SortAscending
                    ? query.OrderBy(d => d.UploadedAt)
                    : query.OrderByDescending(d => d.UploadedAt));

            return query.Select(ToDto).ToList();
        }

        public async Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(Guid id)
        {
            var doc = await _repo.GetByIdAsync(id);
            if (doc == null) return null;
            return (doc.FileData, doc.ContentType, doc.OriginalFileName);
        }

        public async Task<FormDocumentDto> UploadAsync(IFormFile file, string title, string? description,
            string? category, Guid? folderId, string userName)
        {
            ValidateFile(file);

            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Title is required.");

            if (folderId.HasValue)
                await _repo.EnsureFolderExistsAsync(folderId.Value);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var doc = new FormDocument
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                Title = title.Trim(),
                Description = description?.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                FileData = ms.ToArray(),
                UploadedBy = userName,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddAsync(doc);
            await _repo.SaveChangesAsync();

            await LogAsync("FormUploaded", doc.FolderId, doc.Id, doc.Title,
                $"Uploaded '{doc.OriginalFileName}'.", userName);

            return ToDto(doc);
        }

        public async Task<FormDocumentDto> ReplaceFileAsync(Guid id, IFormFile file, string userName)
        {
            ValidateFile(file);

            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var oldFileName = doc.OriginalFileName;

            doc.OriginalFileName = file.FileName;
            doc.ContentType = file.ContentType;
            doc.FileSizeBytes = file.Length;
            doc.FileData = ms.ToArray();
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            await LogAsync("FormFileReplaced", doc.FolderId, doc.Id, doc.Title,
                $"File replaced: '{oldFileName}' → '{doc.OriginalFileName}'.", userName);

            return ToDto(doc);
        }

        public async Task UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto, string userName)
        {
            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

            if (dto.FolderId.HasValue)
                await _repo.EnsureFolderExistsAsync(dto.FolderId.Value);

            var oldFolderId = doc.FolderId;

            doc.Title = dto.Title.Trim();
            doc.Description = dto.Description?.Trim();
            doc.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
            doc.FolderId = dto.FolderId;
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            var moveNote = oldFolderId != dto.FolderId ? " (moved to a different folder)" : "";
            await LogAsync("FormUpdated", doc.FolderId, doc.Id, doc.Title,
                $"Metadata updated{moveNote}.", userName);
        }

        // ✅ CAUTION path — file deletion is soft-delete + logged, same principle
        // as folder deletion. Nothing about a form gateway delete is silent.
        public async Task DeleteAsync(Guid id, string userName)
        {
            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            doc.IsDeleted = true;
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            await LogAsync("FormDeleted", doc.FolderId, doc.Id, doc.Title,
                $"Deleted '{doc.OriginalFileName}'.", userName);
        }

        private async Task LogAsync(string action, Guid? folderId, Guid? documentId,
            string targetName, string details, string userName)
        {
            await _logRepo.AddAsync(new FormActivityLog
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                FormDocumentId = documentId,
                Action = action,
                TargetName = targetName,
                Details = details,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException("File exceeds the 10 MB limit.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new InvalidOperationException(
                    "Unsupported file type. Allowed: PDF, Word (.doc/.docx), Excel (.xls/.xlsx).");
        }

        private static FormDocumentDto ToDto(FormDocument d) => new()
        {
            Id = d.Id,
            FolderId = d.FolderId,
            FolderName = d.Folder?.Name,
            Title = d.Title,
            Description = d.Description,
            Category = d.Category,
            OriginalFileName = d.OriginalFileName,
            ContentType = d.ContentType,
            FileSizeBytes = d.FileSizeBytes,
            UploadedBy = d.UploadedBy,
            UploadedAt = d.UploadedAt,
            UpdatedBy = d.UpdatedBy,
            UpdatedAt = d.UpdatedAt
        };
    }
}