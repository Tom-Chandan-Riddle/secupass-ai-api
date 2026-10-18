using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SecuPass.Infrastructure.Persistence;

public static partial class PersistenceExtensions
{
    private const string DatabaseHealthCheckName = "database";

    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<SecuPassDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<SecuPassDbContext>(DatabaseHealthCheckName, tags: [HealthCheckTags.Readiness]);

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecuPassDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SecuPassDbContext>>();

        // If another host migrates the same database at the same moment, this host can over-report:
        // EF's lock means only one applies each migration, but both may see it as new here.
        var appliedBefore = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        await dbContext.Database.MigrateAsync(cancellationToken);
        var newlyAppliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken))
            .Where(migration => !appliedBefore.Contains(migration))
            .ToList();

        if (newlyAppliedMigrations.Count > 0)
        {
            LogMigrationsApplied(logger, newlyAppliedMigrations.Count, newlyAppliedMigrations);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied {MigrationCount} database migrations: {Migrations}")]
    private static partial void LogMigrationsApplied(ILogger logger, int migrationCount, IReadOnlyList<string> migrations);
}