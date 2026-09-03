using System.Security.Cryptography;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EcaInformationSystem.Application.Services
{
    public class LivenessCheckService : ILivenessCheckService
    {
        // Defense-in-depth backstop behind the client-side compression —
        // a genuinely live, shrunk selfie should never come close to this.
        private const int MaxPhotoBytes = 300 * 1024;

        // Roles with no jurisdiction restriction — see everything, mirroring
        // JurisdictionGuardService's own "who bypasses the municipality check" list.
        private static readonly HashSet<string> UnrestrictedRoles = new() { "Admin", "SuperAdmin", "Encoder" };

        private readonly ILivenessCheckRepository _repo;
        private readonly IPsgcNameCache _psgcNameCache;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly ILivenessNotificationBroadcaster _broadcaster;
        private readonly ILogRepository _logRepository;
        private readonly IStatisticsService _statisticsService;
        private readonly IMemoryCache _memoryCache;

        public LivenessCheckService(
            ILivenessCheckRepository repo,
            IPsgcNameCache psgcNameCache,
            IPendingUserRegistrationRepository userRepo,
            ILivenessNotificationBroadcaster broadcaster,
            ILogRepository logRepository,
            IStatisticsService statisticsService,
            IMemoryCache memoryCache)
        {
            _repo = repo;
            _psgcNameCache = psgcNameCache;
            _userRepo = userRepo;
            _broadcaster = broadcaster;
            _logRepository = logRepository;
            _statisticsService = statisticsService;
            _memoryCache = memoryCache;
        }

        // beneficiary.IsLivenessVerified changing (Verify / reset-on-delete) makes
        // every cached "who matches this filter" result stale — the Statistics
        // report cache, and the grid/summary caches keyed off SummaryCacheVersionKey
        // (BeneficiaryInformationService.InvalidateSummaryCache does the same thing
        // for every OTHER beneficiary mutation; this service never went through
        // that path, which is what let a just-verified grantee still be missing
        // from an already-cached "Province X, no Liveness filter" report until the
        // cache's own 10-minute expiry caught up).
        private async Task InvalidateBeneficiaryCachesAsync()
        {
            _memoryCache.Set(CommonConstants.SummaryCacheVersionKey, Guid.NewGuid().ToString());
            _memoryCache.Set(CommonConstants.DuplicateScanCacheVersionKey, Guid.NewGuid().ToString());
            await _statisticsService.InvalidateStatisticsCacheAsync();
        }

        public async Task<LivenessCheckLinkDto> GenerateLinkAsync(Guid beneficiaryId, string generatedByUserId, string role, string baseUrl)
        {
            var beneficiary = await _repo.GetBeneficiaryAsync(beneficiaryId)
                ?? throw new InvalidOperationException("Beneficiary record not found.");

            // ✅ Same jurisdiction rule as GetPendingReviewsForUserAsync below —
            // Admin/SuperAdmin/Encoder are unrestricted; a PDO may only generate
            // a link for a grantee in one of their own assigned municipalities;
            // any other role (Viewer/Focal) is rejected outright. The client
            // already hides the button outside this scope, but that's not
            // enforcement on its own — this is what actually stops a direct API
            // call from generating a link for a grantee outside the caller's
            // jurisdiction.
            if (!UnrestrictedRoles.Contains(role))
            {
                if (role != "PDO")
                    throw new InvalidOperationException("You don't have permission to generate a liveness link.");

                var user = await _userRepo.GetByUserNameAsync(generatedByUserId);
                var allowedMunicipalityCodes = user?.Jurisdictions.Select(j => j.PsgcCodeMunicipality).ToList() ?? new();

                if (!allowedMunicipalityCodes.Contains(beneficiary.Municipality))
                    throw new InvalidOperationException("This grantee is outside your assigned jurisdiction.");
            }

            // Only one liveness check attempt ever exists per grantee — never
            // multiple rows/links. Regenerating overwrites the same record
            // (fresh token, back to Pending, any prior photo/review cleared)
            // instead of creating a new one.
            var record = await _repo.GetByBeneficiaryIdAsync(beneficiaryId);
            if (record is null)
            {
                record = new LivenessCheckRecord { Id = Guid.NewGuid(), BeneficiaryInformationId = beneficiaryId };
                await _repo.AddAsync(record);
            }

            record.Token = GenerateToken();
            record.IsActive = true;
            record.Status = LivenessCheckStatus.Pending;
            record.GeneratedByUserId = generatedByUserId;
            record.GeneratedDate = DateTime.UtcNow;
            record.PhotoData = null;
            record.PhotoContentType = null;
            record.SubmittedDate = null;
            record.ReviewedByUserId = null;
            record.ReviewedDate = null;
            record.ReviewNotes = null;

            await _repo.SaveChangesAsync();

            return new LivenessCheckLinkDto
            {
                Id = record.Id,
                Token = record.Token,
                Url = $"{baseUrl.TrimEnd('/')}/liveness/{record.Token}",
                GeneratedDate = record.GeneratedDate,
                Status = record.Status.ToString()
            };
        }

        public async Task<List<LivenessCheckHistoryItemDto>> GetHistoryAsync(Guid beneficiaryId, string baseUrl)
        {
            var records = await _repo.GetHistoryByBeneficiaryIdAsync(beneficiaryId);
            return records.Select(r => new LivenessCheckHistoryItemDto
            {
                Id = r.Id,
                IsActive = r.IsActive,
                Status = r.Status.ToString(),
                // Only a still-pending, active link is actually usable — surface
                // its URL so the PDO can view/copy it again without regenerating
                // (which would invalidate the one already sent to the grantee).
                Url = r.IsActive && r.Status == LivenessCheckStatus.Pending
                    ? $"{baseUrl.TrimEnd('/')}/liveness/{r.Token}"
                    : null,
                GeneratedDate = r.GeneratedDate,
                SubmittedDate = r.SubmittedDate,
                ReviewedDate = r.ReviewedDate,
                ReviewNotes = r.ReviewNotes,
                HasPhoto = r.PhotoData is { Length: > 0 }
            }).ToList();
        }

        public async Task DeleteLinkAsync(Guid recordId, string deletedByUserName)
        {
            var record = await _repo.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("Liveness check record not found.");

            // With the liveness photo gone, the grantee no longer has any
            // evidence on file — reset the beneficiary back to "not yet
            // checked" rather than leaving a stale Verified flag/date behind.
            var beneficiary = await _repo.GetBeneficiaryAsync(record.BeneficiaryInformationId);
            if (beneficiary is not null)
            {
                beneficiary.IsLivenessVerified = null;
                beneficiary.DateOfLiveness = null;
            }

            var previousStatus = record.Status;
            await _repo.DeleteAsync(record);

            await _logRepository.AddAsync(new Log
            {
                Id = Guid.NewGuid(),
                BeneficiaryInformationId = record.BeneficiaryInformationId,
                Activity = $"Liveness check link/photo deleted (was {previousStatus})" +
                    (previousStatus == LivenessCheckStatus.Verified ? " — liveness verification reset to Not Yet Checked" : ""),
                UserName = deletedByUserName,
                CreatedAt = DateTime.UtcNow,
                Category = "Liveness"
            });

            await _repo.SaveChangesAsync();
            await InvalidateBeneficiaryCachesAsync();
        }

        public async Task<(byte[] Bytes, string ContentType)> GetPhotoAsync(Guid recordId)
        {
            var record = await _repo.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("Liveness check record not found.");

            if (record.PhotoData is null || record.PhotoData.Length == 0)
                throw new InvalidOperationException("No photo has been submitted for this record.");

            return (record.PhotoData, record.PhotoContentType ?? "image/jpeg");
        }

        public async Task VerifyAsync(Guid recordId, string reviewedByUserId, string? notes)
        {
            var record = await _repo.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("Liveness check record not found.");

            if (record.Status != LivenessCheckStatus.Submitted)
                throw new InvalidOperationException("Only a submitted photo can be verified.");

            var beneficiary = await _repo.GetBeneficiaryAsync(record.BeneficiaryInformationId)
                ?? throw new InvalidOperationException("Beneficiary record not found.");

            record.Status = LivenessCheckStatus.Verified;
            record.ReviewedByUserId = reviewedByUserId;
            record.ReviewedDate = DateTime.UtcNow;
            record.ReviewNotes = notes;

            beneficiary.IsLivenessVerified = true;
            beneficiary.DateOfLiveness = record.SubmittedDate ?? DateTime.UtcNow;

            await _repo.SaveChangesAsync();
            await InvalidateBeneficiaryCachesAsync();
        }

        public async Task RejectAsync(Guid recordId, string reviewedByUserId, string? notes)
        {
            var record = await _repo.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("Liveness check record not found.");

            if (record.Status != LivenessCheckStatus.Submitted)
                throw new InvalidOperationException("Only a submitted photo can be rejected.");

            record.Status = LivenessCheckStatus.Rejected;
            record.ReviewedByUserId = reviewedByUserId;
            record.ReviewedDate = DateTime.UtcNow;
            record.ReviewNotes = notes;

            await _repo.SaveChangesAsync();
        }

        public async Task<LivenessPublicViewDto?> GetPublicViewAsync(string token)
        {
            var record = await _repo.GetByTokenAsync(token);
            if (record is null || !record.IsActive || record.Status != LivenessCheckStatus.Pending)
                return null;

            var b = await _repo.GetBeneficiaryAsync(record.BeneficiaryInformationId);
            if (b is null) return null;

            var nameParts = new[] { b.LastName, b.FirstName, b.MiddleName, b.Extension }
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return new LivenessPublicViewDto
            {
                FullName = string.Join(", ", nameParts).Trim(),
                BirthDate = b.BirthDate,
                Sex = b.Sex,
                OscaIdNumber = b.OscaIdNumber,
                TrackingNumber = b.TrackingNumber,
                RegionName = _psgcNameCache.GetRegionName(b.Region),
                ProvinceName = _psgcNameCache.GetProvinceName(b.Province),
                MunicipalityName = _psgcNameCache.GetMunicipalityName(b.Municipality),
                BarangayName = _psgcNameCache.GetBarangayName(b.Barangay),
                HouseNumber = b.HouseNumber,
                StreetName = b.StreetName,
                ZipCode = b.ZipCode,
                Status = record.Status.ToString()
            };
        }

        public async Task SubmitPhotoAsync(string token, LivenessSubmitRequestDto request)
        {
            var record = await _repo.GetByTokenAsync(token)
                ?? throw new InvalidOperationException("This link is invalid.");

            if (!record.IsActive || record.Status != LivenessCheckStatus.Pending)
                throw new InvalidOperationException("This link is no longer valid — ask your PDO for a new one.");

            if (string.IsNullOrWhiteSpace(request.PhotoBase64))
                throw new InvalidOperationException("No photo was provided.");

            var contentType = request.ContentType?.ToLowerInvariant();
            if (contentType != "image/jpeg" && contentType != "image/jpg")
                throw new InvalidOperationException("Only a JPEG camera capture is accepted.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(request.PhotoBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("The submitted photo data is corrupted.");
            }

            if (bytes.Length == 0 || bytes.Length > MaxPhotoBytes)
                throw new InvalidOperationException("The submitted photo is empty or too large.");

            // JPEG magic bytes: FF D8 FF — guards against a spoofed content-type.
            if (bytes.Length < 3 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
                throw new InvalidOperationException("The submitted file is not a valid JPEG image.");

            record.PhotoData = bytes;
            record.PhotoContentType = "image/jpeg";
            record.Status = LivenessCheckStatus.Submitted;
            record.SubmittedDate = DateTime.UtcNow;
            record.IsActive = false; // single-use — reload now reports "already submitted"

            await _repo.SaveChangesAsync();

            var beneficiary = await _repo.GetBeneficiaryAsync(record.BeneficiaryInformationId);
            if (beneficiary is not null)
            {
                var targetUserIds = await ResolveNotificationTargetUserIdsAsync(beneficiary.Municipality);
                if (targetUserIds.Count > 0)
                {
                    var nameParts = new[] { beneficiary.LastName, beneficiary.FirstName, beneficiary.MiddleName, beneficiary.Extension }
                        .Where(p => !string.IsNullOrWhiteSpace(p));

                    await _broadcaster.NotifyLivenessSubmittedAsync(targetUserIds, new LivenessSubmittedNotificationDto
                    {
                        RecordId = record.Id,
                        BeneficiaryId = beneficiary.Id,
                        FullName = string.Join(", ", nameParts).Trim(),
                        MunicipalityName = _psgcNameCache.GetMunicipalityName(beneficiary.Municipality),
                        SubmittedDate = record.SubmittedDate.Value
                    });
                }
            }
        }

        public async Task<List<LivenessPendingReviewItemDto>> GetPendingReviewsForUserAsync(string userName, string role)
        {
            List<int>? allowedMunicipalityCodes = null;

            if (!UnrestrictedRoles.Contains(role))
            {
                if (role != "PDO") return new(); // Viewer/Focal — no jurisdiction, nothing to show

                var user = await _userRepo.GetByUserNameAsync(userName);
                if (user is null) return new();

                allowedMunicipalityCodes = user.Jurisdictions.Select(j => j.PsgcCodeMunicipality).ToList();
                if (allowedMunicipalityCodes.Count == 0) return new();
            }

            var rows = await _repo.GetSubmittedForReviewAsync(allowedMunicipalityCodes);
            return rows.Select(x =>
            {
                var nameParts = new[] { x.Beneficiary.LastName, x.Beneficiary.FirstName, x.Beneficiary.MiddleName, x.Beneficiary.Extension }
                    .Where(p => !string.IsNullOrWhiteSpace(p));

                return new LivenessPendingReviewItemDto
                {
                    RecordId = x.Record.Id,
                    BeneficiaryId = x.Beneficiary.Id,
                    FullName = string.Join(", ", nameParts).Trim(),
                    MunicipalityName = _psgcNameCache.GetMunicipalityName(x.Beneficiary.Municipality),
                    SubmittedDate = x.Record.SubmittedDate ?? x.Record.GeneratedDate
                };
            }).ToList();
        }

        private async Task<List<Guid>> ResolveNotificationTargetUserIdsAsync(int municipalityCode)
        {
            var allUsers = await _userRepo.GetAllAsync();
            return allUsers
                .Where(u => u.ApprovalStatus == (int)ApprovalStatus.Approved && u.IsActivated && !u.IsDeactivated)
                .Where(u => u.Role is "Admin" or "SuperAdmin"
                    || (u.Role == "PDO" && u.Jurisdictions.Any(j => j.PsgcCodeMunicipality == municipalityCode)))
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
