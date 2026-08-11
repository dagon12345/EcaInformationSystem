using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
        Task<AuthResult> LoginAsync(LoginRequest request, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
        Task<MfaSetupResult> GenerateMfaSecretAsync(Guid userId);
        Task<bool> ConfirmMfaSetupAsync(Guid userId, string code);
        Task<AuthResult> VerifyMfaAndIssueTokenAsync(string userId, string code, string ipAddress, string userAgent);
        Task MarkMfaPromptShownAsync(Guid userId);
        Task<bool> ResetMfaAsync(Guid userId, string currentPassword);
        Task ResetMfaByAdminAsync(Guid targetUserId);
        Task<bool> IsMfaEnabledAsync(Guid userId);

        // Regenerates the JWT for an already-logged-in user whose FullName/Position
        // just changed — those are embedded as token claims, so the client's stored
        // token goes stale the moment a profile edit saves until this reissues one.
        Task<AuthResult> ReissueTokenAsync(Guid userId, string? currentJti = null);
    }
}
