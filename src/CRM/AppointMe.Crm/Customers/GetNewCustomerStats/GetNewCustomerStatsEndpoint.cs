namespace AppointMe.Crm.Customers.GetNewCustomerStats;

internal sealed class GetNewCustomerStatsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/crm/dashboard/new-customers", GetNewCustomerStats)
            .WithName(nameof(GetNewCustomerStats));
    }

    private static async Task<GetNewCustomerStatsResponse> GetNewCustomerStats(
        [AsParameters] GetNewCustomerStatsRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        return await bus.InvokeAsync<GetNewCustomerStatsResponse>(request.ToQuery(), cancellationToken);
    }
}
