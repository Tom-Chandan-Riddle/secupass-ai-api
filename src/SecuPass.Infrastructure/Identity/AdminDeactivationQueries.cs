using Microsoft.EntityFrameworkCore;

using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Infrastructure.Identity;

public static class AdminDeactivationQueries
{
    // D15: a fixed key, so every deactivation waits for the one before it to commit or roll back.
    private const long DeactivationLockKey = 0x5EC0_DEAC;

    // Holds until the current transaction ends, so it must be called inside one.
    public static Task LockDeactivationsAsync(this SecuPassDbContext dbContext, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({DeactivationLockKey})", cancellationToken);

    public static Task<bool> HasOtherActiveAdminAsync(this SecuPassDbContext dbContext, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.Id != userId
                && user.IsActive
                && dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id && userRole.RoleId == StaffRoleConfiguration.AdminRoleId),
            cancellationToken);
}