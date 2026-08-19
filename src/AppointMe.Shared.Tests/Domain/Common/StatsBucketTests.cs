namespace AppointMe.Shared.Tests.Domain.Common;

public class StatsBucketTests
{
    [Theory]
    [InlineData("day", StatsBucket.Day)]
    [InlineData("Day", StatsBucket.Day)]
    [InlineData("DAY", StatsBucket.Day)]
    [InlineData("week", StatsBucket.Week)]
    [InlineData("Week", StatsBucket.Week)]
    [InlineData("WEEK", StatsBucket.Week)]
    [InlineData("month", StatsBucket.Month)]
    [InlineData("Month", StatsBucket.Month)]
    [InlineData("MONTH", StatsBucket.Month)]
    public void should_parse_valid_values_case_insensitive(string value, StatsBucket expected)
    {
        var result = StatsBucket.Parse(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("year")]
    [InlineData("quarter")]
    [InlineData("")]
    public void should_throw_validation_exception_for_invalid_value(string value)
    {
        var error = Assert.Throws<ValidationException>(() => StatsBucket.Parse(value));

        Assert.Equal($"Unknown bucket '{value}'. Expected 'day', 'week' or 'month'.", error.Message);
    }
}
