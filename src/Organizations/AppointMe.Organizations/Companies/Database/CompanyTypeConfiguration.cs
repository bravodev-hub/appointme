using AppointMe.Organizations.Employees;
using AppointMe.Shared.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Organizations.Companies.Database;

public sealed class CompanyTypeConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Id)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(company => company.Name)
            .HasConversion(name => name.Value, value => CompanyName.Create(value))
            .HasMaxLength(CompanyName.MaxLength)
            .IsRequired();

        builder.Property(company => company.TimeZone)
            .HasConversion(timeZone => timeZone.Id, value => TimeZoneInfo.FindSystemTimeZoneById(value))
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(company => company.RegisteredBy)
            .HasConversion(registeredBy => registeredBy.Value, value => new UserId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(company => company.RegistrationDate)
            .IsRequired();

        builder.Property(company => company.PrimaryOwnerEmployeeId)
            .HasConversion(employeeId => employeeId.HasValue ? employeeId.Value.Value : (Guid?)null,
                value => value.HasValue ? new EmployeeId(value.Value) : null)
            .HasColumnType("uniqueidentifier");

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(company => company.PrimaryOwnerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
