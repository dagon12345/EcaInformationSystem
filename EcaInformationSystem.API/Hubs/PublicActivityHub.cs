using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Deliberately NOT [Authorize] — anonymous login-page visitors connect here.
    // This hub only ever broadcasts activities already marked IsPublic; it never
    // touches private activity data, so it's safe to leave open.
    public class PublicActivityHub : Hub
    {
    }
}