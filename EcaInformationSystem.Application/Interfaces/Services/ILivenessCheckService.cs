using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface ILivenessCheckService
    {
        Task<LivenessCheckLinkDto> GenerateLinkAsync(Guid beneficiaryId, string generatedByUserId, string baseUrl);
        Task<List<LivenessCheckHistoryItemDto>> GetHistoryAsync(Guid beneficiaryId, string baseUrl);
        Task<(byte[] Bytes, string ContentType)> GetPhotoAsync(Guid recordId);
        Task VerifyAsync(Guid recordId, string reviewedByUserId, string? notes);
        Task RejectAsync(Guid recordId, string reviewedByUserId, string? notes);
        Task DeleteLinkAsync(Guid recordId, string deletedByUserName);

        Task<LivenessPublicViewDto?> GetPublicViewAsync(string token);
        Task SubmitPhotoAsync(string token, LivenessSubmitRequestDto request);

        // Scoped by the caller's role/jurisdiction — Admin/SuperAdmin/Encoder
        // see every pending submission, a PDO sees only those in their
        // assigned municipalities, everyone else (Viewer/Focal) sees none.
        Task<List<LivenessPendingReviewItemDto>> GetPendingReviewsForUserAsync(string userName, string role);
    }
}
