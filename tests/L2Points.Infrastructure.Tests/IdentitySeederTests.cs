using L2Points.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class IdentitySeederTests
{
    private static AdminOptions FullAdmin() => new() { UserName = "admin", Password = "Admin1234", Nick = "Boss" };

    private static async Task SeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    }

    [Fact]
    public async Task Seed_creates_roles_and_admin_when_none_exists()
    {
        await using var provider = TestServices.Create(FullAdmin());

        await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert.True(await roleManager.RoleExistsAsync(Roles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(Roles.Player));

        var user = await userManager.FindByNameAsync("admin");
        Assert.NotNull(user);
        Assert.Equal("Boss", user.Nick);
        Assert.True(user.IsActive);
        Assert.True(await userManager.IsInRoleAsync(user, Roles.Admin));
        Assert.True(await userManager.IsInRoleAsync(user, Roles.Player));
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await using var provider = TestServices.Create(FullAdmin());

        await SeedAsync(provider);
        await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal(1, await userManager.Users.CountAsync());
    }

    [Fact]
    public async Task Seed_throws_when_no_admin_and_config_missing()
    {
        await using var provider = TestServices.Create(new AdminOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(provider));

        Assert.Contains("Admin__UserName", ex.Message);
    }

    [Fact]
    public async Task Seed_does_not_need_config_when_admin_exists()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await using (var first = TestServices.Create(FullAdmin(), connection))
        {
            await SeedAsync(first);
        }

        await using var second = TestServices.Create(new AdminOptions(), connection);
        await SeedAsync(second);
    }

    [Fact]
    public async Task Seed_repairs_existing_admin_user_without_roles()
    {
        await using var provider = TestServices.Create(FullAdmin());

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var created = await userManager.CreateAsync(
                new AppUser { UserName = "admin", Nick = "Boss", ActiveFrom = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow },
                "Admin1234");
            Assert.True(created.Succeeded);
        }

        await SeedAsync(provider);

        using var verifyScope = provider.CreateScope();
        var verifyManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal(1, await verifyManager.Users.CountAsync());
        var user = await verifyManager.FindByNameAsync("admin");
        Assert.NotNull(user);
        Assert.True(await verifyManager.IsInRoleAsync(user, Roles.Admin));
        Assert.True(await verifyManager.IsInRoleAsync(user, Roles.Player));
    }

    private static async Task DeactivateAsync(ServiceProvider provider, string userName)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = (await userManager.FindByNameAsync(userName))!;
        user.IsActive = false;
        user.DeactivatedAt = new DateOnly(2026, 10, 28);
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
    }

    [Fact]
    public async Task Reactivates_config_admin_when_no_active_admin()
    {
        await using var provider = TestServices.Create(FullAdmin());
        await SeedAsync(provider);
        await DeactivateAsync(provider, "admin");

        await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByNameAsync("admin");
        Assert.NotNull(user);
        Assert.True(user.IsActive);
        Assert.Null(user.DeactivatedAt);
        Assert.True(await userManager.IsInRoleAsync(user, Roles.Admin));
        Assert.True(await userManager.IsInRoleAsync(user, Roles.Player));
    }

    [Fact]
    public async Task Does_nothing_when_an_active_admin_exists()
    {
        await using var provider = TestServices.Create(FullAdmin());
        await SeedAsync(provider);
        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var other = new AppUser { UserName = "admin2", Nick = "Boss2", IsActive = true, ActiveFrom = new DateOnly(2026, 10, 26), CreatedAt = DateTime.UtcNow };
            Assert.True((await userManager.CreateAsync(other, "Admin1234")).Succeeded);
            Assert.True((await userManager.AddToRolesAsync(other, [Roles.Admin, Roles.Player])).Succeeded);
        }
        await DeactivateAsync(provider, "admin");

        await SeedAsync(provider);

        using var verifyScope = provider.CreateScope();
        var verifyManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await verifyManager.FindByNameAsync("admin");
        Assert.NotNull(user);
        Assert.False(user.IsActive);
        Assert.Equal(new DateOnly(2026, 10, 28), user.DeactivatedAt);
    }

    [Fact]
    public async Task Seed_sets_active_from_to_monday_in_prague()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        await using var provider = TestServices.Create(FullAdmin(), timeProvider: time);

        await SeedAsync(provider);

        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByNameAsync("admin");
        Assert.NotNull(user);
        Assert.Equal(new DateOnly(2026, 10, 26), user.ActiveFrom);
    }

    [Fact]
    public async Task Seed_throws_with_identity_errors_when_password_invalid()
    {
        var admin = FullAdmin();
        admin.Password = "short";
        await using var provider = TestServices.Create(admin);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(provider));

        var describer = new IdentityErrorDescriber();
        Assert.Contains(describer.PasswordTooShort(8).Description, ex.Message);
    }
}
