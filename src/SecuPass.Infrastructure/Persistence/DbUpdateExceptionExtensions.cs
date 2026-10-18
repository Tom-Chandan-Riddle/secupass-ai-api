using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace SecuPass.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    // The database's unique index is the final guard when two requests race past an earlier duplicate check.
    public static bool IsUniqueViolationOn(this DbUpdateException exception, string indexName) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && postgresException.ConstraintName == indexName;
}