using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;

namespace EcaInformationSystem.Api.Controllers
{
    // The Client is a pure Blazor WebAssembly app served as static files (no
    // server-side rendering — see the Client's web.config), so a crawler
    // hitting /post/{id} directly always gets the same empty index.html shell:
    // no post-specific title/image/description, hence a "plain link" preview
    // in Messenger/Slack/etc. This endpoint is what PostFeed.razor's
    // CopyPostLinkAsync now copies instead — it lives on the API (which does
    // run server code) and branches on User-Agent: a social-media crawler gets
    // a small static HTML page with real Open Graph tags for that post, while
    // an actual person gets redirected straight to the live client app.
    [ApiController]
    [Route("share")]
    [AllowAnonymous]
    public class SharePostController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IConfiguration _configuration;

        public SharePostController(IPostService postService, IConfiguration configuration)
        {
            _postService = postService;
            _configuration = configuration;
        }

        // Same crawlers major messaging/social apps use to generate link
        // previews — Messenger and Facebook itself both use facebookexternalhit.
        private static readonly string[] CrawlerUserAgentMarkers =
        {
            "facebookexternalhit", "Facebot", "WhatsApp", "TelegramBot",
            "Twitterbot", "LinkedInBot", "Slackbot", "Discordbot",
            "SkypeUriPreview", "Pinterest", "vkShare", "redditbot", "Applebot"
        };

        [HttpGet("post/{id:guid}")]
        public async Task<IActionResult> SharePost(Guid id)
        {
            var clientBaseUrl = (_configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP").TrimEnd('/');
            var postUrl = $"{clientBaseUrl}/post/{id}";

            var post = await _postService.GetPostByIdAsync(id, viewerKey: null, viewerUserId: null, viewerRole: null);
            if (post is null)
                return Redirect(clientBaseUrl);

            var isCrawler = Request.Headers.TryGetValue("User-Agent", out var uaValues) &&
                CrawlerUserAgentMarkers.Any(marker => uaValues.ToString().Contains(marker, StringComparison.OrdinalIgnoreCase));

            if (!isCrawler)
                return Redirect(postUrl);

            var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
            var (title, description, imageUrl) = BuildPreview(post, apiBaseUrl, clientBaseUrl);

            var html = $$"""
                <!DOCTYPE html>
                <html>
                <head>
                <meta charset="utf-8" />
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <meta property="og:title" content="{{WebUtility.HtmlEncode(title)}}" />
                <meta property="og:description" content="{{WebUtility.HtmlEncode(description)}}" />
                <meta property="og:image" content="{{WebUtility.HtmlEncode(imageUrl)}}" />
                <meta property="og:url" content="{{WebUtility.HtmlEncode(postUrl)}}" />
                <meta property="og:type" content="article" />
                <meta property="og:site_name" content="ECA-InFORMS" />
                <meta name="twitter:card" content="summary_large_image" />
                </head>
                <body>
                <p>{{WebUtility.HtmlEncode(description)}}</p>
                <a href="{{WebUtility.HtmlEncode(postUrl)}}">View this post on ECA-InFORMS</a>
                </body>
                </html>
                """;

            return Content(html, "text/html", Encoding.UTF8);
        }

        private static (string Title, string Description, string ImageUrl) BuildPreview(PostDto post, string apiBaseUrl, string clientBaseUrl)
        {
            var fallbackImage = $"{clientBaseUrl}/images/ncsc-seal.png";

            switch (post.PostType)
            {
                case 1: // LeaderboardPodium
                    var winner = post.Podium.FirstOrDefault(p => p.Rank == 1);
                    return (
                        $"Season {post.SeasonNumber} Champions!",
                        post.Content,
                        winner is not null ? $"{apiBaseUrl}/api/UserProfile/{winner.UserId}/avatar/thumb" : fallbackImage);

                case 2: // BirthdayGreeting
                    return (
                        $"Happy Birthday, {post.BirthdayUserName}!",
                        post.Content,
                        post.BirthdayUserId.HasValue
                            ? $"{apiBaseUrl}/api/UserProfile/{post.BirthdayUserId}/avatar/thumb"
                            : fallbackImage);

                default:
                    var description = string.IsNullOrWhiteSpace(post.Content)
                        ? "Tap to view this post on ECA-InFORMS."
                        : Truncate(post.Content, 200);
                    var image = post.Images.Any()
                        ? $"{apiBaseUrl}/api/posts/images/{post.Images.First().Id}"
                        : $"{apiBaseUrl}/api/UserProfile/{post.AuthorUserId}/avatar/thumb";
                    return ($"{post.AuthorName} on ECA-InFORMS", description, image);
            }
        }

        private static string Truncate(string text, int maxLength) =>
            text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
