using System.Security.Claims;
using L2Points.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class PrincipalValidatorTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private static async Task<ClaimsPrincipal> PrincipalOfAsync(ServiceProvider provider, string userId)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
        return await factory.CreateAsync((await users.FindByIdAsync(userId))!);
    }

    private static async Task<bool> IsStillValidAsync(ServiceProvider provider, ClaimsPrincipal principal)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await PrincipalValidator.IsStillValidAsync(users, principal);
    }

    private static async Task ChangeUserAsync(ServiceProvider provider, string userId, Func<UserManager<AppUser>, AppUser, Task<IdentityResult>> change)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var result = await change(users, (await users.FindByIdAsync(userId))!);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Valid_for_current_user()
    {
        await using var provider = TestServices.Create();
        var bob = await TestData.CreatePlayerAsync(provider, "bob", Monday);
        var principal = await PrincipalOfAsync(provider, bob.Id);

        Assert.True(await IsStillValidAsync(provider, principal));
    }

    [Fact]
    public async Task Invalid_after_stamp_change()
    {
        await using var provider = TestServices.Create();
        var bob = await TestData.CreatePlayerAsync(provider, "bob", Monday);
        var principal = await PrincipalOfAsync(provider, bob.Id);

        await ChangeUserAsync(provider, bob.Id, (users, user) => users.UpdateSecurityStampAsync(user));

        Assert.False(await IsStillValidAsync(provider, principal));
    }

    [Fact]
    public async Task Invalid_for_deactivated_or_deleted_user()
    {
        await using var provider = TestServices.Create();
        var bob = await TestData.CreatePlayerAsync(provider, "bob", Monday);
        var carl = await TestData.CreatePlayerAsync(provider, "carl", Monday);
        var bobPrincipal = await PrincipalOfAsync(provider, bob.Id);
        var carlPrincipal = await PrincipalOfAsync(provider, carl.Id);

        // UpdateAsync keeps the security stamp, so only the IsActive check can reject the principal.
        await ChangeUserAsync(provider, bob.Id, (users, user) =>
        {
            user.IsActive = false;
            return users.UpdateAsync(user);
        });
        await ChangeUserAsync(provider, carl.Id, (users, user) => users.DeleteAsync(user));

        Assert.False(await IsStillValidAsync(provider, bobPrincipal));
        Assert.False(await IsStillValidAsync(provider, carlPrincipal));
    }
}
