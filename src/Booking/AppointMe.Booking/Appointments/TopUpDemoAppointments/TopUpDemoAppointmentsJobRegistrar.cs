using AppointMe.Shared.Jobs;
using Microsoft.Extensions.Configuration;

namespace AppointMe.Booking.Appointments.TopUpDemoAppointments;

public sealed class TopUpDemoAppointmentsJobRegistrar(IConfiguration configuration) : IRecurringJobRegistrar
{
    private const string JobId = "booking:top-up-demo-appointments";
    private const string CronExpression = "0 3 * * *"; // daily at 03:00 UTC

    public void Register(IRecurringJobScheduler scheduler)
    {
        if (!configuration.GetValue<bool>("Demo:Enabled"))
        {
            return;
        }

        scheduler.AddOrUpdate<TopUpDemoAppointmentsJob>(
            JobId,
            job => job.Run(CancellationToken.None),
            CronExpression);
    }
}
