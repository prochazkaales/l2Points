using L2Points.Domain.Activities;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class CatalogAdminServiceTests
{
    private static async Task<(ServiceProvider Provider, CatalogAdminService Service)> ArrangeAsync()
    {
        var provider = TestServices.Create();
        await TestData.SeedCatalogAsync(provider);
        return (provider, new CatalogAdminService(provider.GetRequiredService<IDbContextFactory<AppDbContext>>()));
    }

    private static async Task<Activity> GetAsync(CatalogAdminService service, string code) =>
        (await service.GetActivitiesAsync()).Single(a => a.Code == code);

    [Fact]
    public async Task Update_activity_writes_values()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;
        var id = await TestData.ActivityIdAsync(provider, "QueenAnt");

        var result = await service.UpdateActivityAsync(id, new ActivitySettings(3, null, null, 5, false));

        Assert.Equal(CatalogChangeResult.Ok, result);
        var activity = await GetAsync(service, "QueenAnt");
        Assert.Equal(3, activity.Points);
        Assert.Equal(5, activity.ExpectedPerWeek);
        Assert.False(activity.IsActive);
    }

    [Fact]
    public async Task Update_activity_sets_and_clears_slot_points()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;
        var id = await TestData.ActivityIdAsync(provider, "ChaoticCore");

        Assert.Equal(CatalogChangeResult.Ok, await service.UpdateActivityAsync(id, new ActivitySettings(1, 2, null, null, true)));
        var first = await GetAsync(service, "ChaoticCore");
        Assert.Equal([(EventSlot.Evening, 2)], first.SlotPoints.Select(s => (s.Slot, s.Points)).ToArray());

        Assert.Equal(CatalogChangeResult.Ok, await service.UpdateActivityAsync(id, new ActivitySettings(1, null, 4, null, true)));
        var second = await GetAsync(service, "ChaoticCore");
        Assert.Equal([(EventSlot.Morning, 4)], second.SlotPoints.Select(s => (s.Slot, s.Points)).ToArray());
    }

    [Fact]
    public async Task Update_activity_invalid_or_unknown()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;
        var id = await TestData.ActivityIdAsync(provider, "QueenAnt");
        var before = await GetAsync(service, "QueenAnt");

        var invalid = await service.UpdateActivityAsync(id, new ActivitySettings(9, null, null, 0, false));
        var unknown = await service.UpdateActivityAsync(9999, new ActivitySettings(1, null, null, 3, true));

        Assert.Equal(CatalogChangeResult.InvalidValue, invalid);
        Assert.Equal(CatalogChangeResult.UnknownActivity, unknown);
        var after = await GetAsync(service, "QueenAnt");
        Assert.Equal(before.Points, after.Points);
        Assert.Equal(before.ExpectedPerWeek, after.ExpectedPerWeek);
        Assert.Equal(before.IsActive, after.IsActive);
    }

    [Fact]
    public async Task Save_spawns_replaces_set()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;
        var id = await TestData.ActivityIdAsync(provider, "ChaoticZaken");

        var result = await service.SaveSpawnsAsync(id,
        [
            new SpawnInput(DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(19, 0)),
            new SpawnInput(DayOfWeek.Saturday, EventSlot.Morning, new TimeOnly(4, 0)),
        ]);

        Assert.Equal(CatalogChangeResult.Ok, result);
        var spawns = (await GetAsync(service, "ChaoticZaken")).Spawns
            .Select(s => (s.DayOfWeek, s.Slot, s.TimeUtc))
            .OrderBy(s => s.DayOfWeek)
            .ToArray();
        Assert.Equal(
            [
                (DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(19, 0)),
                (DayOfWeek.Saturday, EventSlot.Morning, new TimeOnly(4, 0)),
            ],
            spawns);
    }

    [Fact]
    public async Task Save_spawns_rejects_late_time_without_writing()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;
        var id = await TestData.ActivityIdAsync(provider, "ChaoticZaken");

        var result = await service.SaveSpawnsAsync(id, [new SpawnInput(DayOfWeek.Monday, EventSlot.Evening, new TimeOnly(22, 0))]);

        Assert.Equal(CatalogChangeResult.InvalidSpawnTime, result);
        Assert.Equal(6, (await GetAsync(service, "ChaoticZaken")).Spawns.Count);
    }

    [Fact]
    public async Task Anchor_sets_both_bosses()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;

        var result = await service.SetAntharasValakasAnchorAsync(new DateOnly(2026, 11, 8));

        Assert.Equal(CatalogChangeResult.Ok, result);
        Assert.Equal(new DateOnly(2026, 11, 8), (await GetAsync(service, "Antharas")).BiweeklyAnchorDate);
        Assert.Equal(new DateOnly(2026, 11, 15), (await GetAsync(service, "Valakas")).BiweeklyAnchorDate);

        var wrongDay = await service.SetAntharasValakasAnchorAsync(new DateOnly(2026, 11, 7));

        Assert.Equal(CatalogChangeResult.AnchorNotOnWeeklyDay, wrongDay);
        Assert.Equal(new DateOnly(2026, 11, 8), (await GetAsync(service, "Antharas")).BiweeklyAnchorDate);
        Assert.Equal(new DateOnly(2026, 11, 15), (await GetAsync(service, "Valakas")).BiweeklyAnchorDate);
    }

    [Fact]
    public async Task Tracking_start_roundtrip()
    {
        var (provider, service) = await ArrangeAsync();
        await using var _ = provider;

        Assert.Equal(CatalogChangeResult.Ok, await service.SetTrackingStartAsync(new DateOnly(2026, 11, 2)));
        Assert.Equal(new DateOnly(2026, 11, 2), await service.GetTrackingStartAsync());

        Assert.Equal(CatalogChangeResult.InvalidValue, await service.SetTrackingStartAsync(new DateOnly(1999, 1, 4)));
        Assert.Equal(new DateOnly(2026, 11, 2), await service.GetTrackingStartAsync());
    }
}
