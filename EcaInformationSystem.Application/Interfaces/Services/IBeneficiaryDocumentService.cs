using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IBeneficiaryDocumentService
    {
        Task<List<BeneficiaryDocumentDto>> UploadAsync(
            Guid beneficiaryId,
            List<IFormFile> files,
            string userName);

        Task<List<BeneficiaryDocumentDto>> GetByBeneficiaryIdAsync(Guid beneficiaryId);

        Task<(byte[] Bytes, string FileName)> DownloadAsync(Guid documentId);

        Task SoftDeleteAsync(Guid documentId, string userName);
    }
}