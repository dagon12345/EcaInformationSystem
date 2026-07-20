using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace EcaInformationSystem.Application.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IPasswordResetRequestRepository _resetRepo;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly PasswordHasher<PendingUserRegistration> _passwordHasher;
        private readonly IDataProtector _codeProtector; // ✅ NEW

        private const int CodeLength = 8;
        private const int CodeValidityMinutes = 30;
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // ✅ excludes ambiguous chars (0/O, 1/I/L)

        public PasswordResetService(
            IPasswordResetRequestRepository resetRepo,
            IPendingUserRegistrationRepository userRepo,
            IDataProtectionProvider dataProtectionProvider)
        {
            _resetRepo = resetRepo;
            _userRepo = userRepo;
            _passwordHasher = new PasswordHasher<PendingUserRegistration>();
            _codeProtector = dataProtectionProvider.CreateProtector("PasswordResetCode");
        }

        public async Task RequestResetAsync(string userName, CancellationToken cancellationToken = default)
        {
            // ✅ Same principle as login/registration — don't reveal whether the
            // username exists. If it doesn't, we simply create no request and
            // return silently; the caller always sees the same "submitted" message.
            var user = await _userRepo.GetByUserNameAsync(userName, cancellationToken);
            if (user == null) return;

            // Avoid piling up duplicate pending requests for the same user
            var existing = await _resetRepo.GetLatestPendingByUserNameAsync(userName, cancellationToken);
            if (existing != null) return;

            var request = new PasswordResetRequest
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserName = user.UserName,
                Status = 0,
                RequestedAt = DateTime.UtcNow
            };

            await _resetRepo.AddAsync(request, cancellationToken);
            await _resetRepo.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<PasswordResetRequestListDto>> GetAllRequestsAsync(CancellationToken cancellationToken = default)
        {
            var requests = await _resetRepo.GetAllAsync(cancellationToken);
            var result = new List<PasswordResetRequestListDto>();

            foreach (var r in requests)
            {
                var user = await _userRepo.GetByIdAsync(r.UserId, cancellationToken);
                result.Add(new PasswordResetRequestListDto
                {
                    Id = r.Id,
                    UserName = r.UserName,
                    FullName = user?.FullName ?? "(deleted user)",
                    Status = r.Status,
                    RequestedAt = r.RequestedAt,
                    ResolvedAt = r.ResolvedAt,
                    ResolvedBy = r.ResolvedBy,
                    Remarks = r.Remarks,
                    CodeExpiresAt = r.CodeExpiresAt
                });
            }

            return result;
        }

        public async Task<ApprovePasswordResetResultDto> ApproveAsync(Guid requestId, string? remarks, string approvedBy, CancellationToken cancellationToken = default)
        {
            var request = await _resetRepo.GetByIdAsync(requestId, cancellationToken)
                ?? throw new KeyNotFoundException("Reset request not found.");

            if (request.Status != 0)
                throw new InvalidOperationException("This request has already been resolved.");

            var plainCode = GenerateCode();
            var encryptedCode = _codeProtector.Protect(plainCode); // ✅ CHANGED — encrypt, not hash
            var expiresAt = DateTime.UtcNow.AddMinutes(CodeValidityMinutes);

            request.Status = 1;
            request.CodeHash = encryptedCode; // ✅ field name stays, semantics changed — now holds encrypted ciphertext
            request.CodeExpiresAt = expiresAt;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = approvedBy;
            request.Remarks = remarks;

            await _resetRepo.SaveChangesAsync(cancellationToken);

            return new ApprovePasswordResetResultDto
            {
                Code = plainCode,
                ExpiresAt = expiresAt
            };
        }
        public async Task<ApprovePasswordResetResultDto?> ViewCodeAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            var request = await _resetRepo.GetByIdAsync(requestId, cancellationToken);
            if (request == null || request.Status != 1 || string.IsNullOrEmpty(request.CodeHash))
                return null;

            if (!request.CodeExpiresAt.HasValue || request.CodeExpiresAt.Value < DateTime.UtcNow)
                return null;

            string plainCode;
            try
            {
                plainCode = _codeProtector.Unprotect(request.CodeHash);
            }
            catch (CryptographicException)
            {
                // ✅ NEW — old-format (pre-encryption) data, or corrupted ciphertext.
                // Can't recover the plain code — treat as unavailable rather than 500ing.
                return null;
            }

            return new ApprovePasswordResetResultDto
            {
                Code = plainCode,
                ExpiresAt = request.CodeExpiresAt.Value
            };
        }
        public async Task RejectAsync(Guid requestId, string? remarks, string rejectedBy, CancellationToken cancellationToken = default)
        {
            var request = await _resetRepo.GetByIdAsync(requestId, cancellationToken)
                ?? throw new KeyNotFoundException("Reset request not found.");

            if (request.Status != 0)
                throw new InvalidOperationException("This request has already been resolved.");

            request.Status = 2; // Rejected
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = rejectedBy;
            request.Remarks = remarks;

            await _resetRepo.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> ResetPasswordAsync(string userName, string code, string newPassword, CancellationToken cancellationToken = default)
        {
            var request = await _resetRepo.GetLatestApprovedByUserNameAsync(userName, cancellationToken);
            if (request == null || string.IsNullOrEmpty(request.CodeHash))
                return false;

            if (!request.CodeExpiresAt.HasValue || request.CodeExpiresAt.Value < DateTime.UtcNow)
                return false;

            string decryptedCode;
            try
            {
                decryptedCode = _codeProtector.Unprotect(request.CodeHash); // ✅ CHANGED — decrypt, then compare directly
            }
            catch
            {
                return false; // corrupted/tampered ciphertext
            }

            if (!string.Equals(decryptedCode, code.ToUpperInvariant().Trim(), StringComparison.Ordinal))
                return false;

            var user = await _userRepo.GetByUserNameAsync(userName, cancellationToken);
            if (user == null) return false;

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;

            request.Status = 3;
            await _userRepo.SaveChangesAsync(cancellationToken);
            await _resetRepo.SaveChangesAsync(cancellationToken);

            return true;
        }

        private static string GenerateCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(CodeLength);
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
            }
            return new string(chars);
        }
    }
}