using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SecuPass.Api.Features.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// Runs the same validator ValidateOnStart uses, without starting a host, so there is no race with host shutdown.
public sealed class DevelopmentAdminOptionsValidationTests
{
    private const string ValidEmail = "admin@test.secupass.local";
    private const string ValidPassword = "Seed-Admin-Passw0rd!";

    [Theory]
    [InlineData(null, ValidPassword)]
    [InlineData("   ", ValidPassword)]
    [InlineData("not-an-email", ValidPassword)]
    [InlineData(ValidEmail, null)]
    [InlineData(ValidEmail, "   ")]
    public void Validate_WhenEnabledWithMissingOrInvalidSetting_ThrowsOptionsValidationException(string? email, string? password)
    {
        using var services = BuildServices(enabled: "true", email, password);

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void Validate_WhenEnabledWithEmailAndPassword_DoesNotThrow()
    {
        using var services = BuildServices(enabled: "true", ValidEmail, ValidPassword);

        services.GetRequiredService<IStartupValidator>().Validate();
    }

    // Production has no DevelopmentAdmin section at all, and must still start.
    [Fact]
    public void Validate_WithNoSection_DoesNotThrow()
    {
        using var services = BuildServices(enabled: null, email: null, password: null);

        services.GetRequiredService<IStartupValidator>().Validate();
    }

    // A null value leaves the key out of configuration entirely.
    private static ServiceProvider BuildServices(string? enabled, string? email, string? password)
    {
        Dictionary<string, string?> settings = new()
        {
            [SecuPassApiFactory.DevelopmentAdminEnabledKey] = enabled,
            [SecuPassApiFactory.DevelopmentAdminEmailKey] = email,
            [SecuPassApiFactory.DevelopmentAdminPasswordKey] = password,
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Where(setting => setting.Value is not null))
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddIdentityFeature()
            .BuildServiceProvider();
    }
}