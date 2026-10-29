using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Identity;

public class IdentitySeeder(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminOptions> adminOptions,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Ensures the roles and the config admin (Admin__UserName): it is always active, has the roles
    /// Admin, Player and SuperAdmin, and is the only super admin. Its existing password is never changed.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var role in new[] { Roles.Admin, Roles.Player, Roles.SuperAdmin })
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
            ThrowIfFailed(roleResult, $"Creating role '{role}' failed");
        }

        var options = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            // Only an active admin counts: if all admins were deactivated, nobody could sign in.
            if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Any(u => u.IsActive))
            {
                return;
            }

            throw new InvalidOperationException(
                "No admin account exists. Set Admin__UserName, Admin__Password and Admin__Nick.");
        }

        // Self-healing: a previous run may have created the user but failed before assigning roles.
        var admin = await userManager.FindByNameAsync(options.UserName);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(options.Password) || string.IsNullOrWhiteSpace(options.Nick))
            {
                throw new InvalidOperationException(
                    "The config admin account does not exist yet. Set Admin__Password and Admin__Nick.");
            }

            var now = timeProvider.GetUtcNow();
            admin = new AppUser
            {
                UserName = options.UserName,
                Nick = options.Nick,
                IsActive = true,
                CreatedAt = now.UtcDateTime,
            };

            ThrowIfFailed(await userManager.CreateAsync(admin, options.Password), "Creating admin account failed");
        }
        else if (!admin.IsActive)
        {
            // Lockout recovery: the super admin can always sign in again.
            admin.IsActive = true;
            admin.DeactivatedAt = null;
            ThrowIfFailed(await userManager.UpdateAsync(admin), "Reactivating admin account failed");
        }

        foreach (var role in new[] { Roles.Admin, Roles.Player, Roles.SuperAdmin })
        {
            if (!await userManager.IsInRoleAsync(admin, role))
            {
                ThrowIfFailed(await userManager.AddToRoleAsync(admin, role), $"Assigning role '{role}' to admin failed");
            }
        }

        // There is exactly one super admin: when Admin__UserName changes, the role moves to the new account.
        foreach (var other in await userManager.GetUsersInRoleAsync(Roles.SuperAdmin))
        {
            if (other.Id != admin.Id)
            {
                ThrowIfFailed(
                    await userManager.RemoveFromRoleAsync(other, Roles.SuperAdmin),
                    $"Removing role '{Roles.SuperAdmin}' from '{other.UserName}' failed");
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
