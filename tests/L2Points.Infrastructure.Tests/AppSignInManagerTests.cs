using L2Points.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class AppSignInManagerTests
{
    private static async Task<bool> CanSignInAsync(bool isActive)
    {
        await using var provider = TestServices.Create();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = "player1",
            Nick = "Player1",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        var result = await userManager.CreateAsync(user, "Password1");
        Assert.True(result.Succeeded);

        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        Assert.IsType<AppSignInManager>(signInManager);
        return await signInManager.CanSignInAsync(user);
    }

    [Fact]
    public async Task CanSignIn_is_false_for_inactive_user() => Assert.False(await CanSignInAsync(isActive: false));

    [Fact]
    public async Task CanSignIn_is_true_for_active_user() => Assert.True(await CanSignInAsync(isActive: true));
}
