using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SecuPass.Domain.Identity;

namespace SecuPass.Infrastructure.Identity;

// Roles are seeded by the migration, so they exist in every environment, not only where a startup seed runs.
// IDs and concurrency stamps are fixed so the model snapshot doesn't change on every migration.
public sealed class StaffRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) => builder.HasData(
        SeededRole(StaffRoles.Admin, "0199f3a0-0000-7000-8000-000000000001"),
        SeededRole(StaffRoles.Staff, "0199f3a0-0000-7000-8000-000000000002"),
        SeededRole(StaffRoles.ClientViewer, "0199f3a0-0000-7000-8000-000000000003"));

    private static IdentityRole<Guid> SeededRole(string name, string id) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}