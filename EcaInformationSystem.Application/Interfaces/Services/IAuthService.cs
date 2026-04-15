using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.DTOs.Auth;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
        Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    }
}
