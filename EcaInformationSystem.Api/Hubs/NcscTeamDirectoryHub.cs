using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Unlike SeniorCitizenDirectoryHub, this directory is nationwide (not
    // region-scoped) — every connected client just joins the default "all
    // clients" broadcast, no per-region groups needed.
    [Authorize]
    public class NcscTeamDirectoryHub : Hub
    {
    }
}
