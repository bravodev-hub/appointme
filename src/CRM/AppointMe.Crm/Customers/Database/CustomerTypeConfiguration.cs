using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Crm.Customers.Database;

public sealed class CustomerTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .HasConversion(id => id.Value, value => new CustomerId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(customer => customer.CompanyId)
            .HasConversion(companyId => companyId.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.OwnsOne(customer => customer.Name, navigationBuilder =>
        {
            navigationBuilder.Property(personName => personName.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(PersonName.FirstNameMaxLength)
                .IsRequired();

            navigationBuilder.Property(personName => personName.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(PersonName.LastNameMaxLength);
        });

        builder.Property(customer => customer.DateOfBirth)
            .HasConversion(dateOfBirth => dateOfBirth != null ? dateOfBirth.Value : default,
                value => new DateOfBirth(value))
            .HasColumnType("date");

        builder.Property(customer => customer.Gender)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(customer => customer.Email)
            .HasConversion(
                email => email != null ? email.Value : null,
                value => value != null ? new Email(value) : null)
            .HasMaxLength(Email.MaxLength);

        builder.Property(customer => customer.RegistrationDate)
            .IsRequired();

        builder.Property(customer => customer.IsDeleted)
            .IsRequired();

        builder.Property<string>("SearchKey")
            .HasComputedColumnSql(
                "LOWER(CONCAT([FirstName], ' ', ISNULL([LastName], ''), ' ', ISNULL([Email], '')))",
                stored: true);

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
