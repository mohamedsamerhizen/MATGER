using Microsoft.AspNetCore.Identity;

namespace MATGER.Api.Identity;

public static class DevelopmentAdminSeeder
{
    private const string AdminEmail = "admin@matger.local";
    private const string AdminPassword = "Admin12345";
    private const string AdminFullName = "MATGER Admin";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existingAdmin = await userManager.FindByEmailAsync(AdminEmail);

        if (existingAdmin is null)
        {
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = AdminFullName,
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, AdminPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException($"Failed to seed development admin user: {errors}");
            }

            var roleResult = await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException($"Failed to assign Admin role to development admin user: {errors}");
            }

            return;
        }

        if (!await userManager.IsInRoleAsync(existingAdmin, ApplicationRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(existingAdmin, ApplicationRoles.Admin);

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException($"Failed to assign Admin role to existing development admin user: {errors}");
            }
        }

        if (!existingAdmin.IsActive)
        {
            existingAdmin.IsActive = true;
            existingAdmin.UpdatedAt = DateTime.UtcNow;

            var updateResult = await userManager.UpdateAsync(existingAdmin);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    updateResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException($"Failed to activate development admin user: {errors}");
            }
        }
    }
}