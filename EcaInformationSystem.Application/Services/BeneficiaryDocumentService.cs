using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Services
{
    public class BeneficiaryDocumentService : IBeneficiaryDocumentService
    {
        private readonly IBeneficiaryDocumentRepository _repo;
        private readonly IPdfCompressionService _compression;
        private readonly ILogRepository _logRepository;

        public BeneficiaryDocumentService(
            IBeneficiaryDocumentRepository repo,
            IPdfCompressionService compression,
            ILogRepository logRepository)
        {
            _repo = repo;
            _compression = compression;
            _logRepository = logRepository;
        }

        public async Task<List<BeneficiaryDocumentDto>> UploadAsync(
            Guid beneficiaryId,
            List<IFormFile> files,
            string userName)
        {
            var results = new List<BeneficiaryDocumentDto>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"'{file.FileName}' is not a PDF file.");

                if (file.Length > 52_428_800)
                    throw new Exception($"'{file.FileName}' exceeds the 50MB limit.");

                byte[] originalBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    originalBytes = ms.ToArray();
                }

                byte[] compressedBytes;
                using (var inputStream = new MemoryStream(originalBytes))
                {
                    compressedBytes = await _compression.CompressAsync(inputStream);
                }

                var finalBytes = compressedBytes.Length < originalBytes.Length
                    ? compressedBytes
                    : originalBytes;

                var uniqueName = $"{Guid.NewGuid():N}.pdf";

                var document = new BeneficiaryDocument
                {
                    Id = Guid.NewGuid(),
                    BeneficiaryInformationId = beneficiaryId,
                    FileName = uniqueName,
                    OriginalFileName = file.FileName,
                    FileData = finalBytes,
                    FileSizeBytes = finalBytes.Length,
                    OriginalFileSizeBytes = originalBytes.Length,
                    ContentType = "application/pdf",
                    UploadedAt = DateTime.UtcNow,
                    UploadedBy = userName,
                    IsDeleted = false
                };

                await _repo.AddAsync(document);
                await _repo.SaveChangesAsync();

                await AddLogAsync(
                    beneficiaryId,
                    $"Document uploaded: '{file.FileName}' " +
                    $"({FormatSize(finalBytes.Length)}" +
                    $"{(compressedBytes.Length < originalBytes.Length ? $", compressed from {FormatSize(originalBytes.Length)}" : string.Empty)})",
                    userName);

                await _repo.SaveChangesAsync();

                results.Add(MapToDto(document));
            }

            return results;
        }

        public async Task<List<BeneficiaryDocumentDto>> GetByBeneficiaryIdAsync(
            Guid beneficiaryId)
        {
            var docs = await _repo.GetByBeneficiaryIdAsync(beneficiaryId);
            return docs.Select(MapToDto).ToList();
        }

        public async Task<(byte[] Bytes, string FileName)> DownloadAsync(
            Guid documentId)
        {
            var document = await _repo.GetByIdAsync(documentId)
                ?? throw new Exception("Document not found.");

            if (document.FileData is null || document.FileData.Length == 0)
                throw new Exception("Document data is missing or empty.");

            return (document.FileData, document.OriginalFileName);
        }

        public async Task SoftDeleteAsync(Guid documentId, string userName)
        {
            var doc = await _repo.GetByIdAsync(documentId)
         ?? throw new Exception("Document not found.");

            doc.IsDeleted = true;
            await _repo.UpdateAsync(doc);

            await AddLogAsync(
                doc.BeneficiaryInformationId,
                $"Document deleted: '{doc.OriginalFileName}' ({FormatSize(doc.FileSizeBytes)})",
                userName);

            await _repo.SaveChangesAsync();
        }

        private static BeneficiaryDocumentDto MapToDto(BeneficiaryDocument doc) => new()
        {
            Id = doc.Id,
            BeneficiaryInformationId = doc.BeneficiaryInformationId,
            FileName = doc.FileName,
            OriginalFileName = doc.OriginalFileName,
            FileSizeBytes = doc.FileSizeBytes,
            OriginalFileSizeBytes = doc.OriginalFileSizeBytes,
            UploadedAt = doc.UploadedAt,
            UploadedBy = doc.UploadedBy,

            FileSizeDisplay = FormatSize(doc.FileSizeBytes),
            CompressionDisplay = doc.OriginalFileSizeBytes > 0 && doc.FileSizeBytes < doc.OriginalFileSizeBytes
                ? $"{(int)(100 - (doc.FileSizeBytes * 100.0 / doc.OriginalFileSizeBytes))}% smaller"
                : null
        };

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1048576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1048576.0:F1} MB"
        };

        private async Task AddLogAsync(Guid beneficiaryId, string activity, string userName)
        {
            var log = new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = beneficiaryId,
                Activity = activity,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            };
            await _logRepository.AddAsync(log);
        }
    }
}