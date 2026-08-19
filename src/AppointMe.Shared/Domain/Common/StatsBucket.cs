namespace AppointMe.Shared.Domain.Common;

public enum StatsBucket
{
    Day,
    Week,
    Month
}

public static class StatsBucketFactory
{
    extension(StatsBucket)
    {
        public static StatsBucket Parse(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "day" => StatsBucket.Day,
                "week" => StatsBucket.Week,
                "month" => StatsBucket.Month,
                _ => throw new ValidationException($"Unknown bucket '{value}'. Expected 'day', 'week' or 'month'.")
            };
        }
    }
}
