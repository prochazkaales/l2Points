using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;

namespace L2Points.Domain.Tests.Attendances;

public class AttendanceRulesTests
{
    private static readonly DateOnly TrackingStart = new(2026, 10, 26);

    [Fact]
    public void Player_window_is_today_minus_six()
    {
        var today = new DateOnly(2026, 11, 4);

        Assert.True(AttendanceRules.CanEdit(new DateOnly(2026, 10, 29), today, TrackingStart, isAdmin: false));
        Assert.True(AttendanceRules.CanEdit(new DateOnly(2026, 11, 4), today, TrackingStart, isAdmin: false));
        Assert.False(AttendanceRules.CanEdit(new DateOnly(2026, 10, 28), today, TrackingStart, isAdmin: false));
        Assert.False(AttendanceRules.CanEdit(new DateOnly(2026, 11, 5), today, TrackingStart, isAdmin: false));
        Assert.Equal(new DateOnly(2026, 10, 29), AttendanceRules.EarliestEditableDate(today, TrackingStart, isAdmin: false));
    }

    [Fact]
    public void Player_window_clamped_by_tracking_start()
    {
        var today = new DateOnly(2026, 10, 28);

        Assert.True(AttendanceRules.CanEdit(new DateOnly(2026, 10, 26), today, TrackingStart, isAdmin: false));
        Assert.False(AttendanceRules.CanEdit(new DateOnly(2026, 10, 25), today, TrackingStart, isAdmin: false));
        Assert.Equal(new DateOnly(2026, 10, 26), AttendanceRules.EarliestEditableDate(today, TrackingStart, isAdmin: false));
    }

    [Fact]
    public void Admin_unlimited_back_to_tracking_start()
    {
        var today = new DateOnly(2026, 11, 4);

        Assert.True(AttendanceRules.CanEdit(new DateOnly(2026, 10, 26), today, TrackingStart, isAdmin: true));
        Assert.False(AttendanceRules.CanEdit(new DateOnly(2026, 10, 25), today, TrackingStart, isAdmin: true));
        Assert.False(AttendanceRules.CanEdit(new DateOnly(2026, 11, 5), today, TrackingStart, isAdmin: true));
        Assert.Equal(new DateOnly(2026, 10, 26), AttendanceRules.EarliestEditableDate(today, TrackingStart, isAdmin: true));
    }

    private static Activity Catalog(string code) => DefaultCatalog.CreateActivities().Single(a => a.Code == code);

    [Fact]
    public void Flexible_boss_is_limited_to_expected_per_week()
    {
        var core = Catalog("Core");

        Assert.Equal(4, AttendanceRules.WeeklyLimit(core));
        Assert.False(AttendanceRules.IsWeeklyLimitReached(core, 3));
        Assert.True(AttendanceRules.IsWeeklyLimitReached(core, 4));
        Assert.True(AttendanceRules.IsWeeklyLimitReached(core, 5));
    }

    [Fact]
    public void Scheduled_activities_have_no_weekly_limit()
    {
        foreach (var code in new[] { "Baium", "Antharas", "ChaoticCore", "CastleSiege" })
        {
            var activity = Catalog(code);

            Assert.Null(AttendanceRules.WeeklyLimit(activity));
            Assert.False(AttendanceRules.IsWeeklyLimitReached(activity, 100));
        }
    }
}
