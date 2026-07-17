using AppointMe.Organizations.Companies;
using AppointMe.Shared.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Organizations.Employees.Database;

public sealed class EmployeeTypeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id)
            .HasConversion(id => id.Value, value => new EmployeeId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(employee => employee.CompanyId)
            .HasConversion(companyId => companyId.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(employee => employee.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(employee => employee.Name, navigationBuilder =>
        {
            navigationBuilder.Property(personName => personName.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(PersonName.FirstNameMaxLength)
                .IsRequired();

            navigationBuilder.Property(personName => personName.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(PersonName.LastNameMaxLength);
        });

        builder.Property(employee => employee.Email)
            .HasConversion(email => email.Value, value => new Email(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.PrimitiveCollection(employee => employee.Roles)
            .ElementType()
            .HasConversion<RoleValueConverter>();

        builder.Property(employee => employee.UserId)
            .HasConversion(userId => userId.Value, value => new UserId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(employee => employee.RegistrationDate)
            .IsRequired();

        builder.Property(employee => employee.IsDeleted)
            .IsRequired();

        builder.HasIndex(employee => new { employee.CompanyId, employee.Email })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property<string>("SearchKey")
            .HasComputedColumnSql(
                "LOWER(CONCAT([FirstName], ' ', ISNULL([LastName], ''), ' ', [Email]))",
                stored: true);

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
