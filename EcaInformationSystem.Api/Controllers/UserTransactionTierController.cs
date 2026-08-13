using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Every role can see their own tier — this is a gamified "how much have
    // you done" badge, not an admin report, so it's deliberately NOT gated
    // by the AdminOnly policy LogsController uses for the full activity feed.
    // ✅ REVERTED — the leaderboard ranks internal staff transaction/case
    // volume, not relevant (or appropriate to expose) to an external Focal
    // contact, whose feed access is intentionally view/comment/like/share only.
    [ApiController]
    [Route("api/transaction-tier")]
    [Authorize]
    public class UserTransactionTierController : ControllerBase
    {
        private readonly IUserTransactionTierService _service;
        private readonly ILeaderboardSeasonService _seasonService;
        private readonly ITransactionTierBroadcaster _broadcaster;

        public UserTransactionTierController(
            IUserTransactionTierService service,
            ILeaderboardSeasonService seasonService,
            ITransactionTierBroadcaster broadcaster)
        {
            _service = service;
            _seasonService = seasonService;
            _broadcaster = broadcaster;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            var userName = User.Identity?.Name ?? "System";
            return Ok(await _service.GetTierAsync(userName));
        }

        // Lets a viewer see another user's tier + leaderboard rank from that
        // user's profile page ("stalking" someone's progress) without exposing
        // the full leaderboard to unauthenticated callers — still just [Authorize].
        [HttpGet("user/{userId:guid}")]
        public async Task<IActionResult> GetForUser(Guid userId)
        {
            var tier = await _service.GetTierByUserIdAsync(userId);
            if (tier is null) return NotFound();
            return Ok(tier);
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard()
        {
            var userName = User.Identity?.Name ?? "System";
            return Ok(await _service.GetLeaderboardAsync(userName));
        }

        // Season number + countdown for the leaderboard card header — same
        // [Authorize] as everything else here, no role restriction to just view it.
        [HttpGet("season")]
        public async Task<IActionResult> GetSeason()
            => Ok(await _seasonService.GetActiveSeasonInfoAsync());

        // Manual weekly reset — SuperAdmin only. Ends the active season
        // (snapshotting every account's rank/count/win onto their profile),
        // opens the next one, and broadcasts so every connected client
        // refreshes live instead of showing a stale board.
        [HttpPost("reset")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ResetSeason()
        {
            var resetBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "SuperAdmin";
            var result = await _seasonService.ResetSeasonAsync(resetBy, isAutomatic: false);
            await _broadcaster.NotifyLeaderboardResetAsync(result);
            return Ok(result);
        }
    }
}
