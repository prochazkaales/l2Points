using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Domain.Schedule;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>Weekly attendance grid and attendance changes; enforces the edit rules on the server.</summary>
public sealed class AttendanceService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public async Task<WeekGrid> GetWeekGridAsync(string userId, Week week, bool isAdmin, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var activities = await db.LoadActivitiesAsync(ct);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        var range = week.Range;
        var attendances = await db.Attendances
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= range.From && a.Date <= range.To)
            .Select(a => new AttendanceEntry(a.ActivityId, a.Date, a.Slot))
            .ToListAsync(ct);

        return WeekGridBuilder.Build(activities, week, attendances, AppTime.TodayInPrague(time), trackingStart, isAdmin);
    }

    public async Task<AttendanceChangeResult> SetAttendanceAsync(
        string actorUserId,
        bool actorIsAdmin,
        string targetUserId,
        int activityId,
        DateOnly date,
        EventSlot slot,
        bool attended,
        CancellationToken ct = default)
    {
        if (actorUserId != targetUserId && !actorIsAdmin)
            return AttendanceChangeResult.Forbidden;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var today = AppTime.TodayInPrague(time);
        var trackingStart = await db.GetTrackingStartAsync(ct);
        if (!AttendanceRules.CanEdit(date, today, trackingStart, actorIsAdmin))
            return AttendanceChangeResult.OutsideEditWindow;

        if (!attended)
        {
            // Removal needs no activity check so orphaned attendances (deactivated activity, changed schedule) can be cleaned up.
            await db.Attendances
                .Where(a => a.UserId == targetUserId && a.ActivityId == activityId && a.Date == date && a.Slot == slot)
                .ExecuteDeleteAsync(ct);
            return AttendanceChangeResult.Ok;
        }

        var activity = await db.Activities
            .AsNoTracking()
            .Include(a => a.Spawns)
            .SingleOrDefaultAsync(a => a.Id == activityId, ct);
        if (activity is null || !activity.IsActive)
            return AttendanceChangeResult.UnknownActivity;

        if (!ScheduleCalculator.IsValidSlot(activity, date, slot))
            return AttendanceChangeResult.InvalidSlot;

        if (await ExistsAsync(db, targetUserId, activityId, date, slot, ct))
            return AttendanceChangeResult.Ok;

        db.Attendances.Add(new Attendance
        {
            UserId = targetUserId,
            ActivityId = activityId,
            Date = date,
            Slot = slot,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            CreatedByUserId = actorUserId,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same row (unique index); that outcome is what the caller wanted.
            await using var verify = await dbFactory.CreateDbContextAsync(ct);
            if (!await ExistsAsync(verify, targetUserId, activityId, date, slot, ct))
                throw;
        }

        return AttendanceChangeResult.Ok;
    }

    private static Task<bool> ExistsAsync(AppDbContext db, string userId, int activityId, DateOnly date, EventSlot slot, CancellationToken ct) =>
        db.Attendances.AnyAsync(
            a => a.UserId == userId && a.ActivityId == activityId && a.Date == date && a.Slot == slot, ct);
}
