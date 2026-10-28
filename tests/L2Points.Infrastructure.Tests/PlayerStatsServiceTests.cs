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
}
