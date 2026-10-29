using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class PlayerStatsServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private static readonly DateOnly Tuesday = new(2026, 10, 27);

    private static (PlayerStatsService Stats, AttendanceService Attendance) CreateServices(ServiceProvider provider, TimeProvider time)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        return (new PlayerStatsService(factory, time), new AttendanceService(factory, time));
    }

    [Fact]
    public async Task Week_summary_for_current_week()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-28T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var player = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        var (stats, attendance) = CreateServices(provider, time);
        var queenAnt = await TestData.ActivityIdAsync(provider, "QueenAnt");
        var chaoticCore = await TestData.ActivityIdAsync(provider, "ChaoticCore");
        Assert.Equal(AttendanceChangeResult.Ok,
            await attendance.SetAttendanceAsync(player.Id, false, player.Id, queenAnt, Monday, EventSlot.None, true));
        Assert.Equal(AttendanceChangeResult.Ok,
            await attendance.SetAttendanceAsync(player.Id, false, player.Id, chaoticCore, Tuesday, EventSlot.Evening, true));

        var summary = await stats.GetWeekSummaryAsync(player.Id, Week.Containing(Monday));

        Assert.Equal(2, summary.Points);
        Assert.Equal(new CategoryStat(1, 11), summary.Stats[StatCategory.Normal]);
        Assert.Equal(new CategoryStat(1, 12), summary.Stats[StatCategory.Evening]);
        Assert.Equal(new CategoryStat(0, 12), summary.Stats[StatCategory.Morning]);
        Assert.Equal(new CategoryStat(2, 35), summary.Stats.Total);
    }

    [Fact]
    public async Task Week_summary_ignores_attendance_before_tracking_start()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-04T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var player = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        await TestData.AddAttendanceAsync(provider, player.Id, "Baium", new DateOnly(2026, 10, 31));
        await TestData.AddAttendanceAsync(provider, player.Id, "CastleSiege", new DateOnly(2026, 11, 1));
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        Assert.Equal(CatalogChangeResult.Ok, await new CatalogAdminService(factory).SetTrackingStartAsync(new DateOnly(2026, 11, 1)));
        var (stats, _) = CreateServices(provider, time);

        var summary = await stats.GetWeekSummaryAsync(player.Id, Week.Containing(Monday));

        Assert.Equal(1, summary.Points);
    }

    [Fact]
    public async Task Future_week_has_no_stats()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-28T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var player = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        var (stats, _) = CreateServices(provider, time);

        var summary = await stats.GetWeekSummaryAsync(player.Id, new Week(new DateOnly(2026, 11, 2)));

        Assert.Equal(0, summary.Points);
        Assert.Null(summary.Stats.Total.Percent);
    }

    [Fact]
    public async Task Deactivated_player_period_stops()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-28T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var player = await TestData.CreatePlayerAsync(provider, "playerA", Monday, deactivatedAt: Monday);
        var (stats, _) = CreateServices(provider, time);

        var summary = await stats.GetWeekSummaryAsync(player.Id, Week.Containing(Monday));

        Assert.Equal(13, summary.Stats.Total.Expected);
    }

    [Fact]
    public async Task Unknown_user_throws()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-28T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var (stats, _) = CreateServices(provider, time);

        await Assert.ThrowsAsync<InvalidOperationException>(() => stats.GetWeekSummaryAsync("nope", Week.Containing(Monday)));
    }

    private static async Task<(FakeTimeProvider Time, ServiceProvider Provider, string UserId)> ArrangeDetailAsync()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-04T10:00:00Z"));
        var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var a = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        await TestData.AddAttendanceAsync(provider, a.Id, "QueenAnt", Monday);
        await TestData.AddAttendanceAsync(provider, a.Id, "ChaoticCore", Tuesday, EventSlot.Evening);
        await TestData.AddAttendanceAsync(provider, a.Id, "CastleSiege", new DateOnly(2026, 11, 1));
        return (time, provider, a.Id);
    }

    [Fact]
    public async Task PlayerDetail_for_unknown_id_is_null()
    {
        var (time, provider, _) = await ArrangeDetailAsync();
        await using var disposable = provider;
        var (stats, _) = CreateServices(provider, time);

        Assert.Null(await stats.GetPlayerDetailAsync("nope"));
    }

    [Fact]
    public async Task PlayerDetail_contains_overall_week_and_history()
    {
        var (time, provider, userId) = await ArrangeDetailAsync();
        await using var disposable = provider;
        var (stats, _) = CreateServices(provider, time);

        var detail = await stats.GetPlayerDetailAsync(userId);

        Assert.NotNull(detail);
        Assert.Equal(userId, detail.Player.UserId);
        Assert.Equal(3, detail.Overall.Points);
        Assert.Equal(new CategoryStat(0, 35), detail.CurrentWeek.Stats.Total);
        Assert.Equal(2, detail.History.Count);
        Assert.Equal(new DateOnly(2026, 11, 2), detail.History[0].Week.Monday);
    }

    [Fact]
    public async Task PlayerDetail_contains_jewels()
    {
        var (time, provider, userId) = await ArrangeDetailAsync();
        await using var disposable = provider;
        var (stats, _) = CreateServices(provider, time);
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        int zaken;
        await using (var db = factory.CreateDbContext())
            zaken = await db.JewelTypes.Where(t => t.Code == "ZakenEarring").Select(t => t.Id).SingleAsync();

        var before = await stats.GetPlayerDetailAsync(userId);
        Assert.NotNull(before);
        Assert.Empty(before.Jewels);

        Assert.Equal(JewelChangeResult.Ok, await new JewelService(factory).AssignAsync(userId, zaken));

        var detail = await stats.GetPlayerDetailAsync(userId);
        Assert.NotNull(detail);
        var piece = Assert.Single(detail.Jewels);
        Assert.Equal(("ZakenEarring", 1), (piece.Type.Code, piece.Count));
    }

    [Fact]
    public async Task PlayerDetail_for_empty_id_is_null()
    {
        var (time, provider, _) = await ArrangeDetailAsync();
        await using var disposable = provider;
        var (stats, _) = CreateServices(provider, time);

        Assert.Null(await stats.GetPlayerDetailAsync(""));
    }

    [Fact]
    public async Task PlayerDetail_before_tracking_start()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-20T10:00:00Z"));
        await using var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        var player = await TestData.CreatePlayerAsync(provider, "playerA", Monday);
        var (stats, _) = CreateServices(provider, time);

        var detail = await stats.GetPlayerDetailAsync(player.Id);

        Assert.NotNull(detail);
        Assert.Equal(0, detail.Overall.Points);
        Assert.Null(detail.Overall.Stats.Total.Percent);
        Assert.Empty(detail.History);
    }
}
