using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using SecuPass.Infrastructure.Persistence;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class StaffUserIsActiveMigrationTests(PostgresContainerFixture postgres)
{
    private const string MigrationBeforeIsActive = "20261018193427_AddIdentity";

    // Users who existed before the column was added, such as a seeded dev Admin, must stay able to log in.
    [Fact]
    public async Task AddStaffUserIsActive_MarksExistingUsersActive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = NewDbContextForFreshDatabase();
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeIsActive, cancellationToken: cancellationToken);
        var existingUserId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "AspNetUsers" ("Id", "AccessFailedCount", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
            VALUES ({existingUserId}, 0, FALSE, TRUE, FALSE, FALSE)
            """,
            cancellationToken);

        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        var isActive = await dbContext.Users
            .Where(user => user.Id == existingUserId)
            .Select(user => user.IsActive)
            .SingleAsync(cancellationToken);
        Assert.True(isActive);
    }

    private SecuPassDbContext NewDbContextForFreshDatabase()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = $"secupass_{Guid.NewGuid():N}",
        }.ConnectionString;
        return new SecuPassDbContext(new DbContextOptionsBuilder<SecuPassDbContext>().UseNpgsql(connectionString).Options);
    }
}