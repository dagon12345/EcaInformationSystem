using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    public class ChatUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        }
    }
}
