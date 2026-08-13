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
        private readonly IFileShrinkService _shrinkService;
        private readonly IShrinkPreviewCache _previewCache;

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/msword",
            "application/vnd.ms-excel"
        };

        // Final stored-size ceiling — unchanged from before. Files over this get
        // routed through IFileShrinkService (if the type supports it) instead of
        // being flatly rejected.
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        // Absolute cutoff before a shrink is even attempted — protects the server
        // from processing something enormous regardless of shrinkability.
        private const long HardCeilingBytes = 50 * 1024 * 1024; // 50 MB

        public FormDocumentService(IFormDocumentRepository repo, IFormActivityLogRepository logRepo,
            IFileShrinkService shrinkService, IShrinkPreviewCache previewCache)
        {
            _repo = repo;
            _logRepo = logRepo;
            _shrinkService = shrinkService;
            _previewCache = previewCache;
        }

        public async Task<List<FormDocumentDto>> GetAllAsync()
        {
            var docs = await _repo.GetAllForListingAsync();
            return docs.OrderByDescending(d => d.UploadedAt).Select(ToDto).ToList();
        }

        // ✅ NEW — server-side search. Kept simple (in-memory Contains after a
        // narrow DB fetch) since form counts are expected to stay in the low
        // hundreds; revisit with a proper SQL LIKE/full-text index if this grows.
        public async Task<List<FormDocumentDto>> SearchAsync(FormDocumentSearchDto filter)
        {
            var docs = await _repo.GetAllForListingAsync();

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
            string? category, Guid? folderId, ShrinkQuality? shrinkQuality, string userName)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Title is required.");

            if (folderId.HasValue)
                await _repo.EnsureFolderExistsAsync(folderId.Value);

            var (fileBytes, preShrinkSize) = await PrepareFileBytesAsync(file, shrinkQuality);

            var doc = new FormDocument
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                Title = title.Trim(),
                Description = description?.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = fileBytes.LongLength,
                FileData = fileBytes,
                UploadedBy = userName,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddAsync(doc);
            await _repo.SaveChangesAsync();

            var note = preShrinkSize.HasValue
                ? $"Uploaded '{doc.OriginalFileName}' (shrunk from {FormatSize(preShrinkSize.Value)} to {FormatSize(fileBytes.LongLength)})."
                : $"Uploaded '{doc.OriginalFileName}'.";
            await LogAsync("FormUploaded", doc.FolderId, doc.Id, doc.Title, note, userName);

            var dto = ToDto(doc);
            dto.PreShrinkSizeBytes = preShrinkSize;
            return dto;
        }

        public async Task<FormDocumentDto> ReplaceFileAsync(Guid id, IFormFile file, ShrinkQuality? shrinkQuality, string userName)
        {
            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            var (fileBytes, preShrinkSize) = await PrepareFileBytesAsync(file, shrinkQuality);

            var oldFileName = doc.OriginalFileName;

            doc.OriginalFileName = file.FileName;
            doc.ContentType = file.ContentType;
            doc.FileSizeBytes = fileBytes.LongLength;
            doc.FileData = fileBytes;
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            var note = preShrinkSize.HasValue
                ? $"File replaced: '{oldFileName}' → '{doc.OriginalFileName}' (shrunk from {FormatSize(preShrinkSize.Value)} to {FormatSize(fileBytes.LongLength)})."
                : $"File replaced: '{oldFileName}' → '{doc.OriginalFileName}'.";
            await LogAsync("FormFileReplaced", doc.FolderId, doc.Id, doc.Title, note, userName);

            var dto = ToDto(doc);
            dto.PreShrinkSizeBytes = preShrinkSize;
            return dto;
        }

        // Shrinks the file for a chosen quality WITHOUT saving it, so the client
        // can show "estimated result: X MB" before the user commits. The actual
        // shrunk bytes are cached (keyed by the returned token) so the confirming
        // upload doesn't have to re-send or re-shrink the file.
        public async Task<ShrinkPreviewResultDto> PreviewShrinkAsync(IFormFile file, ShrinkQuality quality)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            if (file.Length > HardCeilingBytes)
                throw new InvalidOperationException(
                    $"File exceeds the {FormatSize(HardCeilingBytes)} hard limit — even shrinking can't help a file this large.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new InvalidOperationException(
                    "Unsupported file type. Allowed: PDF, Word (.doc/.docx), Excel (.xls/.xlsx).");

            if (!_shrinkService.CanShrink(file.ContentType))
                throw new InvalidOperationException("This file type can't be automatically shrunk.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var originalBytes = ms.ToArray();

            var shrunk = await _shrinkService.ShrinkAsync(originalBytes, file.ContentType, quality);

            var token = _previewCache.Store(new ShrinkPreviewEntry
            {
                Data = shrunk,
                ContentType = file.ContentType,
                FileName = file.FileName,
                OriginalSizeBytes = originalBytes.LongLength
            });

            return new ShrinkPreviewResultDto
            {
                PreviewToken = token,
                OriginalSizeBytes = originalBytes.LongLength,
                ShrunkSizeBytes = shrunk.LongLength,
                MeetsLimit = shrunk.LongLength <= MaxFileSizeBytes
            };
        }

        // Confirms an already-previewed shrink result — pulls the cached bytes
        // by token and saves them directly, without needing the file re-sent.
        public async Task<FormDocumentDto> UploadFromPreviewAsync(Guid previewToken, string title, string? description,
            string? category, Guid? folderId, string userName)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Title is required.");

            var entry = _previewCache.Take(previewToken)
                ?? throw new InvalidOperationException(
                    "This preview has expired or was already used. Please re-select the file and choose a shrink quality again.");

            if (entry.Data.LongLength > MaxFileSizeBytes)
                throw new InvalidOperationException(
                    $"The previewed result ({FormatSize(entry.Data.LongLength)}) is still over the {FormatSize(MaxFileSizeBytes)} limit. " +
                    "Choose a higher shrinkage level.");

            if (folderId.HasValue)
                await _repo.EnsureFolderExistsAsync(folderId.Value);

            var doc = new FormDocument
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                Title = title.Trim(),
                Description = description?.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                OriginalFileName = entry.FileName,
                ContentType = entry.ContentType,
                FileSizeBytes = entry.Data.LongLength,
                FileData = entry.Data,
                UploadedBy = userName,
                UploadedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddAsync(doc);
            await _repo.SaveChangesAsync();

            await LogAsync("FormUploaded", doc.FolderId, doc.Id, doc.Title,
                $"Uploaded '{doc.OriginalFileName}' (shrunk from {FormatSize(entry.OriginalSizeBytes)} to {FormatSize(entry.Data.LongLength)}).",
                userName);

            var dto = ToDto(doc);
            dto.PreShrinkSizeBytes = entry.OriginalSizeBytes;
            return dto;
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

        // Reads the file, enforces the hard ceiling and type whitelist, and — for
        // anything over the 10 MB stored-size limit — routes it through
        // IFileShrinkService (when the type supports it and a quality was
        // chosen) before it's ever written to the FileData column. Returns the
        // final bytes plus the pre-shrink size (null if no shrink happened).
        private async Task<(byte[] Bytes, long? PreShrinkSize)> PrepareFileBytesAsync(IFormFile file, ShrinkQuality? shrinkQuality)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            if (file.Length > HardCeilingBytes)
                throw new InvalidOperationException(
                    $"File exceeds the {FormatSize(HardCeilingBytes)} hard limit — even shrinking can't help a file this large.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                throw new InvalidOperationException(
                    "Unsupported file type. Allowed: PDF, Word (.doc/.docx), Excel (.xls/.xlsx).");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var originalBytes = ms.ToArray();

            if (originalBytes.LongLength <= MaxFileSizeBytes)
                return (originalBytes, null);

            if (!_shrinkService.CanShrink(file.ContentType))
                throw new InvalidOperationException(
                    $"File exceeds the {FormatSize(MaxFileSizeBytes)} limit and this file type can't be automatically shrunk. " +
                    "Please reduce it manually, or save it as a PDF/DOCX/XLSX first.");

            if (shrinkQuality is null)
                throw new InvalidOperationException(
                    $"File exceeds the {FormatSize(MaxFileSizeBytes)} limit — please choose a shrink quality (High/Medium/Low) to continue.");

            var shrunk = await _shrinkService.ShrinkAsync(originalBytes, file.ContentType, shrinkQuality.Value);

            if (shrunk.LongLength > MaxFileSizeBytes)
                throw new InvalidOperationException(
                    $"Even at {shrinkQuality.Value} quality, this file could not be reduced below {FormatSize(MaxFileSizeBytes)} " +
                    $"(best result: {FormatSize(shrunk.LongLength)}). Try a smaller or simpler file.");

            return (shrunk, originalBytes.LongLength);
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
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