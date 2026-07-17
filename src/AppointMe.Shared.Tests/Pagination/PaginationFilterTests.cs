using AppointMe.Shared.Pagination;

namespace AppointMe.Shared.Tests.Pagination;

public class PaginationFilterTests
{
    [Fact]
    public void should_clamp_limit_down_to_max_ceiling()
    {
        var filter = new PaginationFilter(limit: 1000, page: 1);
        Assert.Equal(100, filter.Limit);
    }

    [Fact]
    public void should_clamp_limit_up_to_min_when_below_range()
    {
        var filter = new PaginationFilter(limit: 0, page: 1);
        Assert.Equal(1, filter.Limit);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(100, 100)]
    public void should_preserve_limit_within_range(int limit, int expected)
    {
        var filter = new PaginationFilter(limit, page: 1);
        Assert.Equal(expected, filter.Limit);
    }

    [Fact]
    public void should_clamp_page_up_to_one_when_below_min()
    {
        var filter = new PaginationFilter(limit: 10, page: 0);
        Assert.Equal(1, filter.Page);
    }

    [Fact]
    public void should_compute_offset_from_clamped_limit_and_page()
    {
        var filter = new PaginationFilter(limit: 25, page: 3);
        Assert.Equal(50, filter.Offset);
    }
}
