using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Common.Enum;
using Microsoft.AspNetCore.Identity;

namespace EcaInformationSystem.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IPendingUserRegistrationRepository _pendingUserRegistrationRepository;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher<PendingUserRegistration> _passwordHasher;
        public AuthService(IPendingUserRegistrationRepository pendingUserRegistrationRepository,
            TokenService tokenService)
        {
            _pendingUserRegistrationRepository = pendingUserRegistrationRepository;
            _passwordHasher = new PasswordHasher<PendingUserRegistration>();
            _tokenService = tokenService;
        }
        public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _pendingUserRegistrationRepository.GetByUserNameAsync(request.UserName, cancellationToken);
            if (user == null)
            {
                return AuthResult.Failed("Invalid username or password.");
            }
            if (!user.IsActivated || user.ApprovalStatus != 1)
            {
                return AuthResult.Failed("User account is not active.");
            }
            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                return AuthResult.Failed("Invalid username or password.");
            }

            //Generate the token using the Token Service
            var token = _tokenService.GenerateToken(user.UserName, user.FullName, user.Role);



            var result = AuthResult.Passed(
                "Login successful.",
                user.Id.ToString(),
                user.UserName,
                user.FullName,
                user.Position!);
            result.Token = token; // Passed the generated token
            return result;
        }

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        {
            var existingUser = await _pendingUserRegistrationRepository.GetByUserNameAsync(request.UserName, cancellationToken);
            if (existingUser != null)
            {
                return AuthResult.Failed("Username already exists.");
            }
            var user = new PendingUserRegistration
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Position = request.Position,
                BirthDate = request.BirthDate,
                UserName = request.UserName,
                IsActivated = false,
                ApprovalStatus = (int)ApprovalStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow,
                ReviewedBy = "System",
                Remarks = "Auto-approved internal registration",
                Role = request.Role 
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _pendingUserRegistrationRepository.AddAsync(user, cancellationToken);
            await _pendingUserRegistrationRepository.SaveChangesAsync(cancellationToken);
            return AuthResult.Passed(
                "Registration successful.",
                user.Id.ToString(),
                user.UserName,
                user.FullName,
                user.Position);
        }
    }
}
