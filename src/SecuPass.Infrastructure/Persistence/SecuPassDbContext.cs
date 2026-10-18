using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.Infrastructure.Persistence;

public sealed class SecuPassDbContext(DbContextOptions<SecuPassDbContext> options)
    : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SecuPassDbContext).Assembly);
    }
}