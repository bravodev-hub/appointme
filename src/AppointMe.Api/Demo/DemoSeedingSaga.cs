using AppointMe.Booking.Appointments.SeedDemoAppointments;
using AppointMe.Booking.Contracts.Attendees.Events;
using AppointMe.Booking.Contracts.ServiceProviders.Events;
using AppointMe.Crm.Customers.SeedDemoCustomers;
using AppointMe.Organizations.Contracts.Companies.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace AppointMe.Api.Demo;

public sealed class DemoSeedingSaga : Saga
{
    // CustomerCount x AppointmentsPerAttendee = ~450 appointments over the -21..+21 day seed window,
    // roughly 80% chair utilization for a single provider.
    private const int CustomerCount = 150;
    private const int AppointmentsPerAttendee = 3;

    public Guid Id { get; set; }
    public int CustomersExpected { get; set; }
    public int AttendeesSeeded { get; set; }
    public bool CustomerSeedingTriggered { get; set; }

    public static DemoSeedingSaga? Start(CompanyRegistered @event, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Demo:Enabled"))
        {
            return null;
        }

        return new DemoSeedingSaga
        {
            Id = @event.CompanyId,
            CustomersExpected = CustomerCount,
        };
    }

    public object? Handle(
        [SagaIdentityFrom(nameof(ServiceProviderProjected.CompanyId))]
        ServiceProviderProjected @event)
    {
        if (CustomerSeedingTriggered)
        {
            return null;
        }

        CustomerSeedingTriggered = true;
        return new SeedDemoCustomersCommand(CustomersExpected)
            .WithCompany(@event.CompanyId);
    }

    public object Handle([SagaIdentityFrom(nameof(AttendeeProjected.CompanyId))] AttendeeProjected @event)
    {
        AttendeesSeeded++;
        if (AttendeesSeeded >= CustomersExpected)
        {
            MarkCompleted();
        }

        return new SeedDemoAppointmentsCommand(@event.AttendeeId, AppointmentsPerAttendee)
            .WithCompany(@event.CompanyId);
    }
}
