using AppointMe.Booking.Appointments.Database;
using AppointMe.Booking.Appointments.SeedDemoAppointments;
using AppointMe.Booking.Attendees.ReconcileAttendees;
using AppointMe.Booking.BookingCompanies.ReconcileBookingCompanies;
using AppointMe.Booking.Dashboard.Database;
using AppointMe.Booking.Database;
using AppointMe.Booking.Infrastructure;
using AppointMe.Booking.ServiceProviders.Database;
using AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;
using AppointMe.Shared.Configuration;
using AppointMe.Shared.Jobs;
using AppointMe.Shared.Authorization.Permissions;
using AppointMe.Shared.Database;
using AppointMe.Shared.Database.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

[assembly: WolverineModule]

namespace AppointMe.Booking.Configuration;

public static class BookingModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBookingModule()
        {
            return services
                .AddDbContext<BookingDbContext>((serviceProvider, options) =>
                {
                    options.UseSqlServer(serviceProvider.GetRequiredService<ConnectionStrings>().AppointMeSql,
                        builder =>
                        {
                            builder.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.Schema);
                        });
                })
                .AddDatabaseMigration<BookingDbContext>()
                .AddEndpoints(BookingModuleAssembly.Instance)
                .AddSingleton<IDbConnectionFactory, SqlConnectionFactory>(serviceProvider =>
                    new SqlConnectionFactory(serviceProvider.GetRequiredService<ConnectionStrings>().AppointMeSql))
                .AddScoped<AppointmentsRepository>()
                .AddScoped<ServiceProvidersRepository>()
                .AddScoped<DashboardRepository>()
                .AddScoped<BookingCompanySynchronizer>()
                .AddScoped<BookingCompanyReconciliationJob>()
                .AddScoped<ServiceProviderSynchronizer>()
                .AddScoped<ServiceProviderReconciliationJob>()
                .AddScoped<AttendeeSynchronizer>()
                .AddScoped<AttendeeReconciliationJob>()
                .AddScoped<SeedDemoAppointments>()
                .AddPermissions(BookingModuleAssembly.Instance)
                .AddRecurringJobs(BookingModuleAssembly.Instance);
        }
    }
}
