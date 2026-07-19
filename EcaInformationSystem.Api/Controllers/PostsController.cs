using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
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
        private readonly IPsgcNameCache _psgcNameCache;
        private const string ViewerKeyHeader = "X-Viewer-Key";

        public PostsController(IPostService postService, IHubContext<PostsHub> hub, IPsgcNameCache psgcNameCache)
        {
            _postService = postService;
            _hub = hub;
            _psgcNameCache = psgcNameCache;
        }

        private Guid? GetUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private string GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        private string GetFullName() => User.FindFirst("FullName")?.Value ?? GetUserName();
        private string? GetPosition() => User.FindFirst("Position")?.Value;
        private string? GetAuthorRegionName()
        {
            var raw = User.FindFirst("Region")?.Value;
            if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out var code))
                return null;

            return _psgcNameCache.GetRegionName(code);
        }
        private string GetRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        private string? ResolveViewerKey()
        {
            var userId = GetUserId();
            if (userId.HasValue) return userId.Value.ToString();

            var headerKey = Request.Headers[ViewerKeyHeader].ToString();
            return string.IsNullOrWhiteSpace(headerKey) ? null : headerKey;
        }
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPost(Guid id)
        {
            var result = await _postService.GetPostByIdAsync(id, ResolveViewerKey(), GetUserId(), GetRole());
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        [RequestSizeLimit(209_715_200)]
        public async Task<IActionResult> EditPost(
          Guid id,
          [FromForm] string? content,
          [FromForm] string? removeImageIds,
          [FromForm(Name = "newImages")] List<IFormFile>? newImages)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var removeIds = string.IsNullOrWhiteSpace(removeImageIds)
                ? new List<Guid>()
                : removeImageIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();

            var uploads = new List<PostImageUploadDto>();
            foreach (var file in newImages ?? new List<IFormFile>())
            {
                if (file.Length == 0) continue;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                uploads.Add(new PostImageUploadDto { FileName = file.FileName, ContentType = file.ContentType, Data = ms.ToArray() });
            }

            try
            {
                var result = await _postService.EditPostAsync(id, content ?? string.Empty, removeIds, uploads, userId.Value, GetRole());
                await _hub.Clients.All.SendAsync("PostEdited", id, result.Content, result.EditedAt, result.Images);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
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
        [RequestSizeLimit(209_715_200)]
        public async Task<IActionResult> CreatePost(
             [FromForm] string? content,
             [FromForm(Name = "images")] List<IFormFile>? images)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var imgList = images ?? new List<IFormFile>();
            if (imgList.Count > 10)
                return BadRequest("A post can have at most 10 images.");

            var uploads = new List<PostImageUploadDto>();
            foreach (var file in imgList)
            {
                if (file.Length == 0) continue;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                uploads.Add(new PostImageUploadDto
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Data = ms.ToArray()
                });
            }

            var dto = new CreatePostDto { Content = content ?? string.Empty };
            var result = await _postService.CreatePostAsync(dto, uploads, userId.Value, 
                GetFullName(), GetPosition(), GetAuthorRegionName()); // We added the position and region here.

            var broadcastCopy = CloneWithCanDelete(result, false);
            await _hub.Clients.All.SendAsync("PostCreated", broadcastCopy);

            return Ok(result);
        }

        [HttpGet("images/{imageId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetImage(Guid imageId)
        {
            var result = await _postService.GetImageAsync(imageId, thumbnail: false);
            if (result == null) return NotFound();

            Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            return File(result.Value.data, result.Value.contentType);
        }

        [HttpGet("images/{imageId:guid}/thumb")]
        [AllowAnonymous]
        public async Task<IActionResult> GetImageThumbnail(Guid imageId)
        {
            var result = await _postService.GetImageAsync(imageId, thumbnail: true);
            if (result == null) return NotFound();

            Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            return File(result.Value.data, result.Value.contentType);
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

        [HttpPost("{id:guid}/react")]
        [AllowAnonymous]
        public async Task<IActionResult> SetReaction(Guid id, [FromBody] SetPostReactionDto dto)
        {
            var userId = GetUserId();
            var likerKey = ResolveViewerKey();
            if (string.IsNullOrWhiteSpace(likerKey)) return BadRequest("Missing viewer identity.");
            if (dto.ReactionType < 1 || dto.ReactionType > 4) return BadRequest("Invalid reaction type.");

            var result = await _postService.SetReactionAsync(id, likerKey, isAnonymous: !userId.HasValue, dto.ReactionType);
            await _hub.Clients.All.SendAsync("ReactionUpdated", id, result.Reactions);
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

            var result = await _postService.AddCommentAsync(id, dto, userId.Value, 
                GetFullName(), GetPosition(), GetAuthorRegionName());

            var broadcastCopy = new PostCommentDto
            {
                Id = result.Id,
                PostId = result.PostId,
                UserId = result.UserId,
                AuthorName = result.AuthorName,
                AuthorPosition = result.AuthorPosition, //New
                AuthorRegion = result.AuthorRegion, //New
                Content = result.Content,
                CreatedAt = result.CreatedAt,
                CanDelete = false // viewer-relative, same reasoning as posts
            };
            await _hub.Clients.All.SendAsync("CommentAdded", id, broadcastCopy);

            return Ok(result);
        }
        [HttpPut("comments/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> EditComment(Guid commentId, [FromBody] EditCommentDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            try
            {
                var result = await _postService.EditCommentAsync(commentId, dto.Content, userId.Value, GetRole());
                await _hub.Clients.All.SendAsync("CommentEdited", result.PostId, result.Id, result.Content, result.EditedAt);
                return Ok(result);
            }
            catch(UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }  
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
            AuthorRegion = source.AuthorRegion,
            Content = source.Content,
            CreatedAt = source.CreatedAt,
            ViewerReactionType = source.ViewerReactionType,
            CommentCount = source.CommentCount,
            ViewCount = source.ViewCount,
            CanDelete = canDelete,
            Images = source.Images
        };
    }
}