using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Infrastructure.Services;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Features;
using EcaInformationSystem.Infrastructure.Caching;

namespace EcaInformationSystem.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    // 3 minutes for long-running operations like bulk imports
                    sqlOptions.CommandTimeout(180);

                    // Automatically retry transient SQL errors (error 19 "Physical connection
                    // is not usable", error -2 timeout, etc.) before surfacing a failure.
                    // 5 retries with exponential back-off up to 30 s between attempts.
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);   // null = use the default transient-error list
                }));

            services.AddHttpClient("PsgcApi", c =>
            {
                c.BaseAddress = new Uri("https://psgc.gitlab.io/");
                c.Timeout = TimeSpan.FromMinutes(10);
            });
            services.AddScoped<IPsgcSeederService, PsgcSeederService>();

            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IBeneficiaryInformationRepository, BeneficiaryInformationRepository>();
            services.AddScoped<IRegionRepository, RegionRepository>();
            services.AddScoped<IProvinceRepository, ProvinceRepository>();
            services.AddScoped<IMunicipalityRepository, MunicipalityRepository>();
            services.AddScoped<IBarangayRepository, BarangayRepository>();
            services.AddScoped<IPendingUserRegistrationRepository, PendingUserRegistrationRepository>();
            services.AddScoped<ILogRepository, LogRepository>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IBeneficiaryFindingRepository, BeneficiaryFindingRepository>();
            services.AddScoped<IAddressSearchRepository, AddressSearchRepository>();
            services.AddScoped<IBeneficiaryDocumentRepository, BeneficiaryDocumentRepository>();

            // ✅ Payroll background processing — singletons, must outlive any single HTTP request scope
            services.AddSingleton<IPayrollJobTracker, PayrollJobTracker>();
            services.AddSingleton<IPayrollFileStorageService, PayrollFileStorageService>();
            services.AddSingleton<BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundTaskQueue>(sp => sp.GetRequiredService<BackgroundTaskQueue>());
            //Psgc caching
            services.AddSingleton<IPsgcNameCache, PsgcNameCache>();

            // Increase form limits for large PDF uploads
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 209_715_200; // 200MB
            });

            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IFormDocumentRepository, FormDocumentRepository>();
            services.AddScoped<IFormFolderRepository, FormFolderRepository>();
            services.AddScoped<IFormActivityLogRepository, FormActivityLogRepository>();
            services.AddScoped<IPostRepository, PostRepository>();
            services.AddScoped<IPasswordResetRequestRepository, PasswordResetRequestRepository>();
            services.AddScoped<IActivityRepository, ActivityRepository>();
            services.AddScoped<IBeneficiaryVerificationChecklistRepository, BeneficiaryVerificationChecklistRepository>();
            services.AddScoped<IAnnualGranteeTargetRepository, AnnualGranteeTargetRepository>();
            services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            services.AddScoped<IResolvedDuplicatePairRepository, ResolvedDuplicatePairRepository>();
            services.AddScoped<IStickyNoteRepository, StickyNoteRepository>();
            services.AddScoped<IWfpEcaRepository, WfpEcaRepository>();
            return services;
        }
    }
}
