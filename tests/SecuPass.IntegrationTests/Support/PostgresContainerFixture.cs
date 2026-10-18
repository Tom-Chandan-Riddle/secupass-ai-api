using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SecuPass.IntegrationTests.Support.PostgresContainerFixture))]

namespace SecuPass.IntegrationTests.Support;

// One container for the whole test run. D11: same image as compose.yaml.
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}