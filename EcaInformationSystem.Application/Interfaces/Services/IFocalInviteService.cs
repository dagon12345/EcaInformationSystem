using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Shared.DTOs.Auth;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IFocalInviteService
    {
        Task<List<MyJurisdictionDto>> GetMyJurisdictionsAsync(Guid pdoUserId);
        Task<FocalInviteResultDto> CreateInviteAsync(Guid inviterId, string inviterRole, CreateFocalInviteRequest request);
        Task<FocalInvitePreviewDto> PreviewInviteAsync(string code);
        Task<AuthResult> AcceptInviteAsync(AcceptFocalInviteRequest request, string ipAddress, string userAgent);

        // Pending (not yet accepted, not expired) invites the caller created
        // themselves — lets a PDO/Admin recover a link they never got around
        // to copying, without needing to store the plain code anywhere.
        Task<List<FocalInviteSummaryDto>> GetMyPendingInvitesAsync(Guid callerId);

        // Issues a brand-new code (and expiry) for an existing pending invite
        // and invalidates the old one — the only way to get a link back,
        // since the original plain code was never stored.
        Task<FocalInviteResultDto> RegenerateInviteLinkAsync(Guid inviteId, Guid callerId, string callerRole);
    }
}
