using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.IntegrationTests.Support;

public sealed record TestStaffUser(Guid Id, string Email, string Password);

public sealed record TokenResponse(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);

public static class IdentityTestExtensions
{
    public const string LoginPath = "/auth/login";
    public const string RefreshPath = "/auth/refresh";
    public const string CurrentUserPath = "/auth/me";

    // Meets Identity's default password rules (A16).
    public const string TestPassword = "Test-Passw0rd!";

    // Tests arrange their own users. Each gets a unique email, because the whole run shares one database.
    public static async Task<TestStaffUser> CreateStaffUserAsync(this WebApplicationFactory<Program> factory, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var email = $"user-{Guid.NewGuid():N}@test.secupass.local";
        var user = new StaffUser { UserName = email, Email = email };

        ThrowIfFailed(await userManager.CreateAsync(user, TestPassword));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
        return new TestStaffUser(user.Id, email, TestPassword);
    }

    public static async Task UpdateSecurityStampAsync(this WebApplicationFactory<Program> factory, TestStaffUser testUser)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var user = await userManager.FindByIdAsync(testUser.Id.ToString())
            ?? throw new InvalidOperationException("Test user not found.");

        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
    }

    public static Task<HttpResponseMessage> PostLoginAsync(
        this HttpClient client, string email, string password, string? correlationId = null) =>
        client.PostJsonAsync(LoginPath, new { email, password }, correlationId);

    public static async Task<TokenResponse> LoginAsync(this HttpClient client, TestStaffUser user)
    {
        using var response = await client.PostLoginAsync(user.Email, user.Password);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        return tokens ?? throw new InvalidOperationException("Login returned no token body.");
    }

    public static async Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client, string path, object body, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (correlationId is not null)
        {
            request.Headers.Add(HttpClientExtensions.CorrelationHeader, correlationId);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<HttpResponseMessage> GetWithBearerAsync(
        this HttpClient client, string path, string accessToken, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (correlationId is not null)
        {
            request.Headers.Add(HttpClientExtensions.CorrelationHeader, correlationId);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}