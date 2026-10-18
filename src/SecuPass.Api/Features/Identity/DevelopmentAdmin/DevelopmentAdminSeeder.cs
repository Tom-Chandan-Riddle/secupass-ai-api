using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Api.Features.Identity.DevelopmentAdmin;

public sealed partial class DevelopmentAdminSeeder(
    UserManager<StaffUser> userManager,
    SecuPassDbContext dbContext,
    IOptions<DevelopmentAdminOptions> options,
    ILogger<DevelopmentAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var admin = options.Value;
        if (!admin.Enabled)
        {
            return;
        }

        // A18: an existing user keeps its password and role.
        if (await userManager.FindByNameAsync(admin.Email) is { } existingUser)
        {
            LogAdminAlreadyExists(logger, existingUser.Id);
            return;
        }

        var user = await CreateAdminAsync(admin, cancellationToken);
        LogAdminSeeded(logger, user.Id);
    }

    // One transaction, so a failed role assignment can't leave a user without a role that A18 would then keep.
    private async Task<StaffUser> CreateAdminAsync(DevelopmentAdminOptions admin, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new StaffUser { UserName = admin.Email, Email = admin.Email };

        ThrowIfFailed(await userManager.CreateAsync(user, admin.Password));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, StaffRoles.Admin));

        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    // A19: a seed that can't create the Admin stops startup, with Identity's reasons.
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var reasons = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"The Development Admin could not be created: {reasons}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded the Development Admin as staff user {UserId}")]
    private static partial void LogAdminSeeded(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Development Admin already exists as staff user {UserId}; left unchanged")]
    private static partial void LogAdminAlreadyExists(ILogger logger, Guid userId);
}