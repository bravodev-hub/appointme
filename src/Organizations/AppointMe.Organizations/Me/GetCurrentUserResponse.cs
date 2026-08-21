namespace AppointMe.Organizations.Me;

public sealed record GetCurrentUserResponse
{
    public required bool IsAuthenticated { get; init; }
    public Guid? UserId { get; init; }
    public string? Name { get; init; }
    public string? Initials { get; init; }
    public string? Email { get; init; }
    public bool HasPendingInvitations { get; init; }
    public IReadOnlyList<CompanyMembership>? Companies { get; init; }
    public bool HasMembership { get; init; }
    public bool IsSuperAdmin { get; init; }
}

public sealed record CompanyMembership
{
    public required Guid CompanyId { get; init; }
    public required string CompanyName { get; init; }
    public required string TimeZone { get; init; }
}
