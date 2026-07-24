using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OtpNet;
using QRCoder;

namespace EcaInformationSystem.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IPendingUserRegistrationRepository _pendingUserRegistrationRepository;
        private readonly TokenService _tokenService;
        private readonly PasswordHasher<PendingUserRegistration> _passwordHasher;
        private readonly IDataProtector _mfaProtector; // ✅ NEW
        private readonly ILogRepository _logRepository;

        private const int MaxFailedAttempts = 5;
        public AuthService(IPendingUserRegistrationRepository pendingUserRegistrationRepository,
            TokenService tokenService, IDataProtectionProvider dataProtectionProvider, ILogRepository logRepository)
        {
            _pendingUserRegistrationRepository = pendingUserRegistrationRepository;
            _passwordHasher = new PasswordHasher<PendingUserRegistration>();
            _tokenService = tokenService;
            _mfaProtector = dataProtectionProvider.CreateProtector("MfaSecrets");
            _logRepository = logRepository;
        }
        public async Task<bool> IsMfaEnabledAsync(Guid userId)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");
            return user.IsMfaEnabled;
        }
        // ✅ NEW — admin-initiated reset, for when a user is locked out with no working authenticator
        public async Task ResetMfaByAdminAsync(Guid targetUserId)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            user.IsMfaEnabled = false;
            user.MfaSetupComplete = false;
            user.MfaSecret = null;
            user.MfaPromptShown = false; // ✅ so they get prompted to set it up again on next login

            await _pendingUserRegistrationRepository.SaveChangesAsync();
        }
        public async Task<bool> ResetMfaAsync(Guid userId, string currentPassword)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verify == PasswordVerificationResult.Failed) return false;

            user.IsMfaEnabled = false;
            user.MfaSetupComplete = false;
            user.MfaSecret = null;
            await _pendingUserRegistrationRepository.SaveChangesAsync();
            return true;
        }
        public async Task MarkMfaPromptShownAsync(Guid userId)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");
            user.MfaPromptShown = true;
            await _pendingUserRegistrationRepository.SaveChangesAsync();
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

            // ✅ Password correct — reset failure tracking regardless of MFA branch below
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            await _pendingUserRegistrationRepository.SaveChangesAsync(cancellationToken);

            // ✅ NEW — branch here instead of issuing a token immediately
            if (user.IsMfaEnabled && user.MfaSetupComplete)
            {
                return AuthResult.NeedsMfa(user.Id.ToString());
            }

            return await IssueTokenAsync(user);
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
        #region Start Private Methods
        // ✅ NEW — MFA enrollment: generate secret, encrypt before storing, return plain version once for setup
        public async Task<MfaSetupResult> GenerateMfaSecretAsync(Guid userId)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            // ✅ NEW — don't silently regenerate/invalidate an already-active MFA setup
            if (user.IsMfaEnabled && user.MfaSetupComplete)
            {
                throw new InvalidOperationException("MFA is already enabled for this account.");
            }

            var secretKey = KeyGeneration.GenerateRandomKey(20); // 160-bit, standard for TOTP
            var base32Secret = Base32Encoding.ToString(secretKey);

            // ✅ Encrypt before persisting — plain secret only ever leaves this method in the response
            user.MfaSecret = _mfaProtector.Protect(base32Secret);
            user.MfaSetupComplete = false;
            await _pendingUserRegistrationRepository.SaveChangesAsync();

            var issuer = "ECA-InFORMS";
            var otpauthUri = $"otpauth://totp/{issuer}:{user.UserName}?secret={base32Secret}&issuer={issuer}&digits=6&period=30";

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(10);

            return new MfaSetupResult
            {
                Secret = base32Secret, // plain — shown once, for manual entry / QR fallback
                QrCodeImageBase64 = Convert.ToBase64String(qrBytes)
            };
        }

        // ✅ NEW — confirm enrollment: user proves they scanned/entered it correctly
        public async Task<bool> ConfirmMfaSetupAsync(Guid userId, string code)
        {
            var user = await _pendingUserRegistrationRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            if (string.IsNullOrEmpty(user.MfaSecret)) return false;

            var plainSecret = _mfaProtector.Unprotect(user.MfaSecret); // ✅ decrypt to validate
            var totp = new Totp(Base32Encoding.ToBytes(plainSecret));
            var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));

            if (!isValid) return false;

            user.IsMfaEnabled = true;
            user.MfaSetupComplete = true;
            await _pendingUserRegistrationRepository.SaveChangesAsync();
            return true;
        }

        // ✅ NEW — second step of login: verify TOTP code, then issue the JWT
        public async Task<AuthResult> VerifyMfaAndIssueTokenAsync(string userId, string code)
        {
            if (!Guid.TryParse(userId, out var parsedId))
                return AuthResult.Failed("Invalid request.");

            var user = await _pendingUserRegistrationRepository.GetByIdAsync(parsedId);
            if (user == null || !user.IsMfaEnabled || string.IsNullOrEmpty(user.MfaSecret))
                return AuthResult.Failed("Invalid request.");

            var plainSecret = _mfaProtector.Unprotect(user.MfaSecret);
            var totp = new Totp(Base32Encoding.ToBytes(plainSecret));
            var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));

            if (!isValid)
                return AuthResult.Failed("Invalid or expired code.");

            return await IssueTokenAsync(user);
        }
        private static readonly HashSet<string> AllowedSelfRegisterRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "PDO", "Viewer"
            // Admin and SuperAdmin deliberately excluded — those must be created
            // through the SuperAdmin user management page, not open registration.
        };
        // ✅ NEW — extracted so both LoginAsync (non-MFA path) and VerifyMfaAndIssueTokenAsync can use it
        private async Task<AuthResult> IssueTokenAsync(PendingUserRegistration user)
        {
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
            result.Token = token;

            // ✅ CHANGED — show every login until MFA is actually enabled, not just once
            result.ShowMfaPrompt = !user.IsMfaEnabled;

            // ✅ NEW — audit trail of who logged in and when
            await _logRepository.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = null,
                Activity = "User logged in",
                UserName = user.UserName,
                CreatedAt = DateTime.UtcNow,
                Category = "Login"
            });
            await _logRepository.SaveChangesAsync();

            return result;
        }

        #endregion End Private Methods
    }
}
