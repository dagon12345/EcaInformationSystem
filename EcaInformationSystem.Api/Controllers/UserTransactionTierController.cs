using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Every role can see their own tier — this is a gamified "how much have
    // you done" badge, not an admin report, so it's deliberately NOT gated
    // by the AdminOnly policy LogsController uses for the full activity feed.
    [ApiController]
    [Route("api/transaction-tier")]
    [Authorize]
    public class UserTransactionTierController : ControllerBase
    {
        private readonly IUserTransactionTierService _service;

        public UserTransactionTierController(IUserTransactionTierService service)
        {
            _service = service;
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
    }
}
