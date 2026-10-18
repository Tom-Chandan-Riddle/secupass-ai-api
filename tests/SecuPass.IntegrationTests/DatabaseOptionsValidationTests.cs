using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SecuPass.Infrastructure.Persistence;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests;

// Runs the same validator ValidateOnStart uses, without starting a host, so there is no race with host shutdown.
public sealed class DatabaseOptionsValidationTests
{
    private const string ValidConnectionString = "Host=localhost;Database=secupass;Username=secupass;Password=unused";

    [Fact]
    public void Validate_WithMissingConnectionString_ThrowsOptionsValidationException()
    {
        using var services = BuildServices(connectionString: null);

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankConnectionString_ThrowsOptionsValidationException(string connectionString)
    {
        using var services = BuildServices(connectionString);

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void Validate_WithConnectionString_DoesNotThrow()
    {
        using var services = BuildServices(ValidConnectionString);

        services.GetRequiredService<IStartupValidator>().Validate();
    }

    // A null connection string leaves the key out of configuration entirely.
    private static ServiceProvider BuildServices(string? connectionString)
    {
        Dictionary<string, string?> settings = connectionString is null
            ? []
            : new() { [SecuPassApiFactory.ConnectionStringKey] = connectionString };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddPersistence()
            .BuildServiceProvider();
    }
}