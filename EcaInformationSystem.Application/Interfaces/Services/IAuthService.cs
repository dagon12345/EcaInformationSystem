using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
        Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
        Task<MfaSetupResult> GenerateMfaSecretAsync(Guid userId);
        Task<bool> ConfirmMfaSetupAsync(Guid userId, string code);
        Task<AuthResult> VerifyMfaAndIssueTokenAsync(string userId, string code);
        Task MarkMfaPromptShownAsync(Guid userId);
        Task<bool> ResetMfaAsync(Guid userId, string currentPassword);
        Task ResetMfaByAdminAsync(Guid targetUserId);
        Task<bool> IsMfaEnabledAsync(Guid userId);
    }
}
