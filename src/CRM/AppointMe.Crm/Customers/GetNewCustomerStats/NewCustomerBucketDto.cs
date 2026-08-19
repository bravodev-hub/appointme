namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public sealed record NewCustomerBucketDto
{
    public required DateOnly BucketStart { get; init; }
    public required int Count { get; init; }
}
