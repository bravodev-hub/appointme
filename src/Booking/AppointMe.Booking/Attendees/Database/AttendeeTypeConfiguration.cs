using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Booking.Attendees.Database;

public sealed class AttendeeTypeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("Attendees");

        builder.HasKey(attendee => attendee.Id);

        builder.Property(attendee => attendee.Id)
            .HasConversion(id => id.Value, value => new AttendeeId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(attendee => attendee.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.OwnsOne(attendee => attendee.Name, name =>
        {
            name.Property(personName => personName.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(PersonName.FirstNameMaxLength)
                .IsRequired();

            name.Property(personName => personName.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(PersonName.LastNameMaxLength);
        });

        builder.Property(attendee => attendee.DateOfBirth)
            .HasConversion(
                dob => dob != null ? dob.Value : default,
                value => new DateOfBirth(value))
            .HasColumnType("date");

        builder.Property(attendee => attendee.Email)
            .HasConversion(
                email => email != null ? email.Value : null,
                value => value != null ? new Email(value) : null)
            .HasMaxLength(Email.MaxLength);

        builder.Property(attendee => attendee.IsDeleted)
            .IsRequired();

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
