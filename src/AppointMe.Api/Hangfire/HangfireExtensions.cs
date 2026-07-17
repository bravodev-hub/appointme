using AppointMe.Api.Authorization;
using AppointMe.Shared.Jobs;
using Hangfire;
using Hangfire.SqlServer;

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
            return endpoints
                .MapHangfireDashboard("/admin/jobs", new DashboardOptions { Authorization = [] })
                .RequireAuthorization(HangfireDashboardPolicy.Name);
        }
    }
}
