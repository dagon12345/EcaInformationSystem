using EcaInformationSystem.Application.Interfaces;
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
            return services;
        }
    }
}
