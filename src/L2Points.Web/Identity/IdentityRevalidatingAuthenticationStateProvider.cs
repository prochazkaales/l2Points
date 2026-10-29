using L2Points.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;

namespace L2Points.Web.Identity;

/// <summary>
/// Rechecks the user of a running circuit every minute, so deactivation, deletion and security stamp
/// changes (password reset, role change) end the interactive session, not only the cookie.
/// </summary>
public sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // A fresh scope per check: the circuit-scoped UserManager would cache the user for the whole connection.
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await PrincipalValidator.IsStillValidAsync(users, authenticationState.User);
    }
}
