using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IStatisticsService
    {
        Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request);
        Task<StatisticsMembersPagedResultDto> GetStatisticsMembersAsync(StatisticsMembersRequestDto request);
        Task InvalidateStatisticsCacheAsync();
    }
}