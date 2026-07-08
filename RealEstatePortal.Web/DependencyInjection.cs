using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Web.Services;

namespace RealEstatePortal.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUser, CurrentUser>();
        return services;
    }
}