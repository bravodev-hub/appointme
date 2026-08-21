using AppointMe.Shared.Jobs;
using Microsoft.Extensions.Configuration;

namespace AppointMe.Crm.Customers.TopUpDemoCustomers;

public sealed class TopUpDemoCustomersJobRegistrar(IConfiguration configuration) : IRecurringJobRegistrar
{
    private const string JobId = "crm:top-up-demo-customers";
    private const string CronExpression = "0 3 * * *"; // daily at 03:00 UTC

    public void Register(IRecurringJobScheduler scheduler)
    {
        if (!configuration.GetValue<bool>("Demo:Enabled"))
        {
            return;
        }

        scheduler.AddOrUpdate<TopUpDemoCustomersJob>(
            JobId,
            job => job.Run(CancellationToken.None),
            CronExpression);
    }
}
