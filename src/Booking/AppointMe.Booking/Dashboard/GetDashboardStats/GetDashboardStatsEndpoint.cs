namespace AppointMe.Booking.Dashboard.GetDashboardStats;

internal sealed class GetDashboardStatsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/booking/dashboard/stats", GetDashboardStats).WithName(nameof(GetDashboardStats));
    }

    private static async Task<GetDashboardStatsResponse> GetDashboardStats(
        [AsParameters] GetDashboardStatsRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        return await bus.InvokeAsync<GetDashboardStatsResponse>(request.ToQuery(), cancellationToken);
    }
}
