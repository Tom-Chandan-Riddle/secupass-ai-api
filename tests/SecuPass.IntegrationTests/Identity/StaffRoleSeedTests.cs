using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class StaffRoleSeedTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public async Task Migrations_SeedExactlyTheThreeStaffRoles()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var roleNames = await roleManager.Roles
            .Select(role => role.Name)
            .OrderBy(name => name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([StaffRoles.Admin, StaffRoles.ClientViewer, StaffRoles.Staff], roleNames);
    }
}