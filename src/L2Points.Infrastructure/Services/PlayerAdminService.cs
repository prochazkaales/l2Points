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
                    && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin))))
            .ToListAsync(ct);

        return rows
            .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.UserId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Creates a player with a generated password (returned once in the result).</summary>
    public async Task<PlayerAdminResult> CreateAsync(string login, string nick, DateOnly? activeFrom, CancellationToken ct = default)
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
        };

        var password = PasswordGenerator.Generate();
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return FromIdentityErrors(created);

        var role = await users.AddToRoleAsync(user, Roles.Player);
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

    /// <summary>Replaces the password with a generated one (returned once) and clears any lockout.</summary>
    public async Task<PlayerAdminResult> ResetPasswordAsync(string userId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return new PlayerAdminResult(PlayerAdminStatus.UnknownPlayer);

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
