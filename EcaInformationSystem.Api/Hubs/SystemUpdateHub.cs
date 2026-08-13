using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // ✅ Focal accounts need to receive update notices too (see FocalLayout.razor)
    // — the bare [Authorize] here used to resolve to the app's DefaultPolicy,
    // which excludes Focal entirely, so their hub connection/negotiate always
    // 403'd and they silently never saw the "refresh now" prompt.
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class SystemUpdateHub : Hub
    {
    }
}
