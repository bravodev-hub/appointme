using AppointMe.Booking.Attendees;
using AppointMe.Booking.BookingCompanies;
using AppointMe.Booking.ServiceProviders;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppointMe.Booking.Appointments.Database;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<Appointment>
{
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcDateTimeOffsetConverter = new(
        value => value.UtcDateTime,
        value => new DateTimeOffset(value, TimeSpan.Zero));

    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.Id)
            .HasConversion(id => id.Value, value => new AppointmentId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(appointment => appointment.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.HasOne<BookingCompany>()
            .WithMany()
            .HasForeignKey(appointment => appointment.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(appointment => appointment.Period, timePeriod =>
        {
            timePeriod.Property(period => period.Start)
                .HasConversion(UtcDateTimeOffsetConverter)
                .HasColumnType("datetime2(0)")
                .HasColumnName("Start")
                .IsRequired();

            timePeriod.Property(period => period.End)
                .HasConversion(UtcDateTimeOffsetConverter)
                .HasColumnType("datetime2(0)")
                .HasColumnName("End")
                .IsRequired();
        });

        builder.Property(appointment => appointment.ProviderId)
            .HasConversion(id => id.Value, value => new ServiceProviderId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(appointment => appointment.AttendeeId)
            .HasConversion(id => id.Value, value => new AttendeeId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(appointment => appointment.Notes)
            .HasConversion(
                notes => notes != null ? notes.Value : null,
                value => value != null ? new LongString(value) : null)
            .HasMaxLength(LongString.MaxLength);

        builder.Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(appointment => appointment.ScheduledAt)
            .HasConversion(UtcDateTimeOffsetConverter)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
