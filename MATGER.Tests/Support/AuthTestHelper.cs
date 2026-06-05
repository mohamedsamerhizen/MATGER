using System.Net.Http.Headers;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MATGER.Tests.Support;

public static class AuthTestHelper
{
    public static async Task<TestUser> CreateUserAsync(
        TestApplicationFactory factory,
        string role,
        string? email = null)
    {
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        email ??= $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@matger.test";

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"{role} Test User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, TestConstants.Password);

        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        var roleResultForUser = await userManager.AddToRoleAsync(user, role);

        Assert.True(roleResultForUser.Succeeded, string.Join(", ", roleResultForUser.Errors.Select(error => error.Description)));

        var token = jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            [role]);

        return new TestUser(user.Id, email, role, token);
    }

    public static void UseBearerToken(this HttpClient client, TestUser user)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
    }

    public static void ClearBearerToken(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }
}

public sealed record TestUser(
    Guid Id,
    string Email,
    string Role,
    string AccessToken);
