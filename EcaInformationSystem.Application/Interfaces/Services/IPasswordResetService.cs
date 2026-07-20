using EcaInformationSystem.Shared.DTOs.Auth;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IPasswordResetService
    {
        Task RequestResetAsync(string userName, CancellationToken cancellationToken = default);
        Task<List<PasswordResetRequestListDto>> GetAllRequestsAsync(CancellationToken cancellationToken = default);
        Task<ApprovePasswordResetResultDto> ApproveAsync(Guid requestId, string? remarks, string approvedBy, CancellationToken cancellationToken = default);
        Task RejectAsync(Guid requestId, string? remarks, string rejectedBy, CancellationToken cancellationToken = default);
        Task<bool> ResetPasswordAsync(string userName, string code, string newPassword, CancellationToken cancellationToken = default);
        Task<ApprovePasswordResetResultDto?> ViewCodeAsync(Guid requestId, CancellationToken cancellationToken = default);
        Task<ApprovePasswordResetResultDto> RegenerateCodeAsync(Guid requestId, string regeneratedBy, CancellationToken cancellationToken = default);
    }
}
