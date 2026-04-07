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
            return services;
        }
    }
}
