using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class OverviewServiceTests
{
    private static readonly DateOnly Mon = new(2026, 10, 26);
    private static readonly DateOnly Tue = new(2026, 10, 27);
    private static readonly DateOnly Sun = new(2026, 11, 1);
    private static readonly DateOnly NextMon = new(2026, 11, 2);

    private sealed record Arranged(ServiceProvider Provider, OverviewService Service, JewelService Jewels, string A, string B, string D) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private static async Task<Arranged> ArrangeAsync()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-04T10:00:00Z"));
        var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var a = await TestData.CreatePlayerAsync(provider, "playerA", Mon);
        var b = await TestData.CreatePlayerAsync(provider, "playerB", NextMon);
        var d = await TestData.CreatePlayerAsync(provider, "playerD", Mon, deactivatedAt: Tue);
        await TestData.AddAttendanceAsync(provider, a.Id, "QueenAnt", Mon);
        await TestData.AddAttendanceAsync(provider, a.Id, "ChaoticCore", Tue, EventSlot.Evening);
        await TestData.AddAttendanceAsync(provider, a.Id, "CastleSiege", Sun);
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var jewels = new JewelService(factory);
        await using (var db = factory.CreateDbContext())
        {
            var antharas = await db.JewelTypes.Where(t => t.Code == "AntharasEarring").Select(t => t.Id).SingleAsync();
            Assert.Equal(JewelChangeResult.Ok, await jewels.AssignAsync(a.Id, antharas));
        }
        return new Arranged(provider, new OverviewService(factory, time), jewels, a.Id, b.Id, d.Id);
    }

    [Fact]
    public async Task Leaderboard_hides_inactive_by_default()
    {
        await using var x = await ArrangeAsync();

        var rows = (await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: false)).Rows;

        Assert.Equal([x.A, x.B], rows.Select(r => r.Player.UserId));
        Assert.Equal(3, rows[0].Overall.Points);
        Assert.Equal(new CategoryStat(3, 99), rows[0].Overall.Stats.Total);
    }

    [Fact]
    public async Task Leaderboard_includes_inactive_on_request()
    {
        await using var x = await ArrangeAsync();

        var rows = (await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: true)).Rows;

        Assert.Equal(3, rows.Count);
        Assert.Equal(25, rows.Single(r => r.Player.UserId == x.D).Overall.Stats.Total.Expected);
    }

    [Fact]
    public async Task Leaderboard_on_empty_database()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-04T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var service = new OverviewService(provider.GetRequiredService<IDbContextFactory<AppDbContext>>(), time);
        var week = Week.Containing(NextMon);

        Assert.Empty((await service.GetLeaderboardAsync(week, includeInactive: true)).Rows);

        await TestData.CreatePlayerAsync(provider, "lonely", Mon);
        var row = Assert.Single((await service.GetLeaderboardAsync(week, includeInactive: false)).Rows);
        Assert.Equal(0, row.Overall.Points);
        Assert.Equal(0, row.Overall.Stats.Total.Percent);
    }

    [Fact]
    public async Task Leaderboard_returns_jewels_of_players()
    {
        await using var x = await ArrangeAsync();

        var result = await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: false);

        var piece = Assert.Single(result.Jewels.Of(x.A));
        Assert.Equal(("AntharasEarring", 1), (piece.Type.Code, piece.Count));
        Assert.Empty(result.Jewels.Of(x.B));
    }

    [Fact]
    public async Task Leaderboard_returns_points_of_previous_week()
    {
        await using var x = await ArrangeAsync();

        var result = await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: false);

        Assert.Equal(3, result.PreviousWeekPoints);
    }

    [Fact]
    public async Task Leaderboard_has_no_previous_week_before_tracking_start()
    {
        await using var x = await ArrangeAsync();

        var result = await x.Service.GetLeaderboardAsync(Week.Containing(Mon), includeInactive: false);

        Assert.Null(result.PreviousWeekPoints);
    }

    [Fact]
    public async Task Leaderboard_week_before_tracking_start_is_empty()
    {
        await using var x = await ArrangeAsync();

        var rows = (await x.Service.GetLeaderboardAsync(new Week(new DateOnly(2026, 10, 19)), includeInactive: false)).Rows;

        Assert.Equal(0, rows[0].Week.Points);
        Assert.Null(rows[0].Week.Stats.Total.Percent);
        Assert.Equal(3, rows[0].Overall.Points);
    }

    [Fact]
    public async Task Leaderboard_future_week_is_empty()
    {
        await using var x = await ArrangeAsync();

        var rows = (await x.Service.GetLeaderboardAsync(new Week(new DateOnly(2026, 11, 16)), includeInactive: false)).Rows;

        Assert.Equal(0, rows[0].Week.Points);
        Assert.Null(rows[0].Week.Stats.Total.Percent);
        Assert.Equal(3, rows[0].Overall.Points);
    }

    [Fact]
    public async Task WeekOverview_returns_jewels()
    {
        await using var x = await ArrangeAsync();

        var overview = await x.Service.GetWeekOverviewAsync(Week.Containing(Mon));

        Assert.Single(overview.Jewels.Of(x.A));
        Assert.Empty(overview.Jewels.Of(x.B));
    }

    [Fact]
    public async Task WeekOverview_lists_players_and_events()
    {
        await using var x = await ArrangeAsync();

        var overview = await x.Service.GetWeekOverviewAsync(Week.Containing(Mon));

        Assert.Equal(Week.Containing(Mon), overview.Week);
        Assert.Equal(3, overview.Players[0].Week.Points);
        Assert.Equal([x.A, x.B], overview.Players.Select(r => r.Player.UserId));
        Assert.Equal(
            [("QueenAnt", Mon, EventSlot.None), ("ChaoticCore", Tue, EventSlot.Evening), ("CastleSiege", Sun, EventSlot.None)],
            overview.Events.Select(e => (e.Activity.Code, e.Date, e.Slot)));
        Assert.All(overview.Events, e => Assert.Equal([x.A], e.Attendees.Select(p => p.UserId)));
    }

    [Fact]
    public async Task WeekOverview_returns_points_of_previous_week()
    {
        await using var x = await ArrangeAsync();
        await TestData.AddAttendanceAsync(x.Provider, x.D, "QueenAnt", Mon);

        var first = await x.Service.GetWeekOverviewAsync(Week.Containing(Mon));
        var second = await x.Service.GetWeekOverviewAsync(Week.Containing(NextMon));

        Assert.Null(first.PreviousWeekPoints);
        // The inactive player counts in the previous week because they attended it.
        Assert.Equal(4, second.PreviousWeekPoints);
    }

    [Fact]
    public async Task WeekOverview_includes_inactive_with_attendance()
    {
        await using var x = await ArrangeAsync();
        await TestData.AddAttendanceAsync(x.Provider, x.D, "QueenAnt", Mon);

        var first = await x.Service.GetWeekOverviewAsync(Week.Containing(Mon));
        var second = await x.Service.GetWeekOverviewAsync(Week.Containing(NextMon));

        Assert.Contains(first.Players, r => r.Player.UserId == x.D);
        Assert.DoesNotContain(second.Players, r => r.Player.UserId == x.D);
    }

    [Fact]
    public async Task Leaderboard_OmitsHiddenReader()
    {
        await using var x = await ArrangeAsync();
        var reader = await TestData.CreateReaderAsync(x.Provider, "readerHidden", Mon, showInStats: false);
        await TestData.AddAttendanceAsync(x.Provider, reader.Id, "QueenAnt", Mon);

        var rows = (await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: true)).Rows;

        Assert.DoesNotContain(rows, r => r.Player.UserId == reader.Id);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task Leaderboard_IncludesShownReader()
    {
        await using var x = await ArrangeAsync();
        var reader = await TestData.CreateReaderAsync(x.Provider, "readerShown", Mon, showInStats: true);
        await TestData.AddAttendanceAsync(x.Provider, reader.Id, "QueenAnt", Mon);

        var rows = (await x.Service.GetLeaderboardAsync(Week.Containing(NextMon), includeInactive: false)).Rows;

        Assert.Equal(1, rows.Single(r => r.Player.UserId == reader.Id).Overall.Points);
    }

    [Fact]
    public async Task WeekOverview_EventsOmitHiddenReader()
    {
        await using var x = await ArrangeAsync();
        var reader = await TestData.CreateReaderAsync(x.Provider, "readerHidden", Mon, showInStats: false);
        await TestData.AddAttendanceAsync(x.Provider, reader.Id, "QueenAnt", Mon);

        var first = await x.Service.GetWeekOverviewAsync(Week.Containing(Mon));
        var second = await x.Service.GetWeekOverviewAsync(Week.Containing(NextMon));

        Assert.DoesNotContain(first.Players, r => r.Player.UserId == reader.Id);
        Assert.All(first.Events, e => Assert.Equal([x.A], e.Attendees.Select(p => p.UserId)));
        // Player A earned 3 points in the previous week; the hidden reader's point is not counted.
        Assert.Equal(3, second.PreviousWeekPoints);
    }
}
