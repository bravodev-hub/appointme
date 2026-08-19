namespace AppointMe.Booking.Dashboard.GetPeakHours;

internal sealed class GetPeakHoursEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/booking/dashboard/peak-hours", GetPeakHours).WithName(nameof(GetPeakHours));
    }

    private static async Task<GetPeakHoursResponse> GetPeakHours(
        [AsParameters] GetPeakHoursRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        return await bus.InvokeAsync<GetPeakHoursResponse>(request.ToQuery(), cancellationToken);
    }
}
