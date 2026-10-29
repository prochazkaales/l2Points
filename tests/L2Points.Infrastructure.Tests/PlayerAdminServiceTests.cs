using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class PlayerAdminServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private sealed record Arranged(ServiceProvider Provider, PlayerAdminService Service, AppUser Admin) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();

        public async Task<AppUser?> FindAsync(string userName)
        {
            using var scope = Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByNameAsync(userName);
        }

        public async Task<AppUser> FindByIdAsync(string id)
        {
            using var scope = Provider.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(id))!;
        }

        public async Task<IList<string>> RolesOfAsync(AppUser user)
        {
            using var scope = Provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().GetRolesAsync(user);
        }

        public async Task<bool> CheckPasswordAsync(string userId, string password)
        {
            using var scope = Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return await users.CheckPasswordAsync((await users.FindByIdAsync(userId))!, password);
        }

        public async Task<int> UserCountAsync()
        {
            await using var db = Provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            return await db.Users.CountAsync();
        }

        public async Task SetNickAsync(string userName, string nick)
        {
            using var scope = Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(userName))!;
            user.Nick = nick;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
    }

    private static async Task<Arranged> ArrangeAsync()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-29T12:00:00Z"));
        var provider = TestServices.Create(timeProvider: time);

        using (var scope = provider.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Player })
            {
                Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            }
        }

        var admin = await TestData.CreatePlayerAsync(provider, "admin", Monday);
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tracked = (await users.FindByIdAsync(admin.Id))!;
            Assert.True((await users.AddToRolesAsync(tracked, [Roles.Admin, Roles.Player])).Succeeded);
        }

        var service = new PlayerAdminService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            time);
        return new Arranged(provider, service, admin);
    }

    [Fact]
    public async Task Create_returns_password_that_signs_in()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.CreateAsync("karel", "Karel", null);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Password));
        var user = await arranged.FindAsync("karel");
        Assert.NotNull(user);
        Assert.True(await arranged.CheckPasswordAsync(user.Id, result.Password));
        Assert.Equal([Roles.Player], await arranged.RolesOfAsync(user));
        Assert.Equal(Monday, user.ActiveFrom);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Create_uses_given_active_from_and_trims()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.CreateAsync(" eva ", "  Eva ", new DateOnly(2026, 11, 2));

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var user = await arranged.FindAsync("eva");
        Assert.NotNull(user);
        Assert.Equal("eva", user.UserName);
        Assert.Equal("Eva", user.Nick);
        Assert.Equal(new DateOnly(2026, 11, 2), user.ActiveFrom);
    }

    [Fact]
    public async Task Create_rejects_duplicate_nick_case_insensitive()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.CreatePlayerAsync(arranged.Provider, "zofie", Monday);
        await arranged.SetNickAsync("zofie", "Žofie");

        var result = await arranged.Service.CreateAsync("z2", "žOFIE", null);

        Assert.Equal(PlayerAdminStatus.NickTaken, result.Status);
        Assert.Null(await arranged.FindAsync("z2"));
    }

    [Fact]
    public async Task Create_rejects_duplicate_login()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);

        var result = await arranged.Service.CreateAsync("BOB", "Bobek", null);

        Assert.Equal(PlayerAdminStatus.LoginTaken, result.Status);
        Assert.Null(result.Password);
    }

    [Fact]
    public async Task Create_rejects_blank_or_long_nick_and_invalid_login()
    {
        await using var arranged = await ArrangeAsync();
        var before = await arranged.UserCountAsync();

        var blank = await arranged.Service.CreateAsync("ok1", "   ", null);
        var tooLong = await arranged.Service.CreateAsync("ok2", new string('x', AppUser.NickMaxLength + 1), null);
        var badLogin = await arranged.Service.CreateAsync("a b", "Nick", null);

        Assert.Equal(PlayerAdminStatus.Invalid, blank.Status);
        Assert.Equal(PlayerAdminStatus.Invalid, tooLong.Status);
        Assert.Equal(PlayerAdminStatus.Invalid, badLogin.Status);
        Assert.False(string.IsNullOrWhiteSpace(badLogin.Message));
        Assert.Equal(before, await arranged.UserCountAsync());
    }

    [Fact]
    public async Task Update_changes_nick_and_active_from()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        await TestData.CreatePlayerAsync(arranged.Provider, "alice", Monday);
        await arranged.SetNickAsync("alice", "Alice");

        var changed = await arranged.Service.UpdateAsync(bob.Id, "Bobík", new DateOnly(2026, 10, 19));
        var stored = await arranged.FindByIdAsync(bob.Id);
        Assert.Equal(PlayerAdminStatus.Ok, changed.Status);
        Assert.Equal("Bobík", stored.Nick);
        Assert.Equal(new DateOnly(2026, 10, 19), stored.ActiveFrom);

        var sameNickOtherCase = await arranged.Service.UpdateAsync(bob.Id, "BOBÍK", new DateOnly(2026, 10, 19));
        Assert.Equal(PlayerAdminStatus.Ok, sameNickOtherCase.Status);
        Assert.Equal("BOBÍK", (await arranged.FindByIdAsync(bob.Id)).Nick);

        var taken = await arranged.Service.UpdateAsync(bob.Id, "alice", Monday);
        Assert.Equal(PlayerAdminStatus.NickTaken, taken.Status);
        Assert.Equal("BOBÍK", (await arranged.FindByIdAsync(bob.Id)).Nick);

        var invalid = await arranged.Service.UpdateAsync(bob.Id, "  ", Monday);
        Assert.Equal(PlayerAdminStatus.Invalid, invalid.Status);

        var unknown = await arranged.Service.UpdateAsync("missing", "Nick", Monday);
        Assert.Equal(PlayerAdminStatus.UnknownPlayer, unknown.Status);
    }

    [Fact]
    public async Task Reset_password_replaces_old_and_unlocks()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        string oldStamp;
        using (var scope = arranged.Provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tracked = (await users.FindByIdAsync(bob.Id))!;
            await users.SetLockoutEndDateAsync(tracked, DateTimeOffset.UtcNow.AddHours(1));
            oldStamp = (await users.GetSecurityStampAsync(tracked))!;
        }

        var result = await arranged.Service.ResetPasswordAsync(bob.Id);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Password));
        Assert.False(await arranged.CheckPasswordAsync(bob.Id, "Passw0rd!x"));
        Assert.True(await arranged.CheckPasswordAsync(bob.Id, result.Password));
        using (var scope = arranged.Provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tracked = (await users.FindByIdAsync(bob.Id))!;
            Assert.False(await users.IsLockedOutAsync(tracked));
            Assert.NotEqual(oldStamp, await users.GetSecurityStampAsync(tracked));
        }
    }

    [Fact]
    public async Task Reset_password_of_unknown_player_reports_unknown()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.ResetPasswordAsync("missing");

        Assert.Equal(PlayerAdminStatus.UnknownPlayer, result.Status);
        Assert.Null(result.Password);
    }

    [Fact]
    public async Task Get_players_lists_all_with_admin_flag()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        await TestData.CreatePlayerAsync(arranged.Provider, "carl", Monday, deactivatedAt: new DateOnly(2026, 10, 28));
        await arranged.SetNickAsync("admin", "Admin");
        await arranged.SetNickAsync("bob", "bob");
        await arranged.SetNickAsync("carl", "Carl");

        var players = await arranged.Service.GetPlayersAsync();

        Assert.Equal(["Admin", "bob", "Carl"], players.Select(p => p.Nick).ToArray());
        Assert.Equal([true, false, false], players.Select(p => p.IsAdmin).ToArray());
        var carl = players.Single(p => p.Login == "carl");
        Assert.False(carl.IsActive);
        Assert.Equal(new DateOnly(2026, 10, 28), carl.DeactivatedAt);
        Assert.Equal(Monday, carl.ActiveFrom);
    }
}
