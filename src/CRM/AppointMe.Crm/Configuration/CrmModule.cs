using AppointMe.Crm.Contracts.Customers;
using AppointMe.Crm.Customers;
using AppointMe.Crm.Customers.Database;
using AppointMe.Crm.Customers.SeedDemoCustomers;
using AppointMe.Crm.Customers.TopUpDemoCustomers;
using AppointMe.Crm.Database;
using AppointMe.Shared.Configuration;
using AppointMe.Shared.Jobs;
using AppointMe.Shared.Authorization.Permissions;
using AppointMe.Shared.Database;
using AppointMe.Shared.Database.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

[assembly: WolverineModule]

namespace AppointMe.Crm.Configuration;

public static class CrmModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCrmModule()
        {
            return services
                .AddDbContext<CrmDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlServer(serviceProvider.GetRequiredService<ConnectionStrings>().AppointMeSql,
                        builder =>
                        {
                            builder.MigrationsHistoryTable("__EFMigrationsHistory", CrmDbContext.DefaultSchema);
                        });
                })
                .AddDatabaseMigration<CrmDbContext>()
                .AddEndpoints(ModuleAssembly.Instance)
                .AddSingleton<IDbConnectionFactory, SqlConnectionFactory>(serviceProvider =>
                    new SqlConnectionFactory(serviceProvider.GetRequiredService<ConnectionStrings>().AppointMeSql))
                .AddScoped<CustomersRepository>()
                .AddScoped<ICustomerRehydrationSource, CustomerRehydrationSource>()
                .AddScoped<SeedDemoCustomers>()
                .AddScoped<TopUpDemoCustomersJob>()
                .AddPermissions(ModuleAssembly.Instance)
                .AddRecurringJobs(ModuleAssembly.Instance);
        }
    }
}
