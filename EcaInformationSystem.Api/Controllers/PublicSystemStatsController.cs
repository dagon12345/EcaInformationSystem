using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Anonymous, read-only headline numbers for the public Features page —
    // "look how much data this system already handles." Only aggregate
    // counts are exposed here, never any record detail.
    [ApiController]
    [Route("api/public-system-stats")]
    [AllowAnonymous]
    public class PublicSystemStatsController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _beneficiaryService;
        private readonly ILogRepository _logRepository;

        public PublicSystemStatsController(IBeneficiaryInformationService beneficiaryService, ILogRepository logRepository)
        {
            _beneficiaryService = beneficiaryService;
            _logRepository = logRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var granteeCount = await _beneficiaryService.GetMatchingCountAsync(new BeneficiaryFilterDto());
            var transactionCount = await _logRepository.CountAllAsync();

            return Ok(new PublicSystemStatsDto
            {
                GranteeCount = granteeCount,
                TransactionCount = transactionCount
            });
        }
    }
}
