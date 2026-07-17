using AppointMe.Api.Authorization;
using AppointMe.Shared.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AppointMe.Api.Hangfire;

public static class HangfireExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppointMeHangfire(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("AppointMeSql")
                                   ?? throw new InvalidOperationException("Connection string 'AppointMeSql' not found");

            services.AddSingleton<SystemContextJobFilter>();

            services.AddHangfire((serviceProvider, config) => config
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true,
                })
                .UseFilter(serviceProvider.GetRequiredService<SystemContextJobFilter>()));

            services.AddHangfireServer();
            services.AddSingleton<IRecurringJobScheduler, HangfireRecurringJobScheduler>();
            services.AddHostedService<RecurringJobsHostedService>();

            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointConventionBuilder MapAppointMeHangfireDashboard()
        {
            // Map the dashboard as a routed endpoint so it flows through the
            // ASP.NET Core authorization pipeline. Hangfire's own dashboard
            // filters are intentionally left empty — access is enforced by
            // RequireAuthorization below, which demands an authenticated,
            // registered user. Never expose this endpoint anonymously.
            return endpoints
                .MapHangfireDashboard("/admin/jobs", new DashboardOptions { Authorization = [] })
                .RequireAuthorization(HangfireDashboardPolicy.Name);
        }
    }
}
