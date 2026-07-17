using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Booking.BookingCompanies.Database;

public sealed class BookingCompanyTypeConfiguration : IEntityTypeConfiguration<BookingCompany>
{
    public void Configure(EntityTypeBuilder<BookingCompany> builder)
    {
        builder.ToTable("BookingCompanies");

        builder.HasKey(bookingCompany => bookingCompany.Id);

        builder.Property(bookingCompany => bookingCompany.Id)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(bookingCompany => bookingCompany.Name)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(bookingCompany => bookingCompany.TimeZone)
            .HasConversion(
                timeZone => timeZone.Id,
                value => TimeZoneInfo.FindSystemTimeZoneById(value))
            .HasColumnName("TimeZone")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
