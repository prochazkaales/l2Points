using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>Points and participation of one player.</summary>
public sealed class PlayerStatsService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    /// <summary>Summary of one week, with participation counted over the player's active period (up to today).</summary>
    public async Task<PlayerSummary> GetWeekSummaryAsync(string userId, Week week, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.ActiveFrom, u.DeactivatedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"User '{userId}' does not exist.");

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var window = week.Range;
        var attendances = await db.Attendances
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= window.From && a.Date >= trackingStart && a.Date <= window.To)
            .Select(a => new AttendanceEntry(a.ActivityId, a.Date, a.Slot))
            .ToListAsync(ct);

        var period = PlayerPeriod.Resolve(trackingStart, user.ActiveFrom, user.DeactivatedAt, AppTime.TodayInPrague(time));
        return PlayerSummaryCalculator.Calculate(activities, attendances, window, period);
    }

    /// <summary>Overall, current-week and weekly history statistics of a player, or null when the player does not exist or is hidden from stats.</summary>
    public async Task<PlayerDetail?> GetPlayerDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var player = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.ShowInStats)
            .SelectPlayerInfo()
            .SingleOrDefaultAsync(ct);
        if (player is null)
            return null;

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var today = AppTime.TodayInPrague(time);
        var week = Week.Containing(today);
        var to = StatsWindows.LoadUntil(today, week);
        var entries = await db.Attendances
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= trackingStart && a.Date <= to)
            .Select(a => new AttendanceEntry(a.ActivityId, a.Date, a.Slot))
            .ToListAsync(ct);

        var row = LeaderboardCalculator
            .Build([player], activities, entries.Select(e => new UserAttendance(userId, e)).ToList(), week, trackingStart, today)
            .Single();
        var history = PlayerHistoryCalculator.Build(player, activities, entries, trackingStart, today);
        var jewels = await db.LoadJewelsByPlayerAsync(ct);
        return new PlayerDetail(player, row.Overall, row.Week, history, jewels.Of(userId), await db.IsReaderAsync(userId, ct));
    }
}
