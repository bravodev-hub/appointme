using AppointMe.Organizations.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddAppointMeAuthorization(this IServiceCollection services)
    {
        services
            .AddSingleton<ICurrentPrincipal, CurrentPrincipal>()
            .AddScoped<ICurrentPrincipalResolver, CurrentPrincipalResolver>()
            .AddScoped<IAuthorizationHandler, RegisteredUserAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, SuperAdminAuthorizationHandler>()
            .AddSingleton<PermissionRegistry>();

        services
            .AddAuthorizationBuilder()
            .AddPolicy(HangfireDashboardPolicy.Name, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new SuperAdminRequirement()))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new RegisteredUserRequirement())
                .Build());

        return services;
    }
}
