using L2Points.Domain.Activities;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class AttendanceServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private static readonly DateOnly Tuesday = new(2026, 10, 27);

    private sealed record Fixture(ServiceProvider Provider, AttendanceService Service, string PlayerA, string PlayerB, string Admin) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();

        public AppDbContext NewContext() =>
            Provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    }

    private static async Task<Fixture> CreateAsync(string utcNow = "2026-10-28T10:00:00Z")
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(utcNow));
        var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var a = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        var b = await TestData.CreatePlayerAsync(provider, "playerB", Monday);
        var admin = await TestData.CreatePlayerAsync(provider, "adminX", Monday);
        var service = new AttendanceService(provider.GetRequiredService<IDbContextFactory<AppDbContext>>(), time);
        return new Fixture(provider, service, a.Id, b.Id, admin.Id);
    }

    [Fact]
    public async Task Add_and_remove_own_attendance()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var added = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.Ok, added);
        await using (var db = f.NewContext())
        {
            var row = await db.Attendances.SingleAsync();
            Assert.Equal(f.PlayerA, row.UserId);
            Assert.Equal(f.PlayerA, row.CreatedByUserId);
            Assert.Equal(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc), row.CreatedAtUtc);
        }

        var removed = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, false);

        Assert.Equal(AttendanceChangeResult.Ok, removed);
        await using var verify = f.NewContext();
        Assert.Empty(await verify.Attendances.ToListAsync());
    }

    [Fact]
    public async Task Add_twice_is_idempotent()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var first = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);
        var second = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.Ok, first);
        Assert.Equal(AttendanceChangeResult.Ok, second);
        await using var db = f.NewContext();
        Assert.Equal(1, await db.Attendances.CountAsync());
    }

    [Fact]
    public async Task Remove_missing_is_ok()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var result = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, false);

        Assert.Equal(AttendanceChangeResult.Ok, result);
    }

    [Fact]
    public async Task Player_cannot_edit_other_player()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var result = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerB, queenAnt, Tuesday, EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.Forbidden, result);
        await using var db = f.NewContext();
        Assert.Empty(await db.Attendances.ToListAsync());
    }

    [Fact]
    public async Task Outside_window_rejected()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var future = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, new DateOnly(2026, 10, 29), EventSlot.None, true);
        var beforeTracking = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, new DateOnly(2026, 10, 25), EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.OutsideEditWindow, future);
        Assert.Equal(AttendanceChangeResult.OutsideEditWindow, beforeTracking);
    }

    [Fact]
    public async Task Prague_midnight_boundary()
    {
        // 23:30Z on 10-28 is already 00:30 on 10-29 in Prague (CET, UTC+1).
        await using var f = await CreateAsync("2026-10-28T23:30:00Z");
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var result = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, new DateOnly(2026, 10, 29), EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.Ok, result);
    }

    [Fact]
    public async Task Invalid_slot_rejected()
    {
        await using var f = await CreateAsync();
        var zaken = await TestData.ActivityIdAsync(f.Provider, "ChaoticZaken");
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var wrongDay = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, zaken, Tuesday, EventSlot.Evening, true);
        var wrongSlot = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.Evening, true);

        Assert.Equal(AttendanceChangeResult.InvalidSlot, wrongDay);
        Assert.Equal(AttendanceChangeResult.InvalidSlot, wrongSlot);
        await using var db = f.NewContext();
        Assert.Empty(await db.Attendances.ToListAsync());
    }

    [Fact]
    public async Task Unknown_or_inactive_activity_rejected()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");
        await TestData.DeactivateActivityAsync(f.Provider, "QueenAnt");

        var unknown = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, 9999, Tuesday, EventSlot.None, true);
        var inactive = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.UnknownActivity, unknown);
        Assert.Equal(AttendanceChangeResult.UnknownActivity, inactive);
    }

    [Fact]
    public async Task Orphan_attendance_can_be_removed()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");
        Assert.Equal(AttendanceChangeResult.Ok,
            await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true));
        await TestData.DeactivateActivityAsync(f.Provider, "QueenAnt");

        var removed = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, false);

        Assert.Equal(AttendanceChangeResult.Ok, removed);
        await using var db = f.NewContext();
        Assert.Empty(await db.Attendances.ToListAsync());
    }

    [Fact]
    public async Task Admin_can_edit_other_player_older_date()
    {
        await using var f = await CreateAsync("2026-11-05T10:00:00Z");
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");

        var asPlayer = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);
        var asAdmin = await f.Service.SetAttendanceAsync(f.Admin, true, f.PlayerB, queenAnt, Tuesday, EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.OutsideEditWindow, asPlayer);
        Assert.Equal(AttendanceChangeResult.Ok, asAdmin);
        await using var db = f.NewContext();
        var row = await db.Attendances.SingleAsync();
        Assert.Equal(f.PlayerB, row.UserId);
        Assert.Equal(f.Admin, row.CreatedByUserId);
    }

    [Fact]
    public async Task GetWeekGrid_marks_checked_cells()
    {
        await using var f = await CreateAsync();
        var queenAnt = await TestData.ActivityIdAsync(f.Provider, "QueenAnt");
        await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, queenAnt, Tuesday, EventSlot.None, true);

        var grid = await f.Service.GetWeekGridAsync(f.PlayerA, Week.Containing(Tuesday), false);

        var row = grid.Rows.Single(r => r.Activity.Code == "QueenAnt");
        Assert.True(row.Cells.Single(c => c.Date == Tuesday).IsChecked);
        Assert.All(row.Cells.Where(c => c.Date != Tuesday), c => Assert.False(c.IsChecked));

        var other = await f.Service.GetWeekGridAsync(f.PlayerB, Week.Containing(Tuesday), false);
        Assert.All(other.Rows.Single(r => r.Activity.Code == "QueenAnt").Cells, c => Assert.False(c.IsChecked));
    }

    [Fact]
    public async Task Flexible_boss_cannot_exceed_expected_per_week()
    {
        // Today is Tuesday 3. 11.: the player may edit 28. 10. to 3. 11., which spans two weeks.
        await using var f = await CreateAsync("2026-11-03T10:00:00Z");
        var frintezza = await TestData.ActivityIdAsync(f.Provider, "Frintezza");

        foreach (var day in new[] { 28, 29, 30 })
        {
            var ok = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 10, day), EventSlot.None, true);
            Assert.Equal(AttendanceChangeResult.Ok, ok);
        }

        var fourth = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 10, 31), EventSlot.None, true);
        var byAdmin = await f.Service.SetAttendanceAsync(f.Admin, true, f.PlayerA, frintezza, new DateOnly(2026, 11, 1), EventSlot.None, true);
        var again = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 10, 29), EventSlot.None, true);
        var otherPlayer = await f.Service.SetAttendanceAsync(f.PlayerB, false, f.PlayerB, frintezza, new DateOnly(2026, 10, 31), EventSlot.None, true);
        var nextWeek = await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 11, 2), EventSlot.None, true);

        Assert.Equal(AttendanceChangeResult.WeeklyLimitReached, fourth);
        Assert.Equal(AttendanceChangeResult.WeeklyLimitReached, byAdmin);
        Assert.Equal(AttendanceChangeResult.Ok, again);
        Assert.Equal(AttendanceChangeResult.Ok, otherPlayer);
        Assert.Equal(AttendanceChangeResult.Ok, nextWeek);
        await using (var db = f.NewContext())
        {
            Assert.Equal(4, await db.Attendances.CountAsync(a => a.UserId == f.PlayerA));
        }

        // Removing one frees a day for another.
        Assert.Equal(AttendanceChangeResult.Ok,
            await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 10, 28), EventSlot.None, false));
        Assert.Equal(AttendanceChangeResult.Ok,
            await f.Service.SetAttendanceAsync(f.PlayerA, false, f.PlayerA, frintezza, new DateOnly(2026, 10, 31), EventSlot.None, true));
    }
}
