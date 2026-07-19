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
        private const int MaxFailedAttempts = 5;
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

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                return AuthResult.Failed(
                    "Account temporarily locked due to too many failed attempts.",
                    attemptsRemaining: 0,
                    isLockedOut: true,
                    lockoutEndsAt: user.LockoutEnd);
            }

            if (!user.IsActivated || user.ApprovalStatus != 1)
            {
                return AuthResult.Failed("Invalid username or password.");
            }
            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                user.FailedLoginCount++;
                var remaining = MaxFailedAttempts - user.FailedLoginCount;
                if (user.FailedLoginCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    await _pendingUserRegistrationRepository.SaveChangesAsync(cancellationToken);

                    return AuthResult.Failed(
                        "Too many failed attempts. Your account is now locked for 15 minutes.",
                        attemptsRemaining: 0,
                        isLockedOut: true,
                        lockoutEndsAt: user.LockoutEnd);
                }
                await _pendingUserRegistrationRepository.SaveChangesAsync(cancellationToken);

                return AuthResult.Failed(
                         $"Invalid username or password. {remaining} attempt(s) remaining before lockout.",
                         attemptsRemaining: remaining);
            }

            // ✅ NEW — successful login resets the counter
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            await _pendingUserRegistrationRepository.SaveChangesAsync(cancellationToken);

            var token = _tokenService.GenerateToken(
                user.Id,
                user.UserName,
                user.FullName,
                user.Position,
                user.Role,
                user.Role == "PDO"
                    ? user.Jurisdictions.Select(j => j.PsgcCodeMunicipality).ToList()
                    : null,
                user.Region);  

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
            // ✅ NEW — reject any role not explicitly allowed for self-registration
            if (!AllowedSelfRegisterRoles.Contains(request.Role))
            {
                return AuthResult.Failed("Invalid role specified.");
            }

            var existingUser = await _pendingUserRegistrationRepository.GetByUserNameAsync(request.UserName, cancellationToken);
            if (existingUser != null)
            {
                return AuthResult.Failed("Registration could not be completed. Please contact your administrator.");
            }
            var user = new PendingUserRegistration
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Position = request.Position,
                BirthDate = request.BirthDate,
                Region = request.Region, //New
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
        private static readonly HashSet<string> AllowedSelfRegisterRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "PDO", "Viewer"
            // Admin and SuperAdmin deliberately excluded — those must be created
            // through the SuperAdmin user management page, not open registration.
        };

    }
}
