using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IStatisticsService
    {
        Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request);
        Task<StatisticsMembersPagedResultDto> GetStatisticsMembersAsync(StatisticsMembersRequestDto request);
        Task<byte[]> ExportGranteesAsync(StatisticsMembersRequestDto request);

        // "Download FINDES Upload File" — same filtered/bucket row set as
        // ExportGranteesAsync above, but written in the FINDES & WEACCESS
        // fund-transfer upload layout (Bank Code, BICFI, TelMobile, Email,
        // Purpose, CreditorName, CreditorAddress, CreditorAcctNum,
        // TransferAmount) instead of the full grantee audit sheet.
        Task<byte[]> ExportFindesUploadAsync(StatisticsMembersRequestDto request);
        Task InvalidateStatisticsCacheAsync();
    }
}