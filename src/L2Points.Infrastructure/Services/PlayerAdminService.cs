using L2Points.Domain.Time;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Services;

/// <summary>
/// Admin management of player accounts. Every mutation runs in its own DI scope, because the
/// UserManager injected into a Blazor circuit would live (and cache entities) for the whole connection.
/// </summary>
public sealed class PlayerAdminService(
    IServiceScopeFactory scopes,
    IDbContextFactory<AppDbContext> dbFactory,
    TimeProvider time)
{
    /// <summary>All players including inactive ones, ordered by nick.</summary>
    public async Task<IReadOnlyList<AdminPlayer>> GetPlayersAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Users
            .AsNoTracking()
            .Select(u => new AdminPlayer(
                u.Id,
                u.UserName ?? string.Empty,
                u.Nick,
                u.IsActive,
                u.ActiveFrom,
                u.DeactivatedAt,
                db.UserRoles.Any(ur => ur.UserId == u.Id
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin)),
                db.UserRoles.Any(ur => ur.UserId == u.Id
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.SuperAdmin)),
                db.UserRoles.Any(ur => ur.UserId == u.Id
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Reader)),
                u.ShowInStats))
            .ToListAsync(ct);

        return rows
            .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.UserId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Creates a player (or a read-only reader) with a generated password (returned once in the result).
    /// <paramref name="showInStats"/> only applies to readers; players are always shown in stats.
    /// </summary>
    public async Task<PlayerAdminResult> CreateAsync(
        string login,
        string nick,
        DateOnly? activeFrom,
        bool isReader = false,
        bool showInStats = true,
        CancellationToken ct = default)
    {
        login = login.Trim();
        nick = nick.Trim();

        var nickError = ValidateNick(nick);
        if (nickError is not null)
            return new PlayerAdminResult(PlayerAdminStatus.Invalid, Message: nickError);

        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (await IsNickTakenAsync(users, nick, excludeUserId: null, ct))
            return new PlayerAdminResult(PlayerAdminStatus.NickTaken);

        var user = new AppUser
        {
            UserName = login,
            Nick = nick,
            IsActive = true,
            ActiveFrom = activeFrom ?? Week.Containing(AppTime.TodayInPrague(time)).Monday,
            CreatedAt = time.GetUtcNow().UtcDateTime,
            ShowInStats = !isReader || showInStats,
        };

        var password = PasswordGenerator.Generate();
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return FromIdentityErrors(created);

        var role = await users.AddToRoleAsync(user, isReader ? Roles.Reader : Roles.Player);
        if (!role.Succeeded)
        {
            // Do not leave a half-created account without a role behind.
            await users.DeleteAsync(user);
            return FromIdentityErrors(role);
        }

        return new PlayerAdminResult(PlayerAdminStatus.Ok, Password: password);
    }

    /// <summary>Changes nick and the date from which the player is evaluated. The login stays as it was.</summary>
    public async Task<PlayerAdminResult> UpdateAsync(string userId, string nick, DateOnly activeFrom, CancellationToken ct = default)
    {
        nick = nick.Trim();

        var nickError = ValidateNick(nick);
        if (nickError is not null)
            return new PlayerAdminResult(PlayerAdminStatus.Invalid, Message: nickError);

        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);

        if (await IsNickTakenAsync(users, nick, excludeUserId: user.Id, ct))
            return new PlayerAdminResult(PlayerAdminStatus.NickTaken);

        user.Nick = nick;
        user.ActiveFrom = activeFrom;
        var updated = await users.UpdateAsync(user);
        return updated.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(updated);
    }

    /// <summary>
    /// Replaces the password with a generated one (returned once) and clears any lockout.
    /// Another admin's password may only be reset by the super admin (see <see cref="ProtectionAsync"/>).
    /// </summary>
    public async Task<PlayerAdminResult> ResetPasswordAsync(string actorUserId, string userId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (await ProtectionAsync(users, actorUserId, user) is { } protection)
            return new PlayerAdminResult(protection);

        var removed = await users.RemovePasswordAsync(user);
        if (!removed.Succeeded)
            return FromIdentityErrors(removed);

        var password = PasswordGenerator.Generate();
        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded)
            return FromIdentityErrors(added);

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return new PlayerAdminResult(PlayerAdminStatus.Ok, Password: password);
    }

    /// <summary>
    /// Deactivates (today in Prague becomes DeactivatedAt, the security stamp rotates so sessions end)
    /// or reactivates the player. Setting the current state again changes nothing.
    /// Another admin may only be deactivated by the super admin, the super admin by nobody; reactivating is allowed.
    /// </summary>
    public async Task<PlayerAdminResult> SetActiveAsync(string actorUserId, string userId, bool active, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (!active && actorUserId == userId)
            return new PlayerAdminResult(PlayerAdminStatus.CannotChangeSelf);
        if (!active && await ProtectionAsync(users, actorUserId, user) is { } protection)
            return new PlayerAdminResult(protection);
        if (user.IsActive == active)
            return new PlayerAdminResult(PlayerAdminStatus.Ok);

        user.IsActive = active;
        user.DeactivatedAt = active ? null : AppTime.TodayInPrague(time);
        // UpdateSecurityStampAsync saves the other changes too.
        var saved = active ? await users.UpdateAsync(user) : await users.UpdateSecurityStampAsync(user);
        return saved.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(saved);
    }

    /// <summary>
    /// Grants or revokes the Admin role (a reader cannot become an admin). The security stamp rotates, so the player is signed out
    /// and gets the new roles on the next sign-in. Only the super admin may do this, and the
    /// super admin's own Admin role cannot be changed.
    /// </summary>
    public async Task<PlayerAdminResult> SetAdminAsync(string actorUserId, string userId, bool isAdmin, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (!isAdmin && actorUserId == userId)
            return new PlayerAdminResult(PlayerAdminStatus.CannotChangeSelf);
        if (await users.IsInRoleAsync(user, Roles.SuperAdmin))
            return new PlayerAdminResult(PlayerAdminStatus.SuperAdminProtected);
        if (!await IsSuperAdminAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.RequiresSuperAdmin);
        if (isAdmin && await users.IsInRoleAsync(user, Roles.Reader))
            return new PlayerAdminResult(PlayerAdminStatus.ReaderCannotBeAdmin);
        if (await users.IsInRoleAsync(user, Roles.Admin) == isAdmin)
            return new PlayerAdminResult(PlayerAdminStatus.Ok);

        var changed = isAdmin
            ? await users.AddToRoleAsync(user, Roles.Admin)
            : await users.RemoveFromRoleAsync(user, Roles.Admin);
        if (!changed.Succeeded)
            return FromIdentityErrors(changed);

        var stamp = await users.UpdateSecurityStampAsync(user);
        return stamp.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(stamp);
    }

    /// <summary>
    /// Switches the account between Player and Reader (read-only). The user is shown in stats again either way,
    /// attendance stays as it is, and the security stamp rotates so the session picks up the new role.
    /// An admin (including the super admin) cannot become a reader.
    /// </summary>
    public async Task<PlayerAdminResult> SetReaderAsync(string actorUserId, string userId, bool isReader, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (actorUserId == userId)
            return new PlayerAdminResult(PlayerAdminStatus.CannotChangeSelf);
        if (isReader && await users.IsInRoleAsync(user, Roles.Admin))
            return new PlayerAdminResult(PlayerAdminStatus.AdminCannotBeReader);
        if (await users.IsInRoleAsync(user, Roles.Reader) == isReader)
            return new PlayerAdminResult(PlayerAdminStatus.Ok);

        // Older accounts may lack the Player role, so only remove a role the user actually has.
        var oldRole = isReader ? Roles.Player : Roles.Reader;
        var newRole = isReader ? Roles.Reader : Roles.Player;
        if (await users.IsInRoleAsync(user, oldRole))
        {
            var removed = await users.RemoveFromRoleAsync(user, oldRole);
            if (!removed.Succeeded)
                return FromIdentityErrors(removed);
        }

        if (!await users.IsInRoleAsync(user, newRole))
        {
            var added = await users.AddToRoleAsync(user, newRole);
            if (!added.Succeeded)
                return FromIdentityErrors(added);
        }

        user.ShowInStats = true;
        // UpdateSecurityStampAsync saves the other changes too.
        var stamp = await users.UpdateSecurityStampAsync(user);
        return stamp.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(stamp);
    }

    /// <summary>
    /// Shows or hides a reader in stats (leaderboards, week overview). Only readers can be hidden;
    /// players are always shown. No security stamp rotation, nothing changes for the session.
    /// </summary>
    public async Task<PlayerAdminResult> SetShowInStatsAsync(string actorUserId, string userId, bool show, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (!show && !await users.IsInRoleAsync(user, Roles.Reader))
            return new PlayerAdminResult(PlayerAdminStatus.NotReader);
        if (user.ShowInStats == show)
            return new PlayerAdminResult(PlayerAdminStatus.Ok);

        user.ShowInStats = show;
        var saved = await users.UpdateAsync(user);
        return saved.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(saved);
    }

    /// <summary>
    /// Deletes the player; attendance and jewels go with it (cascading foreign keys).
    /// Another admin may only be deleted by the super admin, the super admin by nobody.
    /// </summary>
    public async Task<PlayerAdminResult> DeleteAsync(string actorUserId, string userId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!await IsAllowedActorAsync(users, actorUserId))
            return new PlayerAdminResult(PlayerAdminStatus.ActorNotAllowed);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);
        if (actorUserId == userId)
            return new PlayerAdminResult(PlayerAdminStatus.CannotChangeSelf);
        if (await ProtectionAsync(users, actorUserId, user) is { } protection)
            return new PlayerAdminResult(protection);

        var deleted = await users.DeleteAsync(user);
        return deleted.Succeeded ? new PlayerAdminResult(PlayerAdminStatus.Ok) : FromIdentityErrors(deleted);
    }

    /// <summary>
    /// The actor must still be an active admin: a deactivated or demoted admin keeps a working
    /// circuit until revalidation, and must not be able to lock the others out meanwhile.
    /// </summary>
    private static async Task<bool> IsAllowedActorAsync(UserManager<AppUser> users, string actorUserId)
    {
        var actor = await users.FindByIdAsync(actorUserId);
        return actor is { IsActive: true } && await users.IsInRoleAsync(actor, Roles.Admin);
    }

    /// <summary>
    /// Admins are off limits for each other: nobody restricts the super admin, and only the super admin
    /// restricts another admin. Null when the actor may proceed (including acting on themselves).
    /// </summary>
    private static async Task<PlayerAdminStatus?> ProtectionAsync(UserManager<AppUser> users, string actorUserId, AppUser target)
    {
        if (actorUserId == target.Id)
            return null;
        if (await users.IsInRoleAsync(target, Roles.SuperAdmin))
            return PlayerAdminStatus.SuperAdminProtected;
        if (await users.IsInRoleAsync(target, Roles.Admin) && !await IsSuperAdminAsync(users, actorUserId))
            return PlayerAdminStatus.AdminProtected;
        return null;
    }

    private static async Task<bool> IsSuperAdminAsync(UserManager<AppUser> users, string actorUserId)
    {
        var actor = await users.FindByIdAsync(actorUserId);
        return actor is not null && await users.IsInRoleAsync(actor, Roles.SuperAdmin);
    }

    private static string? ValidateNick(string nick)
    {
        if (nick.Length == 0)
            return "Přezdívka nesmí být prázdná.";

        return nick.Length > AppUser.NickMaxLength
            ? $"Přezdívka může mít nejvýš {AppUser.NickMaxLength} znaků."
            : null;
    }

    /// <summary>Compared in memory: SQLite upper() does not fold diacritics and SQL Server collation differs.</summary>
    private static async Task<bool> IsNickTakenAsync(UserManager<AppUser> users, string nick, string? excludeUserId, CancellationToken ct)
    {
        var nicks = await users.Users
            .Where(u => u.Id != excludeUserId)
            .Select(u => u.Nick)
            .ToListAsync(ct);
        return nicks.Contains(nick, StringComparer.OrdinalIgnoreCase);
    }

    private static PlayerAdminResult FromIdentityErrors(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
            return new PlayerAdminResult(PlayerAdminStatus.LoginTaken);

        return new PlayerAdminResult(
            PlayerAdminStatus.Invalid,
            Message: string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
