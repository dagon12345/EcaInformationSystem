using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Infrastructure.Persistence;
using EcaInformationSystem.Infrastructure.Repositories;
using EcaInformationSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EcaInformationSystem.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IBeneficiaryInformationRepository, BeneficiaryInformationRepository>();
            services.AddScoped<IRegionRepository, RegionRepository>();
            services.AddScoped<IProvinceRepository, ProvinceRepository>();
            services.AddScoped<IMunicipalityRepository, MunicipalityRepository>();
            services.AddScoped<IBarangayRepository, BarangayRepository>();
            services.AddScoped<IPendingUserRegistrationRepository, PendingUserRegistrationRepository>();
            services.AddScoped<ILogRepository, LogRepository>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            return services;
        }
    }
}
