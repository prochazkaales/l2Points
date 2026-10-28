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
            .Where(a => a.UserId == userId && a.Date >= window.From && a.Date <= window.To)
            .Select(a => new AttendanceEntry(a.ActivityId, a.Date, a.Slot))
            .ToListAsync(ct);

        var period = PlayerPeriod.Resolve(trackingStart, user.ActiveFrom, user.DeactivatedAt, AppTime.TodayInPrague(time));
        return PlayerSummaryCalculator.Calculate(activities, attendances, window, period);
    }
}
