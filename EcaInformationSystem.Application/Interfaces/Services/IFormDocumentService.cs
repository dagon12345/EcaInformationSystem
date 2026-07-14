using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFormDocumentService
    {
        Task<List<FormDocumentDto>> GetAllAsync();
        Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(Guid id);

        // Admin/SuperAdmin only — enforced at controller level, service trusts the caller
        Task<FormDocumentDto> UploadAsync(IFormFile file, string title, string? description,
            string? category, string userName);

        Task<FormDocumentDto> ReplaceFileAsync(Guid id, IFormFile file, string userName);
        Task UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto, string userName);
        Task DeleteAsync(Guid id, string userName);
    }
}
