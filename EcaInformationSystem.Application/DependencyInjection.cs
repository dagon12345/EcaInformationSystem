using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EcaInformationSystem.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IBeneficiaryInformationService, BeneficiaryInformationService>();
            services.AddScoped<IRegionService, RegionService>();
            services.AddScoped<IProvinceService, ProvinceService>();
            services.AddScoped<IMunicipalityService, MunicipalityService>();
            services.AddScoped<IBarangayService, BarangayService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<TokenService>();
            services.AddScoped<IBeneficiaryFindingService, BeneficiaryFindingService>();
            services.AddScoped<IAddressSearchService, AddressSearchService>();
            services.AddScoped<IBeneficiaryDocumentService, BeneficiaryDocumentService>();
            services.AddScoped<IPdfCompressionService, PdfCompressionService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IJurisdictionGuardService, JurisdictionGuardService>();
            services.AddScoped<ICoeService, CoeService>();
            services.AddScoped<IImageToPdfService, ImageToPdfService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatAttachmentService, ChatAttachmentService>();
            services.AddScoped<IFormDocumentService, FormDocumentService>();
            services.AddSingleton<IFileShrinkService, FileShrinkService>();
            services.AddSingleton<IShrinkPreviewCache, ShrinkPreviewCache>();
            services.AddScoped<IFormFolderService, FormFolderService>();
            services.AddScoped<IPostService, PostService>();
            services.AddScoped<IPostImageProcessingService, PostImageProcessingService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<IBeneficiaryVerificationChecklistService, BeneficiaryVerificationChecklistService>();
            services.AddScoped<IAnnualGranteeTargetService, AnnualGranteeTargetService>();
            services.AddScoped<IUserProfileService, UserProfileService>();
            services.AddScoped<IStickyNoteService, StickyNoteService>();
            services.AddScoped<IVoiceCallLogService, VoiceCallLogService>();
            services.AddScoped<IFocalBeneficiaryService, FocalBeneficiaryService>();
            services.AddScoped<IFocalInviteService, FocalInviteService>();
            services.AddScoped<IDirectoryService, DirectoryService>();
            services.AddScoped<IWfpEcaService, WfpEcaService>();
            services.AddScoped<IApplicationTrackingService, ApplicationTrackingService>();
            services.AddScoped<IDocumentTrackingService, DocumentTrackingService>();
            services.AddScoped<ISystemUpdateNoticeService, SystemUpdateNoticeService>();
            services.AddScoped<ISeniorCitizenDirectoryService, SeniorCitizenDirectoryService>();
            services.AddScoped<IUserTransactionTierService, UserTransactionTierService>();
            services.AddScoped<ILeaderboardSeasonService, LeaderboardSeasonService>();
            services.AddScoped<IDarReportService, DarReportService>();
            services.AddScoped<IAttendanceLogService, AttendanceLogService>();
            services.AddScoped<IDtrService, DtrService>();
            services.AddScoped<IBiometricDeviceUserService, BiometricDeviceUserService>();
            services.AddScoped<IBiometricSyncStatusService, BiometricSyncStatusService>();
            services.AddScoped<IBiometricDeviceSettingService, BiometricDeviceSettingService>();
            services.AddScoped<IDtrDayMarkService, DtrDayMarkService>();
            return services;
        }
    }
}
