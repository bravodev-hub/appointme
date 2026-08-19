using AppointMe.Shared.Database;
using AppointMe.Shared.Database.Dapper;
using AppointMe.Shared.Pagination;
using Dapper;

namespace AppointMe.Crm.Customers.Database;

public sealed class CustomersRepository(IDbConnectionFactory connectionFactory)
{
    private const string CustomersSqlTemplate =
        """
        SELECT
            [Id],
            [FirstName],
            [LastName],
            [DateOfBirth],
            [Gender],
            [Email],
            [RegistrationDate]
            /**totalcount**/
        FROM [crm].[Customers]
        /**where**/
        ORDER BY [RegistrationDate] DESC, [Id] DESC
        /**pagination**/
        """;

    private async Task<CustomerDto?> GetById(CustomerId customerId, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var builder = new ExtSqlBuilder()
            .Where("[CompanyId] = @CompanyId", new { CompanyId = companyId.Value })
            .Where("[Id] = @Id", new { Id = customerId.Value })
            .Where("[IsDeleted] = 0");

        var template = builder.AddTemplate(CustomersSqlTemplate);

        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<CustomerRecord>(
            new CommandDefinition(template.RawSql, template.Parameters, cancellationToken: cancellationToken));
        return record?.ToDto();
    }

    public async Task<CustomerDto> LoadById(CustomerId customerId, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        return await GetById(customerId, companyId, cancellationToken) ??
               throw new NotFoundException($"Customer with id='{customerId.Value}' was not found");
    }

    public async Task<PagedResult<CustomerDto>> GetAll(string[] searchTokens, PaginationFilter pagination,
        CompanyId companyId, CancellationToken cancellationToken)
    {
        var builder = new ExtSqlBuilder()
            .AddPagination(pagination)
            .Where("[CompanyId] = @CompanyId", new { CompanyId = companyId.Value })
            .Where("[IsDeleted] = 0")
            .WhereSearch("[SearchKey]", searchTokens);

        var template = builder.AddTemplate(CustomersSqlTemplate);

        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var (records, totalCount) = await connection
            .QueryWithPaginationAsync<CustomerRecord>(template.RawSql, template.Parameters);
        return records.Select(record => record.ToDto()).ToPagedResult(pagination, totalCount);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetRegistrationDates(DateTimeOffsetPeriod range,
        CompanyId companyId, CancellationToken cancellationToken)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var commandDefinition = new CommandDefinition(
            """
            SELECT
                [RegistrationDate]
            FROM [crm].[Customers]
            WHERE
                [CompanyId] = @CompanyId
                AND [IsDeleted] = 0
                AND [RegistrationDate] >= @From
                AND [RegistrationDate] < @To
            """, new
            {
                CompanyId = companyId.Value,
                From = range.Start,
                To = range.End
            }, cancellationToken: cancellationToken);
        var results = await connection.QueryAsync<DateTimeOffset>(commandDefinition);
        return results.ToList();
    }
}
