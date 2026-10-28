using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>Leaderboard and week overview over all players.</summary>
public sealed class OverviewService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    /// <summary>
    /// Leaderboard ordered by overall points. Overall stats cover tracking start to today; the week columns cover <paramref name="week"/>.
    /// Inactive players are only included on request.
    /// </summary>
    public async Task<IReadOnlyList<LeaderboardRow>> GetLeaderboardAsync(Week week, bool includeInactive, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var players = await db.Users
            .AsNoTracking()
            .Where(u => includeInactive || u.IsActive)
            .Select(ToPlayerInfo)
            .ToListAsync(ct);

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var today = AppTime.TodayInPrague(time);
        var attendances = await LoadAttendancesAsync(db, trackingStart, today, week, includeInactive ? null : players, ct);

        return LeaderboardCalculator.Build(players, activities, attendances, week, trackingStart, today);
    }

    /// <summary>
    /// Week overview: active players plus inactive players who attended something that week,
    /// ordered by the week's points, and the events with their attendees.
    /// </summary>
    public async Task<WeekOverview> GetWeekOverviewAsync(Week week, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var today = AppTime.TodayInPrague(time);
        var attendances = await LoadAttendancesAsync(db, trackingStart, today, week, null, ct);

        var range = week.Range;
        var attendedThisWeek = attendances
            .Where(a => range.Contains(a.Entry.Date))
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        var players = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive || attendedThisWeek.Contains(u.Id))
            .Select(ToPlayerInfo)
            .ToListAsync(ct);

        var rows = LeaderboardCalculator.Build(players, activities, attendances, week, trackingStart, today)
            .OrderByDescending(r => r.Week.Points)
            .ThenBy(r => r.Player.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Player.UserId, StringComparer.Ordinal)
            .ToList();

        var events = WeekEventsCalculator.Build(players, activities, attendances, week);
        return new WeekOverview(week, rows, events);
    }

    private static readonly System.Linq.Expressions.Expression<Func<AppUser, PlayerInfo>> ToPlayerInfo =
        u => new PlayerInfo(u.Id, u.Nick, u.IsActive, u.ActiveFrom, u.DeactivatedAt);

    /// <summary>Attendances from tracking start to the later of today and the end of the week (one query).</summary>
    private static async Task<List<UserAttendance>> LoadAttendancesAsync(
        AppDbContext db, DateOnly trackingStart, DateOnly today, Week week, IReadOnlyList<PlayerInfo>? onlyPlayers, CancellationToken ct)
    {
        var to = today > week.Sunday ? today : week.Sunday;
        var query = db.Attendances
            .AsNoTracking()
            .Where(a => a.Date >= trackingStart && a.Date <= to);

        if (onlyPlayers is not null)
        {
            var ids = onlyPlayers.Select(p => p.UserId).ToList();
            query = query.Where(a => ids.Contains(a.UserId));
        }

        return await query
            .Select(a => new UserAttendance(a.UserId, new AttendanceEntry(a.ActivityId, a.Date, a.Slot)))
            .ToListAsync(ct);
    }
}
