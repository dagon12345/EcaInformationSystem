using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Repositories;
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
            return services;
        }
    }
}
