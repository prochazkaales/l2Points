using L2Points.Domain.Stats;
using L2Points.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Persistence;

public static class PlayerQueries
{
    /// <summary>The single projection of a user to the player data used by stats and overviews.</summary>
    public static IQueryable<PlayerInfo> SelectPlayerInfo(this IQueryable<AppUser> users) =>
        users.Select(u => new PlayerInfo(u.Id, u.Nick, u.IsActive, u.ActiveFrom, u.DeactivatedAt));

    /// <summary>Whether the user has the read-only Reader role.</summary>
    public static Task<bool> IsReaderAsync(this AppDbContext db, string userId, CancellationToken ct = default) =>
        db.UserRoles.AnyAsync(
            ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Reader), ct);

    /// <summary>Whether the user exists and is excluded from stats (ShowInStats is false).</summary>
    public static Task<bool> IsHiddenFromStatsAsync(this AppDbContext db, string userId, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Id == userId && !u.ShowInStats, ct);
}
