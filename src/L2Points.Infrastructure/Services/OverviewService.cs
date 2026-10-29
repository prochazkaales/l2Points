using L2Points.Domain.Stats;
using L2Points.Domain.Time;
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
    public async Task<LeaderboardResult> GetLeaderboardAsync(Week week, bool includeInactive, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var players = await db.Users
            .AsNoTracking()
            .Where(u => includeInactive || u.IsActive)
            .SelectPlayerInfo()
            .ToListAsync(ct);

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var today = AppTime.TodayInPrague(time);
        var attendances = await LoadAttendancesAsync(db, trackingStart, today, week, includeInactive ? null : players, ct);

        var rows = LeaderboardCalculator.Build(players, activities, attendances, week, trackingStart, today);
        // The attendances already reach back to tracking start, so they cover the previous week too.
        var previous = week.Previous();
        int? previousWeekPoints = previous.Sunday < trackingStart
            ? null
            : LeaderboardCalculator.Build(players, activities, attendances, previous, trackingStart, today).Sum(r => r.Week.Points);
        var jewels = await db.LoadJewelsByPlayerAsync(ct);
        return new LeaderboardResult(rows, jewels, previousWeekPoints);
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
            .SelectPlayerInfo()
            .ToListAsync(ct);

        var rows = LeaderboardCalculator.Build(players, activities, attendances, week, trackingStart, today)
            .OrderByDescending(r => r.Week.Points)
            .ThenBy(r => r.Player.Nick, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Player.UserId, StringComparer.Ordinal)
            .ToList();

        var events = WeekEventsCalculator.Build(players, activities, attendances, week);
        var jewels = await db.LoadJewelsByPlayerAsync(ct);
        return new WeekOverview(week, rows, events, jewels);
    }

    /// <summary>Attendances from tracking start to the later of today and the end of the week (one query).</summary>
    private static async Task<List<UserAttendance>> LoadAttendancesAsync(
        AppDbContext db, DateOnly trackingStart, DateOnly today, Week week, IReadOnlyList<PlayerInfo>? onlyPlayers, CancellationToken ct)
    {
        var to = StatsWindows.LoadUntil(today, week);
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
