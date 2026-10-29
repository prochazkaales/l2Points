using L2Points.Domain.Time;
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
        ct.ThrowIfCancellationRequested();

        foreach (var role in new[] { Roles.Admin, Roles.Player })
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            ThrowIfFailed(roleResult, $"Creating role '{role}' failed");
        }

        // Only an active admin counts: if all admins were deactivated, nobody could sign in.
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Any(u => u.IsActive))
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

        // Self-healing: a previous run may have created the user but failed before assigning roles.
        var admin = await userManager.FindByNameAsync(options.UserName);
        if (admin is null)
        {
            var now = timeProvider.GetUtcNow();
            admin = new AppUser
            {
                UserName = options.UserName,
                Nick = options.Nick,
                IsActive = true,
                ActiveFrom = Week.Containing(AppTime.TodayInPrague(timeProvider)).Monday,
                CreatedAt = now.UtcDateTime,
            };

            ThrowIfFailed(await userManager.CreateAsync(admin, options.Password), "Creating admin account failed");
        }
        else if (!admin.IsActive)
        {
            // Lockout recovery: no active admin is left, so the config admin is brought back.
            admin.IsActive = true;
            admin.DeactivatedAt = null;
            ThrowIfFailed(await userManager.UpdateAsync(admin), "Reactivating admin account failed");
        }

        foreach (var role in new[] { Roles.Admin, Roles.Player })
        {
            if (!await userManager.IsInRoleAsync(admin, role))
            {
                ThrowIfFailed(await userManager.AddToRoleAsync(admin, role), $"Assigning role '{role}' to admin failed");
            }
        }
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
