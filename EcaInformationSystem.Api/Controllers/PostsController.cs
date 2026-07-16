using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/posts")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IHubContext<PostsHub> _hub;
        private const string ViewerKeyHeader = "X-Viewer-Key";

        public PostsController(IPostService postService, IHubContext<PostsHub> hub)
        {
            _postService = postService;
            _hub = hub;
        }

        private Guid? GetUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private string GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        private string GetFullName() => User.FindFirst("FullName")?.Value ?? GetUserName();
        private string? GetPosition() => User.FindFirst("Position")?.Value;
        private string GetRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        private string? ResolveViewerKey()
        {
            var userId = GetUserId();
            if (userId.HasValue) return userId.Value.ToString();

            var headerKey = Request.Headers[ViewerKeyHeader].ToString();
            return string.IsNullOrWhiteSpace(headerKey) ? null : headerKey;
        }

        [HttpGet("feed")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var viewerKey = ResolveViewerKey();
            var userId = GetUserId();
            var result = await _postService.GetFeedAsync(pageNumber, pageSize, viewerKey, userId, GetRole());
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _postService.CreatePostAsync(dto, userId.Value, GetFullName(), GetPosition());

            // ✅ CanDelete is viewer-relative — broadcast a copy with it false so
            // OTHER clients don't render a delete button that isn't theirs to use.
            var broadcastCopy = CloneWithCanDelete(result, false);
            await _hub.Clients.All.SendAsync("PostCreated", broadcastCopy);

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                await _postService.DeletePostAsync(id, userId.Value, GetRole(), GetFullName());
                await _hub.Clients.All.SendAsync("PostDeleted", id);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPost("{id:guid}/like")]
        [AllowAnonymous]
        public async Task<IActionResult> ToggleLike(Guid id)
        {
            var userId = GetUserId();
            var likerKey = ResolveViewerKey();

            if (string.IsNullOrWhiteSpace(likerKey))
                return BadRequest("Missing viewer identity.");

            var result = await _postService.ToggleLikeAsync(id, likerKey, isAnonymous: !userId.HasValue);
            await _hub.Clients.All.SendAsync("LikeUpdated", id, result.LikeCount);
            return Ok(result);
        }

        [HttpPost("{id:guid}/view")]
        [AllowAnonymous]
        public async Task<IActionResult> RecordView(Guid id)
        {
            var viewerKey = ResolveViewerKey();
            if (string.IsNullOrWhiteSpace(viewerKey))
                return Ok();

            var wasNew = await _postService.RecordViewIfNewAsync(id, viewerKey);
            if (wasNew)
            {
                var count = await _postService.GetViewCountAsync(id);
                await _hub.Clients.All.SendAsync("ViewUpdated", id, count);
            }

            return Ok();
        }

        [HttpGet("{id:guid}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _postService.GetCommentsAsync(id, pageNumber, pageSize, GetUserId(), GetRole());
            return Ok(result);
        }

        [HttpPost("{id:guid}/comments")]
        [Authorize]
        public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCommentDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var result = await _postService.AddCommentAsync(id, dto, userId.Value, GetFullName());

            var broadcastCopy = new PostCommentDto
            {
                Id = result.Id,
                PostId = result.PostId,
                UserId = result.UserId,
                AuthorName = result.AuthorName,
                Content = result.Content,
                CreatedAt = result.CreatedAt,
                CanDelete = false // viewer-relative, same reasoning as posts
            };
            await _hub.Clients.All.SendAsync("CommentAdded", id, broadcastCopy);

            return Ok(result);
        }

        [HttpDelete("comments/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(Guid commentId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var postId = await _postService.DeleteCommentAsync(commentId, userId.Value, GetRole(), GetFullName());
                await _hub.Clients.All.SendAsync("CommentDeleted", postId, commentId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        private static PostDto CloneWithCanDelete(PostDto source, bool canDelete) => new()
        {
            Id = source.Id,
            AuthorUserId = source.AuthorUserId,
            AuthorName = source.AuthorName,
            AuthorPosition = source.AuthorPosition,
            Content = source.Content,
            CreatedAt = source.CreatedAt,
            LikeCount = source.LikeCount,
            CommentCount = source.CommentCount,
            ViewCount = source.ViewCount,
            IsLikedByViewer = false, // viewer-relative, same reasoning
            CanDelete = canDelete
        };
    }
}