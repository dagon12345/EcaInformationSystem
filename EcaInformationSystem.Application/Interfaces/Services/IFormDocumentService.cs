using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFormDocumentService
    {
        Task<List<FormDocumentDto>> GetAllAsync();
        Task<List<FormDocumentDto>> SearchAsync(FormDocumentSearchDto filter);
        Task<(byte[] Data, string ContentType, string FileName)?> DownloadAsync(Guid id);

        Task<FormDocumentDto> UploadAsync(IFormFile file, string title, string? description,
            string? category, Guid? folderId, ShrinkQuality? shrinkQuality, string userName,
            int? payrollQuarter = null, int? fiscalYear = null, int? psgcCodeRegion = null,
            int? psgcCodeProvince = null, int? psgcCodeMunicipality = null, int? milestoneYear = null);

        Task<FormDocumentDto> ReplaceFileAsync(Guid id, IFormFile file, ShrinkQuality? shrinkQuality, string userName);

        Task<ShrinkPreviewResultDto> PreviewShrinkAsync(IFormFile file, ShrinkQuality quality);
        Task<FormDocumentDto> UploadFromPreviewAsync(Guid previewToken, string title, string? description,
            string? category, Guid? folderId, string userName,
            int? payrollQuarter = null, int? fiscalYear = null, int? psgcCodeRegion = null,
            int? psgcCodeProvince = null, int? psgcCodeMunicipality = null, int? milestoneYear = null);

        Task UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto, string userName);
        Task DeleteAsync(Guid id, string userName);
    }
}