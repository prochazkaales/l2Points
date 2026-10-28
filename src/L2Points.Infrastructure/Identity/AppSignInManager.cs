using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Identity;

/// <summary>Sign-in manager that refuses deactivated users.</summary>
public class AppSignInManager(
    UserManager<AppUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<AppUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation)
    : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(AppUser user)
    {
        if (!user.IsActive)
        {
            return false;
        }

        return await base.CanSignInAsync(user);
    }
}
