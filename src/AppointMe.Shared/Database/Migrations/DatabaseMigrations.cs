using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppointMe.Shared.Database.Migrations;

internal sealed class DatabaseMigrationOptions
{
    private readonly HashSet<Type> _seen = [];
    private readonly List<Type> _ordered = [];

    public IReadOnlyList<Type> ContextTypes => _ordered;

    public void Register<TContext>() where TContext : DbContext
    {
        if (_seen.Add(typeof(TContext)))
        {
            _ordered.Add(typeof(TContext));
        }
    }
}

internal sealed class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseMigrationOptions> options,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        foreach (var contextType in options.Value.ContextTypes)
        {
            await MigrateContext(scope.ServiceProvider, contextType, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateContext(IServiceProvider serviceProvider, Type contextType,
        CancellationToken cancellationToken)
    {
        var context = (DbContext)serviceProvider.GetRequiredService(contextType);

        logger.LogInformation("Applying migrations for {Context}...", contextType.Name);

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migrations applied for {Context}.", contextType.Name);
    }
}

public static class DatabaseMigrationsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDatabaseMigration<TContext>() where TContext : DbContext
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DatabaseMigrationService>());
            services.Configure<DatabaseMigrationOptions>(options => { options.Register<TContext>(); });

            return services;
        }

        public IServiceCollection DisableDatabaseMigrations()
        {
            services.RemoveAll<DatabaseMigrationService>();
            var hostedService = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(DatabaseMigrationService));
            if (hostedService is not null)
            {
                services.Remove(hostedService);
            }

            return services;
        }
    }
}
