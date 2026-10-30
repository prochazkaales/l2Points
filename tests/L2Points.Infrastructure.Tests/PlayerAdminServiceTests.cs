using L2Points.Domain.Jewels;
using L2Points.Domain.Time;
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

    private sealed record Arranged(ServiceProvider Provider, PlayerAdminService Service, AppUser Admin, FakeTimeProvider Time) : IAsyncDisposable
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

        public async Task<string> StampOfAsync(string userId)
        {
            using var scope = Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return (await users.GetSecurityStampAsync((await users.FindByIdAsync(userId))!))!;
        }

        /// <summary>An active admin without the SuperAdmin role.</summary>
        public async Task<AppUser> CreateRegularAdminAsync(string userName)
        {
            var user = await TestData.CreatePlayerAsync(Provider, userName, Monday);
            using var scope = Provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.AddToRolesAsync((await users.FindByIdAsync(user.Id))!, [Roles.Admin, Roles.Player])).Succeeded);
            return user;
        }
    }

    private static async Task<Arranged> ArrangeAsync()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-29T12:00:00Z"));
        var provider = TestServices.Create(timeProvider: time);

        using (var scope = provider.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Admin, Roles.Player, Roles.SuperAdmin, Roles.Reader })
            {
                Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            }
        }

        var admin = await TestData.CreatePlayerAsync(provider, "admin", Monday);
        using (var scope = provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tracked = (await users.FindByIdAsync(admin.Id))!;
            Assert.True((await users.AddToRolesAsync(tracked, [Roles.Admin, Roles.Player, Roles.SuperAdmin])).Succeeded);
        }

        var service = new PlayerAdminService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            time);
        return new Arranged(provider, service, admin, time);
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
            // Lock out through failed attempts: UserManager compares lockout against the system clock, not TimeProvider.
            for (var i = 0; i < 5; i++)
            {
                Assert.True((await users.AccessFailedAsync(tracked)).Succeeded);
            }
            Assert.True(await users.IsLockedOutAsync(tracked));
            oldStamp = (await users.GetSecurityStampAsync(tracked))!;
        }

        var result = await arranged.Service.ResetPasswordAsync(arranged.Admin.Id, bob.Id);

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

        var result = await arranged.Service.ResetPasswordAsync(arranged.Admin.Id, "missing");

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

    [Fact]
    public async Task Deactivate_sets_date_and_rotates_stamp()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        var oldStamp = await arranged.StampOfAsync(bob.Id);

        var result = await arranged.Service.SetActiveAsync(arranged.Admin.Id, bob.Id, false);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var stored = await arranged.FindByIdAsync(bob.Id);
        Assert.False(stored.IsActive);
        Assert.Equal(new DateOnly(2026, 10, 29), stored.DeactivatedAt);
        Assert.NotEqual(oldStamp, await arranged.StampOfAsync(bob.Id));

        arranged.Time.SetUtcNow(DateTimeOffset.Parse("2026-11-03T12:00:00Z"));
        var again = await arranged.Service.SetActiveAsync(arranged.Admin.Id, bob.Id, false);

        Assert.Equal(PlayerAdminStatus.Ok, again.Status);
        Assert.Equal(new DateOnly(2026, 10, 29), (await arranged.FindByIdAsync(bob.Id)).DeactivatedAt);
    }

    [Fact]
    public async Task Reactivate_clears_deactivated_at()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        await arranged.Service.SetActiveAsync(arranged.Admin.Id, bob.Id, false);

        var result = await arranged.Service.SetActiveAsync(arranged.Admin.Id, bob.Id, true);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var stored = await arranged.FindByIdAsync(bob.Id);
        Assert.True(stored.IsActive);
        Assert.Null(stored.DeactivatedAt);
    }

    [Fact]
    public async Task Admin_cannot_restrict_self()
    {
        await using var arranged = await ArrangeAsync();
        var adminId = arranged.Admin.Id;
        var oldStamp = await arranged.StampOfAsync(adminId);

        Assert.Equal(PlayerAdminStatus.CannotChangeSelf, (await arranged.Service.SetActiveAsync(adminId, adminId, false)).Status);
        Assert.Equal(PlayerAdminStatus.CannotChangeSelf, (await arranged.Service.SetAdminAsync(adminId, adminId, false)).Status);
        Assert.Equal(PlayerAdminStatus.CannotChangeSelf, (await arranged.Service.DeleteAsync(adminId, adminId)).Status);

        var stored = await arranged.FindByIdAsync(adminId);
        Assert.True(stored.IsActive);
        Assert.Null(stored.DeactivatedAt);
        Assert.Contains(Roles.Admin, await arranged.RolesOfAsync(stored));
        Assert.Equal(oldStamp, await arranged.StampOfAsync(adminId));

        Assert.Equal(PlayerAdminStatus.Ok, (await arranged.Service.SetActiveAsync(adminId, adminId, true)).Status);
    }

    [Fact]
    public async Task Set_admin_grants_and_revokes_role()
    {
        await using var arranged = await ArrangeAsync();
        var created = await arranged.Service.CreateAsync("bob", "Bob", null);
        var bob = (await arranged.FindAsync("bob"))!;
        Assert.Equal(PlayerAdminStatus.Ok, created.Status);
        var oldStamp = await arranged.StampOfAsync(bob.Id);

        var granted = await arranged.Service.SetAdminAsync(arranged.Admin.Id, bob.Id, true);

        Assert.Equal(PlayerAdminStatus.Ok, granted.Status);
        Assert.Equal([Roles.Admin, Roles.Player], (await arranged.RolesOfAsync(bob)).Order().ToArray());
        var grantedStamp = await arranged.StampOfAsync(bob.Id);
        Assert.NotEqual(oldStamp, grantedStamp);

        var revoked = await arranged.Service.SetAdminAsync(arranged.Admin.Id, bob.Id, false);

        Assert.Equal(PlayerAdminStatus.Ok, revoked.Status);
        Assert.Equal([Roles.Player], await arranged.RolesOfAsync(bob));
        Assert.NotEqual(grantedStamp, await arranged.StampOfAsync(bob.Id));
    }

    [Fact]
    public async Task Delete_removes_player_with_attendance_and_jewels()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.SeedCatalogAsync(arranged.Provider);
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        await TestData.AddAttendanceAsync(arranged.Provider, bob.Id, "QueenAnt", Monday);
        var factory = arranged.Provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        int jewelTypes;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var baium = await db.JewelTypes.Where(t => t.Code == "BaiumRing").Select(t => t.Id).SingleAsync();
            db.UserJewels.Add(new UserJewel { UserId = bob.Id, JewelTypeId = baium });
            await db.SaveChangesAsync();
            jewelTypes = await db.JewelTypes.CountAsync();
        }

        var result = await arranged.Service.DeleteAsync(arranged.Admin.Id, bob.Id);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        Assert.Null(await arranged.FindAsync("bob"));
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.False(await db.Attendances.AnyAsync(a => a.UserId == bob.Id));
            Assert.False(await db.UserJewels.AnyAsync(j => j.UserId == bob.Id));
            Assert.Equal(jewelTypes, await db.JewelTypes.CountAsync());
        }
    }

    [Fact]
    public async Task Inactive_or_non_admin_actor_is_rejected()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        var alice = await TestData.CreatePlayerAsync(arranged.Provider, "alice", Monday);
        var admin2 = await TestData.CreatePlayerAsync(arranged.Provider, "admin2", Monday, deactivatedAt: new DateOnly(2026, 10, 28));
        using (var scope = arranged.Provider.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(bob.Id))!, Roles.Player)).Succeeded);
            Assert.True((await users.AddToRolesAsync((await users.FindByIdAsync(admin2.Id))!, [Roles.Admin, Roles.Player])).Succeeded);
        }

        var byPlayer = await arranged.Service.SetActiveAsync(bob.Id, alice.Id, false);

        Assert.Equal(PlayerAdminStatus.ActorNotAllowed, byPlayer.Status);
        var storedAlice = await arranged.FindByIdAsync(alice.Id);
        Assert.True(storedAlice.IsActive);
        Assert.Null(storedAlice.DeactivatedAt);

        var byInactiveAdmin = await arranged.Service.DeleteAsync(admin2.Id, alice.Id);

        Assert.Equal(PlayerAdminStatus.ActorNotAllowed, byInactiveAdmin.Status);
        Assert.NotNull(await arranged.FindAsync("alice"));
    }

    [Fact]
    public async Task Lifecycle_on_unknown_player()
    {
        await using var arranged = await ArrangeAsync();
        var adminId = arranged.Admin.Id;

        Assert.Equal(PlayerAdminStatus.UnknownPlayer, (await arranged.Service.SetActiveAsync(adminId, "nobody", false)).Status);
        Assert.Equal(PlayerAdminStatus.UnknownPlayer, (await arranged.Service.SetAdminAsync(adminId, "nobody", true)).Status);
        Assert.Equal(PlayerAdminStatus.UnknownPlayer, (await arranged.Service.DeleteAsync(adminId, "nobody")).Status);
        Assert.Equal(PlayerAdminStatus.UnknownPlayer, (await arranged.Service.ResetPasswordAsync(adminId, "nobody")).Status);
    }

    [Fact]
    public async Task Reset_password_rejects_inactive_or_non_admin_actor()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        var alice = await TestData.CreatePlayerAsync(arranged.Provider, "alice", Monday);

        var result = await arranged.Service.ResetPasswordAsync(bob.Id, alice.Id);

        Assert.Equal(PlayerAdminStatus.ActorNotAllowed, result.Status);
        Assert.Null(result.Password);
        Assert.True(await arranged.CheckPasswordAsync(alice.Id, "Passw0rd!x"));
    }

    [Fact]
    public async Task Only_super_admin_changes_admin_role()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");
        var admin3 = await arranged.CreateRegularAdminAsync("admin3");
        var bob = await arranged.Service.CreateAsync("bob", "Bob", null);
        Assert.Equal(PlayerAdminStatus.Ok, bob.Status);
        var bobUser = (await arranged.FindAsync("bob"))!;
        var bobStamp = await arranged.StampOfAsync(bobUser.Id);

        var grant = await arranged.Service.SetAdminAsync(admin2.Id, bobUser.Id, true);
        var revoke = await arranged.Service.SetAdminAsync(admin2.Id, admin3.Id, false);

        Assert.Equal(PlayerAdminStatus.RequiresSuperAdmin, grant.Status);
        Assert.Equal(PlayerAdminStatus.RequiresSuperAdmin, revoke.Status);
        Assert.Equal([Roles.Player], await arranged.RolesOfAsync(bobUser));
        Assert.Equal([Roles.Admin, Roles.Player], (await arranged.RolesOfAsync(admin3)).Order().ToArray());
        Assert.Equal(bobStamp, await arranged.StampOfAsync(bobUser.Id));

        var bySuperAdmin = await arranged.Service.SetAdminAsync(arranged.Admin.Id, bobUser.Id, true);

        Assert.Equal(PlayerAdminStatus.Ok, bySuperAdmin.Status);
        Assert.Equal([Roles.Admin, Roles.Player], (await arranged.RolesOfAsync(bobUser)).Order().ToArray());
    }

    [Fact]
    public async Task Super_admin_is_protected_from_other_admins()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");
        var superId = arranged.Admin.Id;
        var oldStamp = await arranged.StampOfAsync(superId);

        var deactivate = await arranged.Service.SetActiveAsync(admin2.Id, superId, false);
        var delete = await arranged.Service.DeleteAsync(admin2.Id, superId);
        var reset = await arranged.Service.ResetPasswordAsync(admin2.Id, superId);
        var revoke = await arranged.Service.SetAdminAsync(admin2.Id, superId, false);

        Assert.Equal(PlayerAdminStatus.SuperAdminProtected, deactivate.Status);
        Assert.Equal(PlayerAdminStatus.SuperAdminProtected, delete.Status);
        Assert.Equal(PlayerAdminStatus.SuperAdminProtected, reset.Status);
        Assert.Null(reset.Password);
        Assert.Equal(PlayerAdminStatus.SuperAdminProtected, revoke.Status);
        var stored = await arranged.FindByIdAsync(superId);
        Assert.True(stored.IsActive);
        Assert.Null(stored.DeactivatedAt);
        Assert.True(await arranged.CheckPasswordAsync(superId, "Passw0rd!x"));
        Assert.Equal([Roles.Admin, Roles.Player, Roles.SuperAdmin], (await arranged.RolesOfAsync(stored)).Order().ToArray());
        Assert.Equal(oldStamp, await arranged.StampOfAsync(superId));

        var edit = await arranged.Service.UpdateAsync(superId, "Velitel", new DateOnly(2026, 10, 19));

        Assert.Equal(PlayerAdminStatus.Ok, edit.Status);
        Assert.Equal("Velitel", (await arranged.FindByIdAsync(superId)).Nick);
    }

    [Fact]
    public async Task Regular_admin_cannot_restrict_other_admins()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");
        var admin3 = await arranged.CreateRegularAdminAsync("admin3");
        var oldStamp = await arranged.StampOfAsync(admin3.Id);

        var deactivate = await arranged.Service.SetActiveAsync(admin2.Id, admin3.Id, false);
        var delete = await arranged.Service.DeleteAsync(admin2.Id, admin3.Id);
        var reset = await arranged.Service.ResetPasswordAsync(admin2.Id, admin3.Id);

        Assert.Equal(PlayerAdminStatus.AdminProtected, deactivate.Status);
        Assert.Equal(PlayerAdminStatus.AdminProtected, delete.Status);
        Assert.Equal(PlayerAdminStatus.AdminProtected, reset.Status);
        Assert.Null(reset.Password);
        var stored = await arranged.FindByIdAsync(admin3.Id);
        Assert.True(stored.IsActive);
        Assert.True(await arranged.CheckPasswordAsync(admin3.Id, "Passw0rd!x"));
        Assert.Equal(oldStamp, await arranged.StampOfAsync(admin3.Id));

        // Regular players stay manageable by any admin.
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        Assert.Equal(PlayerAdminStatus.Ok, (await arranged.Service.SetActiveAsync(admin2.Id, bob.Id, false)).Status);
        Assert.Equal(PlayerAdminStatus.Ok, (await arranged.Service.ResetPasswordAsync(admin2.Id, bob.Id)).Status);
        Assert.Equal(PlayerAdminStatus.Ok, (await arranged.Service.DeleteAsync(admin2.Id, bob.Id)).Status);
    }

    [Fact]
    public async Task Super_admin_can_restrict_other_admins()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");
        var admin3 = await arranged.CreateRegularAdminAsync("admin3");
        var superId = arranged.Admin.Id;

        var reset = await arranged.Service.ResetPasswordAsync(superId, admin2.Id);
        var deactivate = await arranged.Service.SetActiveAsync(superId, admin2.Id, false);
        var delete = await arranged.Service.DeleteAsync(superId, admin3.Id);

        Assert.Equal(PlayerAdminStatus.Ok, reset.Status);
        Assert.NotNull(reset.Password);
        Assert.Equal(PlayerAdminStatus.Ok, deactivate.Status);
        Assert.False((await arranged.FindByIdAsync(admin2.Id)).IsActive);
        Assert.Equal(PlayerAdminStatus.Ok, delete.Status);
        Assert.Null(await arranged.FindAsync("admin3"));
    }

    [Fact]
    public async Task Get_players_flags_super_admin()
    {
        await using var arranged = await ArrangeAsync();
        await arranged.CreateRegularAdminAsync("admin2");
        await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);

        var players = await arranged.Service.GetPlayersAsync();

        Assert.True(players.Single(p => p.Login == "admin").IsSuperAdmin);
        Assert.False(players.Single(p => p.Login == "admin2").IsSuperAdmin);
        Assert.True(players.Single(p => p.Login == "admin2").IsAdmin);
        Assert.False(players.Single(p => p.Login == "bob").IsSuperAdmin);
    }

    [Fact]
    public async Task Create_Reader_GetsReaderRoleOnly()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.CreateAsync("rita", "Rita", null, isReader: true, showInStats: false);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var user = (await arranged.FindAsync("rita"))!;
        Assert.Equal([Roles.Reader], await arranged.RolesOfAsync(user));
        Assert.False(user.ShowInStats);
    }

    [Fact]
    public async Task Create_HiddenReader_IsStoredHidden()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.SeedCatalogAsync(arranged.Provider);

        var result = await arranged.Service.CreateAsync("rita", "Rita", null, isReader: true, showInStats: false);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var row = (await arranged.Service.GetPlayersAsync()).Single(p => p.Login == "rita");
        Assert.True(row.IsReader);
        Assert.False(row.ShowInStats);
        var overview = arranged.Provider.GetRequiredService<OverviewService>();
        var leaderboard = await overview.GetLeaderboardAsync(Week.Containing(Monday), includeInactive: true);
        Assert.DoesNotContain(leaderboard.Rows, r => r.Player.Nick == "Rita");
    }

    [Fact]
    public async Task Create_PlayerIgnoresShowInStatsFalse()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.CreateAsync("karel", "Karel", null, isReader: false, showInStats: false);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var user = (await arranged.FindAsync("karel"))!;
        Assert.True(user.ShowInStats);
        Assert.Equal([Roles.Player], await arranged.RolesOfAsync(user));
    }

    [Fact]
    public async Task SetReader_Player_BecomesShownReader()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.SeedCatalogAsync(arranged.Provider);
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        await TestData.AddAttendanceAsync(arranged.Provider, bob.Id, "QueenAnt", Monday);
        var oldStamp = await arranged.StampOfAsync(bob.Id);
        var factory = arranged.Provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        int before;
        await using (var db = await factory.CreateDbContextAsync())
            before = await db.Attendances.CountAsync(a => a.UserId == bob.Id);

        var result = await arranged.Service.SetReaderAsync(arranged.Admin.Id, bob.Id, true);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var reloaded = await arranged.FindByIdAsync(bob.Id);
        Assert.Equal([Roles.Reader], await arranged.RolesOfAsync(reloaded));
        Assert.True(reloaded.ShowInStats);
        Assert.NotEqual(oldStamp, await arranged.StampOfAsync(bob.Id));
        await using (var db = await factory.CreateDbContextAsync())
            Assert.Equal(before, await db.Attendances.CountAsync(a => a.UserId == bob.Id));
    }

    [Fact]
    public async Task SetReader_BackToPlayer_ShowsInStatsAgain()
    {
        await using var arranged = await ArrangeAsync();
        var rita = await TestData.CreateReaderAsync(arranged.Provider, "rita", Monday, showInStats: false);

        var result = await arranged.Service.SetReaderAsync(arranged.Admin.Id, rita.Id, false);

        Assert.Equal(PlayerAdminStatus.Ok, result.Status);
        var reloaded = await arranged.FindByIdAsync(rita.Id);
        Assert.Equal([Roles.Player], await arranged.RolesOfAsync(reloaded));
        Assert.True(reloaded.ShowInStats);
    }

    [Fact]
    public async Task SetReader_Admin_IsRejected()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");

        var regular = await arranged.Service.SetReaderAsync(arranged.Admin.Id, admin2.Id, true);
        var superAdmin = await arranged.Service.SetReaderAsync(admin2.Id, arranged.Admin.Id, true);

        Assert.Equal(PlayerAdminStatus.AdminCannotBeReader, regular.Status);
        Assert.Equal(PlayerAdminStatus.AdminCannotBeReader, superAdmin.Status);
        Assert.DoesNotContain(Roles.Reader, await arranged.RolesOfAsync(admin2));
    }

    [Fact]
    public async Task SetReader_Self_IsRejected()
    {
        await using var arranged = await ArrangeAsync();

        var result = await arranged.Service.SetReaderAsync(arranged.Admin.Id, arranged.Admin.Id, false);

        Assert.Equal(PlayerAdminStatus.CannotChangeSelf, result.Status);
    }

    [Fact]
    public async Task SetReader_InactiveActor_IsRejected()
    {
        await using var arranged = await ArrangeAsync();
        var admin2 = await arranged.CreateRegularAdminAsync("admin2");
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);
        Assert.Equal(PlayerAdminStatus.Ok, (await arranged.Service.SetActiveAsync(arranged.Admin.Id, admin2.Id, false)).Status);

        var result = await arranged.Service.SetReaderAsync(admin2.Id, bob.Id, true);

        Assert.Equal(PlayerAdminStatus.ActorNotAllowed, result.Status);
    }

    [Fact]
    public async Task SetShowInStats_Reader_TogglesWithoutStamp()
    {
        await using var arranged = await ArrangeAsync();
        var rita = await TestData.CreateReaderAsync(arranged.Provider, "rita", Monday, showInStats: true);
        var stamp = await arranged.StampOfAsync(rita.Id);

        var hidden = await arranged.Service.SetShowInStatsAsync(arranged.Admin.Id, rita.Id, false);

        Assert.Equal(PlayerAdminStatus.Ok, hidden.Status);
        Assert.False((await arranged.FindByIdAsync(rita.Id)).ShowInStats);

        var shown = await arranged.Service.SetShowInStatsAsync(arranged.Admin.Id, rita.Id, true);

        Assert.Equal(PlayerAdminStatus.Ok, shown.Status);
        Assert.True((await arranged.FindByIdAsync(rita.Id)).ShowInStats);
        Assert.Equal(stamp, await arranged.StampOfAsync(rita.Id));
    }

    [Fact]
    public async Task SetShowInStats_HidePlayer_IsRejected()
    {
        await using var arranged = await ArrangeAsync();
        var bob = await TestData.CreatePlayerAsync(arranged.Provider, "bob", Monday);

        var result = await arranged.Service.SetShowInStatsAsync(arranged.Admin.Id, bob.Id, false);

        Assert.Equal(PlayerAdminStatus.NotReader, result.Status);
        Assert.True((await arranged.FindByIdAsync(bob.Id)).ShowInStats);
    }

    [Fact]
    public async Task SetAdmin_Reader_IsRejected()
    {
        await using var arranged = await ArrangeAsync();
        var rita = await TestData.CreateReaderAsync(arranged.Provider, "rita", Monday, showInStats: true);

        var result = await arranged.Service.SetAdminAsync(arranged.Admin.Id, rita.Id, true);

        Assert.Equal(PlayerAdminStatus.ReaderCannotBeAdmin, result.Status);
        Assert.Equal([Roles.Reader], await arranged.RolesOfAsync(rita));
    }

    [Fact]
    public async Task GetPlayers_IncludesHiddenReaders()
    {
        await using var arranged = await ArrangeAsync();
        await TestData.CreateReaderAsync(arranged.Provider, "rita", Monday, showInStats: false);

        var players = await arranged.Service.GetPlayersAsync();

        var rita = players.Single(p => p.Login == "rita");
        Assert.True(rita.IsReader);
        Assert.False(rita.ShowInStats);
        var admin = players.Single(p => p.Login == "admin");
        Assert.False(admin.IsReader);
        Assert.True(admin.ShowInStats);
    }
}
