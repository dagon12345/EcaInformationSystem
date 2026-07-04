using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatAttachmentController : ControllerBase
{
    private readonly IChatAttachmentService _attachmentService;

    public ChatAttachmentController(IChatAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpPost("attachments")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        try
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            var uploaderId = Guid.Parse(userIdClaim!);

            using var stream = file.OpenReadStream();
            var attachmentId = await _attachmentService.UploadAttachmentAsync(
                stream, file.FileName, file.ContentType, uploaderId);

            return Ok(new { attachmentId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ✅ Full-resolution fetch — only called when the user taps an image/PDF
    // in the chat bubble. Never bulk-loaded with message history.
    [HttpGet("attachments/{id}")]
    public async Task<IActionResult> GetFull(Guid id)
    {
        var result = await _attachmentService.GetFullAttachmentAsync(id);
        if (result == null) return NotFound();

        return File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
    }
}