namespace AppointMe.Shared.Pagination;

public sealed record PaginationFilter
{
    private const int MinLimit = 1;
    private const int MaxLimit = 100;

    private const int MinPage = 1;

    public PaginationFilter(int limit, int page)
    {
        Limit = Math.Clamp(limit, MinLimit, MaxLimit);
        Page = Math.Max(page, MinPage);
    }

    public int Limit { get; }
    public int Page { get; }
    public int Offset => Limit * (Page - 1);
}
