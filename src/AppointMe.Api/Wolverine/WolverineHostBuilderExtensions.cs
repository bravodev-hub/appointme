using AppointMe.Api.Wolverine.HandlerContext;
using AppointMe.Shared.Domain;
using AppointMe.Shared.Domain.Errors;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.Attributes;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.SqlServer;

[assembly: WolverineModule]

namespace AppointMe.Api.Wolverine;

public static class HostBuilderExtensions
{
    extension(IHostBuilder builder)
    {
        public void AddWolverine(IConfiguration configuration, IHostEnvironment environment, bool isCodegen)
        {
            var transport = configuration["Wolverine:Transport"] ?? "SqlDurable";

            builder.UseWolverine(options =>
            {
                options.PersistMessagesWithSqlServer(
                    configuration.GetConnectionString("AppointMeSql")
                    ?? throw new InvalidOperationException("Connection string 'AppointMeSql' not found"));

                options.Durability.DurabilityAgentEnabled = !isCodegen;

                options.CodeGeneration.TypeLoadMode = environment.IsDevelopment()
                    ? TypeLoadMode.Dynamic
                    : TypeLoadMode.Static;

                options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

                options.Durability.MessageStorageSchemaName = "wolverine";
                options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
                options.Durability.MessageIdentity = MessageIdentity.IdAndDestination;

                options.Policies.Add<TenantContextPolicy>();
                options.Policies.Add<DomainEventContextPolicy>();
                options.Policies.Add<IdentityContextPolicy>();
                options.Policies.Add<PrincipalContextPolicy>();
                options.Policies.Add<CompanyContextPolicy>();

                options.UseEntityFrameworkCoreTransactions();
                options.PublishDomainEventsFromEntityFrameworkCore<AggregateRoot>(root => root.Events);
                options.Policies.UseDurableLocalQueues();
                options.Policies.AutoApplyTransactions();

                options.OnException<ValidationException>().MoveToErrorQueue();

                switch (transport)
                {
                    case "SqlDurable":
                        // SQL durable local queues only — already wired above. No external broker.
                        break;
                    case "AzureServiceBus":
                        var serviceBusConnection = configuration.GetConnectionString("AppointMeMessaging")
                                                   ?? throw new InvalidOperationException(
                                                       "Connection string 'AppointMeMessaging' is required when Wolverine:Transport=AzureServiceBus.");
                        options.UseAzureServiceBus(serviceBusConnection).AutoProvision();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown Wolverine:Transport '{transport}'. Expected 'SqlDurable' or 'AzureServiceBus'.");
                }
            });
        }
    }
}
