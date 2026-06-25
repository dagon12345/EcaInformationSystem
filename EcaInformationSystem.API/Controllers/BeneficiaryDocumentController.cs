using System.Reflection;
using EcaInformationSystem.Api.Extensions;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/beneficiary-documents")]
    [Authorize]  // ✅ base auth — all authenticated users
    public class BeneficiaryDocumentController : ControllerBase
    {
        private readonly IBeneficiaryDocumentService _service;
        private readonly IJurisdictionGuardService _jurisdictionGuardService;

        public BeneficiaryDocumentController(IBeneficiaryDocumentService service, IJurisdictionGuardService jurisdictionGuardService)
        {
            _service = service;
            _jurisdictionGuardService = jurisdictionGuardService;
        }

        // ── Upload — all roles ────────────────────────────────────────────────────
        [HttpPost("{beneficiaryId:guid}/upload")]
        [RequestSizeLimit(209_715_200)]
        [RequestFormLimits(MultipartBodyLengthLimit = 209_715_200)]
        // ✅ No extra [Authorize] needed — base [Authorize] on class covers all authenticated users
        public async Task<IActionResult> Upload(
            Guid beneficiaryId, [FromForm] List<IFormFile> files)
        {
            try
            {
                if (files == null || !files.Any())
                    return BadRequest("No files uploaded.");

                var userName = User.Identity?.Name ?? "System";

                var result = await _service.UploadAsync(beneficiaryId, files, userName);
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Get documents — all roles ─────────────────────────────────────────────
        [HttpGet("{beneficiaryId:guid}")]
        public async Task<IActionResult> GetDocuments(Guid beneficiaryId)
        {
            try
            {
                var docs = await _service.GetByBeneficiaryIdAsync(beneficiaryId);
                return Ok(docs);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Download — all roles ──────────────────────────────────────────────────
        [HttpGet("download/{documentId:guid}")]
        public async Task<IActionResult> Download(Guid documentId)
        {
            try
            {
                var (bytes, fileName) = await _service.DownloadAsync(documentId);
                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex) { return NotFound(ex.Message); }
        }

        // ── Stream / Preview — all roles ──────────────────────────────────────────
        [HttpGet("stream/{documentId:guid}")]
        public async Task<IActionResult> Stream(Guid documentId)
        {
            try
            {
                var (bytes, _) = await _service.DownloadAsync(documentId);
                Response.Headers["Content-Disposition"] = "inline; filename=file.pdf";
                return File(bytes, "application/pdf");
            }
            catch (Exception ex) { return NotFound(ex.Message); }
        }

        // ── Delete — all roles (document delete is allowed for PDO and Viewer too) ─
        [HttpDelete("delete/{documentId:guid}")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Delete(Guid documentId, [FromQuery] int psgcCodeMunicipality)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                var role = User.GetRole();

                var jurisdictionError = await _jurisdictionGuardService.CheckAsync(userName, role, psgcCodeMunicipality);

                if (jurisdictionError is not null)
                    return StatusCode(403, jurisdictionError);

                await _service.SoftDeleteAsync(documentId, userName);
                return NoContent();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}