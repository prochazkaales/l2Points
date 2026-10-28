using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Identity;

public class IdentitySeeder(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminOptions> adminOptions,
    TimeProvider timeProvider)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in new[] { Roles.Admin, Roles.Player })
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            ThrowIfFailed(roleResult, $"Creating role '{role}' failed");
        }

        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
        {
            return;
        }

        var options = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.UserName)
            || string.IsNullOrWhiteSpace(options.Password)
            || string.IsNullOrWhiteSpace(options.Nick))
        {
            throw new InvalidOperationException(
                "No admin account exists. Set Admin__UserName, Admin__Password and Admin__Nick.");
        }

        var now = timeProvider.GetUtcNow();
        var admin = new AppUser
        {
            UserName = options.UserName,
            Nick = options.Nick,
            IsActive = true,
            ActiveFrom = DateOnly.FromDateTime(now.UtcDateTime),
            CreatedAt = now.UtcDateTime,
        };

        ThrowIfFailed(await userManager.CreateAsync(admin, options.Password), "Creating admin account failed");
        ThrowIfFailed(await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.Player]), "Assigning admin roles failed");
    }

    private static void ThrowIfFailed(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{message}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
