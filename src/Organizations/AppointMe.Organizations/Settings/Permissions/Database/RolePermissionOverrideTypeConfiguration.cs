using AppointMe.Organizations.Companies;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Organizations.Settings.Permissions.Database;

public sealed class RolePermissionOverrideTypeConfiguration : IEntityTypeConfiguration<RolePermissionOverride>
{
    public void Configure(EntityTypeBuilder<RolePermissionOverride> builder)
    {
        builder.ToTable("RolePermissionOverrides");

        builder.HasKey(permissionOverride => new
        {
            permissionOverride.CompanyId,
            permissionOverride.Role,
            permissionOverride.PermissionCode,
        });

        builder.Property(permissionOverride => permissionOverride.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(permissionOverride => permissionOverride.PermissionCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permissionOverride => permissionOverride.Role)
            .HasMaxLength(Role.MaxLength)
            .IsRequired();

        builder.Property(permissionOverride => permissionOverride.IsGranted)
            .IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(permissionOverride => permissionOverride.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
