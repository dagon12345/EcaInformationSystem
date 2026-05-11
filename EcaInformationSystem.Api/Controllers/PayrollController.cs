using EcaInformationSystem.Application.Interfaces;
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
        public PayrollController(IBeneficiaryInformationService beneficiaryInformationService)
        {
            _beneficiaryInformationService = beneficiaryInformationService;
        }
        /// <summary>
        /// POST api/beneficiary/generate-payroll
        /// Body: PayrollSettingsDto (includes Ids + all signatory/CGP settings)
        /// Returns an .xlsx file download.
        /// </summary>
        [HttpPost("generate-payroll")]
        public async Task<IActionResult> GeneratePayroll([FromBody] PayrollSettingsDto settings)
        {
            if (settings?.Ids == null || !settings.Ids.Any())
                return BadRequest("No record IDs provided.");

            try
            {
                var fileBytes = await _beneficiaryInformationService.GeneratePayrollAsync(settings);
                var fileName = $"CashGiftPayroll_{DateTime.Today:yyyy-MM-dd}.zip";

                return File(
                    fileBytes,
                    "application/zip",
                    fileName);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return StatusCode(500, $"Payroll generation failed: {ex.Message}"); }
        }

    }
}
