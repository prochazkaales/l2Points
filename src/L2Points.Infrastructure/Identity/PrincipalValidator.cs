using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace L2Points.Infrastructure.Identity;

/// <summary>Decides whether an already signed-in principal (e.g. of a running Blazor circuit) may stay signed in.</summary>
public static class PrincipalValidator
{
    /// <summary>
    /// False when the user no longer exists, is deactivated, or the security stamp in the principal
    /// no longer matches (password reset, role change, deactivation).
    /// </summary>
    public static async Task<bool> IsStillValidAsync(UserManager<AppUser> users, ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null || !user.IsActive)
            return false;

        if (!users.SupportsUserSecurityStamp)
            return true;

        var principalStamp = principal.FindFirstValue(users.Options.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await users.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }
}
