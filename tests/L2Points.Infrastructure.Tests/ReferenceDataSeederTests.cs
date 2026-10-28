using L2Points.Domain.Settings;
using L2Points.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class ReferenceDataSeederTests
{
    private static async Task SeedAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    }

    private static AppDbContext NewContext(ServiceProvider provider) =>
        provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    [Fact]
    public async Task Seed_inserts_catalog_and_setting()
    {
        await using var provider = TestServices.Create();

        await SeedAsync(provider);

        await using var db = NewContext(provider);
        Assert.Equal(14, await db.Activities.CountAsync());
        Assert.Equal(40, await db.ActivitySpawns.CountAsync());
        Assert.Equal(8, await db.JewelTypes.CountAsync());
        var setting = await db.AppSettings.SingleAsync();
        Assert.Equal(AppSettingKeys.TrackingStartDate, setting.Key);
        Assert.Equal("2026-10-26", setting.Value);
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await using var provider = TestServices.Create();

        await SeedAsync(provider);
        await SeedAsync(provider);

        await using var db = NewContext(provider);
        Assert.Equal(14, await db.Activities.CountAsync());
        Assert.Equal(40, await db.ActivitySpawns.CountAsync());
        Assert.Equal(8, await db.JewelTypes.CountAsync());
        Assert.Equal(1, await db.AppSettings.CountAsync());
    }

    [Fact]
    public async Task Seed_does_not_overwrite_admin_changes()
    {
        await using var provider = TestServices.Create();
        await SeedAsync(provider);

        await using (var db = NewContext(provider))
        {
            var queenAnt = await db.Activities.SingleAsync(a => a.Code == "QueenAnt");
            queenAnt.Points = 9;
            queenAnt.ExpectedPerWeek = 5;
            (await db.AppSettings.SingleAsync()).Value = "2027-01-04";
            await db.SaveChangesAsync();
        }

        await SeedAsync(provider);

        await using var verify = NewContext(provider);
        var reloaded = await verify.Activities.SingleAsync(a => a.Code == "QueenAnt");
        Assert.Equal(9, reloaded.Points);
        Assert.Equal(5, reloaded.ExpectedPerWeek);
        Assert.Equal("2027-01-04", (await verify.AppSettings.SingleAsync()).Value);
    }

    [Fact]
    public async Task Seed_adds_missing_activity_only()
    {
        await using var provider = TestServices.Create();
        await SeedAsync(provider);

        await using (var db = NewContext(provider))
        {
            (await db.Activities.SingleAsync(a => a.Code == "Core")).Points = 7;
            db.Activities.Remove(await db.Activities.SingleAsync(a => a.Code == "CastleSiege"));
            await db.SaveChangesAsync();
        }

        await SeedAsync(provider);

        await using var verify = NewContext(provider);
        Assert.Equal(14, await verify.Activities.CountAsync());
        Assert.Equal(1, await verify.Activities.CountAsync(a => a.Code == "CastleSiege"));
        Assert.Equal(7, (await verify.Activities.SingleAsync(a => a.Code == "Core")).Points);
        Assert.Equal(40, await verify.ActivitySpawns.CountAsync());
    }

    [Fact]
    public async Task Seed_adds_missing_jewel_types_without_overwriting()
    {
        await using var provider = TestServices.Create();
        await SeedAsync(provider);

        await using (var db = NewContext(provider))
        {
            (await db.JewelTypes.SingleAsync(t => t.Code == "BaiumRing")).Name = "Baium ring";
            db.JewelTypes.Remove(await db.JewelTypes.SingleAsync(t => t.Code == "ValakasNecklace"));
            await db.SaveChangesAsync();
        }

        await SeedAsync(provider);

        await using var verify = NewContext(provider);
        Assert.Equal(8, await verify.JewelTypes.CountAsync());
        Assert.Equal(1, await verify.JewelTypes.CountAsync(t => t.Code == "ValakasNecklace"));
        Assert.Equal("Baium ring", (await verify.JewelTypes.SingleAsync(t => t.Code == "BaiumRing")).Name);
    }
}
