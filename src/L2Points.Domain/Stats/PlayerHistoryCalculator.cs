using L2Points.Domain.Activities;
using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class PlayerHistoryCalculator
{
    /// <summary>Per-week summaries from the week of <paramref name="today"/> back to the week of tracking start, newest first.</summary>
    public static IReadOnlyList<WeekHistoryRow> Build(
        PlayerInfo player,
        IReadOnlyList<Activity> activities,
        IReadOnlyCollection<AttendanceEntry> attendances,
        DateOnly trackingStart,
        DateOnly today)
    {
        if (today < trackingStart)
            return [];

        var period = PlayerPeriod.Resolve(trackingStart, player.ActiveFrom, player.DeactivatedAt, today);
        var first = Week.Containing(trackingStart);
        var rows = new List<WeekHistoryRow>();
        for (var week = Week.Containing(today); week.Monday >= first.Monday; week = week.Previous())
            rows.Add(new WeekHistoryRow(week, PlayerSummaryCalculator.Calculate(activities, attendances, week.Range, period)));

        return rows;
    }
}
