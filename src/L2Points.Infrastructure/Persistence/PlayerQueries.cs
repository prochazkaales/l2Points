using L2Points.Domain.Stats;
using L2Points.Infrastructure.Identity;

namespace L2Points.Infrastructure.Persistence;

public static class PlayerQueries
{
    /// <summary>The single projection of a user to the player data used by stats and overviews.</summary>
    public static IQueryable<PlayerInfo> SelectPlayerInfo(this IQueryable<AppUser> users) =>
        users.Select(u => new PlayerInfo(u.Id, u.Nick, u.IsActive, u.DeactivatedAt));
}
