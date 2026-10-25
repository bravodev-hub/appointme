using Microsoft.AspNetCore.Mvc;

namespace AppointMe.Shared.Pagination;

public class PaginationRequest
{
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    [FromQuery(Name = "page")]
    public int? Page { get; init; }
}

public static class PaginationRequestExtensions
{
    private const int DefaultLimit = 10;
    private const int DefaultPage = 1;

    extension(PaginationRequest request)
    {
        public PaginationFilter ToPaginationFilter()
        {
            return new PaginationFilter(request.Limit ?? DefaultLimit, request.Page ?? DefaultPage);
        }
    }
}
