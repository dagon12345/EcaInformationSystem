using System.Security.Cryptography;
using System.Text;
using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;

namespace EcaInformationSystem.Application.Services
{
    // PDO-initiated onboarding for external partner-LGU "focal" contacts —
    // the PDO vouches for who they are and which province(s) they cover;
    // the focal only ever sets a username/password. Resulting accounts get
    // Role="Focal", which AuthPolicies excludes from everything except
    // voice calling and the directory (see Program.cs AddAuthorization).
    public class FocalInviteService : IFocalInviteService
    {
        private const int CodeLength = 8;
        private const int CodeValidityDays = 7;
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // excludes ambiguous chars (0/O, 1/I/L)

        private readonly IFocalInviteRepository _inviteRepo;
        private readonly IPendingUserRegistrationRepository _userRepo;
        private readonly IAuthService _authService;
        private readonly IChatService _chatService;
        private readonly PasswordHasher<PendingUserRegistration> _passwordHasher;

        public FocalInviteService(
            IFocalInviteRepository inviteRepo,
            IPendingUserRegistrationRepository userRepo,
            IAuthService authService,
            IChatService chatService)
        {
            _inviteRepo = inviteRepo;
            _userRepo = userRepo;
            _authService = authService;
            _chatService = chatService;
            _passwordHasher = new PasswordHasher<PendingUserRegistration>();
        }

        public async Task<List<MyJurisdictionDto>> GetMyJurisdictionsAsync(Guid pdoUserId)
        {
            var jurisdictions = await _userRepo.GetJurisdictionsByUserIdAsync(pdoUserId);
            return jurisdictions.Select(j => new MyJurisdictionDto
            {
                Id = j.Id,
                PsgcCodeMunicipality = j.PsgcCodeMunicipality,
                MunicipalityName = j.MunicipalityName,
                ProvinceName = j.ProvinceName
            }).ToList();
        }

        public async Task<FocalInviteResultDto> CreateInviteAsync(Guid inviterId, string inviterRole, CreateFocalInviteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                throw new InvalidOperationException("Name is required.");

            // ✅ A focal covers exactly one municipality — enforced here too,
            // not just by the UI only offering a single choice.
            if (request.Municipalities.Count != 1)
                throw new InvalidOperationException("Select exactly one municipality for this focal.");

            List<FocalMunicipalitySelection> selected;
            if (inviterRole == "PDO")
            {
                // ✅ A PDO can only vouch for a focal in a province they actually
                // cover — validated against their own jurisdiction, not trusted
                // from the client-supplied list.
                var myCodes = (await _userRepo.GetJurisdictionsByUserIdAsync(inviterId))
                    .Select(j => j.PsgcCodeMunicipality)
                    .ToHashSet();
                selected = request.Municipalities.Where(m => myCodes.Contains(m.PsgcCodeMunicipality)).ToList();

                if (selected.Count == 0)
                    throw new InvalidOperationException("Select a municipality you cover.");
            }
            else
            {
                // Admin/SuperAdmin aren't scoped to any particular jurisdiction —
                // they can invite a focal for any municipality.
                selected = request.Municipalities;
            }

            var plainCode = GenerateCode();
            var invite = new FocalInvite
            {
                Id = Guid.NewGuid(),
                InvitedByUserId = inviterId,
                FullName = request.FullName.Trim(),
                ContactNote = string.IsNullOrWhiteSpace(request.ContactNote) ? null : request.ContactNote.Trim(),
                CodeHash = HashCode(plainCode),
                ExpiresAt = DateTime.UtcNow.AddDays(CodeValidityDays),
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var m in selected)
            {
                invite.Jurisdictions.Add(new FocalInviteJurisdiction
                {
                    Id = Guid.NewGuid(),
                    FocalInviteId = invite.Id,
                    PsgcCodeMunicipality = m.PsgcCodeMunicipality,
                    MunicipalityName = m.MunicipalityName,
                    ProvinceName = m.ProvinceName
                });
            }

            await _inviteRepo.AddAsync(invite);
            await _inviteRepo.SaveChangesAsync();

            return new FocalInviteResultDto
            {
                InviteId = invite.Id,
                Code = plainCode,
                ExpiresAt = invite.ExpiresAt
            };
        }

        public async Task<FocalInvitePreviewDto> PreviewInviteAsync(string code)
        {
            var invite = await _inviteRepo.GetPendingByCodeHashAsync(HashCode(code));
            if (invite is null || invite.ExpiresAt < DateTime.UtcNow)
            {
                return new FocalInvitePreviewDto
                {
                    IsValid = false,
                    ErrorMessage = "This invite link is invalid or has expired. Ask your PDO contact for a new one."
                };
            }

            return new FocalInvitePreviewDto
            {
                IsValid = true,
                FullName = invite.FullName,
                ProvinceNames = invite.Jurisdictions
                    .Select(j => j.ProvinceName)
                    .Distinct()
                    .ToList()
            };
        }

        public async Task<AuthResult> AcceptInviteAsync(AcceptFocalInviteRequest request, string ipAddress, string userAgent)
        {
            var invite = await _inviteRepo.GetPendingByCodeHashAsync(HashCode(request.Code));
            if (invite is null || invite.ExpiresAt < DateTime.UtcNow)
                return AuthResult.Failed("This invite link is invalid or has expired.");

            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
                return AuthResult.Failed("Username and password are required.");

            var existing = await _userRepo.GetByUserNameAsync(request.UserName);
            if (existing != null)
                return AuthResult.Failed("That username is already taken.");

            var inviter = await _userRepo.GetByIdAsync(invite.InvitedByUserId);

            var user = new PendingUserRegistration
            {
                Id = Guid.NewGuid(),
                FullName = invite.FullName,
                Position = "Provincial Focal",
                BirthDate = DateTime.UtcNow.Date, // not meaningful for a call-only account
                Region = null,
                UserName = request.UserName,
                IsActivated = true,
                ApprovalStatus = (int)ApprovalStatus.Approved, // the inviting PDO already vetted them
                RequestedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow,
                ReviewedBy = inviter?.FullName ?? "PDO Invite",
                Remarks = $"Onboarded via PDO invite {invite.Id}",
                Role = "Focal"
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            await _userRepo.AddAsync(user);
            await _userRepo.SaveChangesAsync();

            var newJurisdictions = invite.Jurisdictions.Select(j => new PdoJurisdiction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PsgcCodeMunicipality = j.PsgcCodeMunicipality,
                MunicipalityName = j.MunicipalityName,
                ProvinceName = j.ProvinceName,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = $"Invite ({inviter?.FullName ?? "unknown"})"
            }).ToList();
            await _userRepo.ReplaceJurisdictionsAsync(user.Id, newJurisdictions);

            invite.Status = 1;
            invite.AcceptedAt = DateTime.UtcNow;
            invite.ResultingUserId = user.Id;
            await _inviteRepo.SaveChangesAsync();

            // ✅ Auto-create the Focal's one 1:1 chat room with the PDO who
            // covers their municipality — "since they have only one PDO each
            // jurisdiction/municipality" per the business rule. Best-effort:
            // a missing/ambiguous PDO shouldn't block account creation.
            var primaryMunicipality = newJurisdictions.FirstOrDefault()?.PsgcCodeMunicipality;
            if (primaryMunicipality.HasValue)
            {
                try
                {
                    var pdo = await _userRepo.GetPdoByMunicipalityAsync(primaryMunicipality.Value);
                    if (pdo != null)
                        await _chatService.StartDirectConversationAsync(user.Id, pdo.Id);
                }
                catch { /* chat room creation is a nice-to-have, not a blocker for onboarding */ }
            }

            return await _authService.ReissueTokenAsync(user.Id);
        }

        public async Task<List<FocalInviteSummaryDto>> GetMyPendingInvitesAsync(Guid callerId)
        {
            var invites = await _inviteRepo.GetByInviterAsync(callerId);

            return invites
                .Where(i => i.Status == 0 && i.ExpiresAt >= DateTime.UtcNow)
                .Select(i => new FocalInviteSummaryDto
                {
                    Id = i.Id,
                    FullName = i.FullName,
                    ContactNote = i.ContactNote,
                    MunicipalityNames = string.Join(", ", i.Jurisdictions.Select(j => j.MunicipalityName).Distinct()),
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.ExpiresAt
                })
                .ToList();
        }

        public async Task<FocalInviteResultDto> RegenerateInviteLinkAsync(Guid inviteId, Guid callerId, string callerRole)
        {
            var invite = await _inviteRepo.GetByIdAsync(inviteId)
                ?? throw new InvalidOperationException("Invite not found.");

            // A PDO may only regenerate their own invites; Admin/SuperAdmin
            // can regenerate any, same override they already have on creation.
            if (callerRole == "PDO" && invite.InvitedByUserId != callerId)
                throw new UnauthorizedAccessException("You can only regenerate an invite you created.");

            if (invite.Status != 0)
                throw new InvalidOperationException("This invite has already been used or is no longer valid.");

            var plainCode = GenerateCode();
            invite.CodeHash = HashCode(plainCode);
            invite.ExpiresAt = DateTime.UtcNow.AddDays(CodeValidityDays);
            await _inviteRepo.SaveChangesAsync();

            return new FocalInviteResultDto
            {
                InviteId = invite.Id,
                Code = plainCode,
                ExpiresAt = invite.ExpiresAt
            };
        }

        private static string GenerateCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(CodeLength);
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
            return new string(chars);
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));
            return Convert.ToHexString(bytes);
        }
    }
}
