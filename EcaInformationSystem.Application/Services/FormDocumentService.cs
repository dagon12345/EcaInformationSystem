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

        // Keep this list tight — forms are official documents, not general uploads.
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",       // .xlsx
            "application/msword",
            "application/vnd.ms-excel"
        };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public FormDocumentService(IFormDocumentRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<FormDocumentDto>> GetAllAsync()
        {
            var docs = await _repo.GetAllAsync();
            return docs.OrderByDescending(d => d.UploadedAt).Select(ToDto).ToList();
        }

        public async Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(Guid id)
        {
            var doc = await _repo.GetByIdAsync(id);
            if (doc == null) return null;
            return (doc.FileData, doc.ContentType, doc.OriginalFileName);
        }

        public async Task<FormDocumentDto> UploadAsync(IFormFile file, string title, string? description,
            string? category, string userName)
        {
            ValidateFile(file);

            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Title is required.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var doc = new FormDocument
            {
                Id = Guid.NewGuid(),
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

            return ToDto(doc);
        }

        public async Task<FormDocumentDto> ReplaceFileAsync(Guid id, IFormFile file, string userName)
        {
            ValidateFile(file);

            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            doc.OriginalFileName = file.FileName;
            doc.ContentType = file.ContentType;
            doc.FileSizeBytes = file.Length;
            doc.FileData = ms.ToArray();
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            return ToDto(doc);
        }

        public async Task UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto, string userName)
        {
            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            if (string.IsNullOrWhiteSpace(dto.Title))
                throw new InvalidOperationException("Title is required.");

            doc.Title = dto.Title.Trim();
            doc.Description = dto.Description?.Trim();
            doc.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id, string userName)
        {
            var doc = await _repo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Form document not found.");

            doc.IsDeleted = true;
            doc.UpdatedBy = userName;
            doc.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();
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
