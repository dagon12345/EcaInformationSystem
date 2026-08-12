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
    }
}
