using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Models;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _beneficiaryInformationService;
        private readonly IPayrollJobTracker _jobTracker;
        private readonly IPayrollFileStorageService _fileStorage;

        public PayrollController(
            IBeneficiaryInformationService beneficiaryInformationService,
            IPayrollJobTracker jobTracker,
            IPayrollFileStorageService fileStorage)
        {
            _beneficiaryInformationService = beneficiaryInformationService;
            _jobTracker = jobTracker;
            _fileStorage = fileStorage;
        }

        /// <summary>
        /// POST api/payroll/generate-payroll
        /// LEGACY/SYNCHRONOUS endpoint — kept as fallback for small batches.
        /// At 2000+ records this risks IIS 503s. Prefer queue-payroll for anything large.
        /// </summary>
        [HttpPost("generate-payroll")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GeneratePayroll([FromBody] PayrollSettingsDto settings)
        {
            if (settings?.Ids == null || !settings.Ids.Any())
                return BadRequest("No record IDs provided.");

            try
            {
                var fileBytes = await _beneficiaryInformationService.GeneratePayrollAsync(settings);
                var fileName = $"CashGiftPayroll_{DateTime.Today:yyyy-MM-dd}.zip";

                return File(fileBytes, "application/zip", fileName);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Payroll generation failed: {ex.Message}"); }
        }

        /// <summary>
        /// POST api/payroll/queue-payroll
        /// Queues payroll generation as a background job and returns immediately (202).
        /// Use for any batch that could be large — sidesteps IIS/ANCM request timeouts.
        /// </summary>
        [HttpPost("queue-payroll")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> QueuePayroll([FromBody] PayrollSettingsDto settings)
        {
            if (settings?.Ids == null || !settings.Ids.Any())
                return BadRequest("No record IDs provided.");

            try
            {
                var jobId = await _beneficiaryInformationService.QueuePayrollGenerationAsync(settings);
                return Accepted(new { jobId });
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Failed to queue payroll generation: {ex.Message}"); }
        }

        /// <summary>
        /// GET api/payroll/status/{jobId}
        /// Poll after queue-payroll to check progress.
        /// </summary>
        [HttpGet("status/{jobId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetStatus(Guid jobId)
        {
            var job = _jobTracker.GetJob(jobId);
            if (job == null)
                return NotFound(new { message = "Job not found. It may have expired or the server restarted." });

            return Ok(new
            {
                jobId = job.JobId,
                state = job.State.ToString(),
                totalRecords = job.TotalRecords,
                errorMessage = job.ErrorMessage,
                createdAt = job.CreatedAt,
                completedAt = job.CompletedAt
            });
        }

        /// <summary>
        /// GET api/payroll/download/{jobId}
        /// Call only after status returns "Completed". Cleans up the temp file
        /// and removes job tracking once downloaded.
        /// </summary>
        [HttpGet("download/{jobId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult Download(Guid jobId)
        {
            var job = _jobTracker.GetJob(jobId);
            if (job == null)
                return NotFound(new { message = "Job not found. It may have expired or the server restarted." });

            if (job.State == PayrollJobState.Failed)
                return BadRequest(new { message = job.ErrorMessage ?? "Payroll generation failed." });

            if (job.State != PayrollJobState.Completed || string.IsNullOrWhiteSpace(job.FilePath))
                return BadRequest(new { message = "File is not ready yet." });

            if (!_fileStorage.Exists(job.FilePath))
            {
                _jobTracker.RemoveJob(jobId);
                return NotFound(new { message = "Generated file is missing or already downloaded." });
            }

            var bytes = _fileStorage.ReadAndDelete(job.FilePath);
            var fileName = job.FileName ?? $"CashGiftPayroll_{DateTime.Today:yyyy-MM-dd}.zip";

            _jobTracker.RemoveJob(jobId);

            return File(bytes, "application/zip", fileName);
        }
    }
}