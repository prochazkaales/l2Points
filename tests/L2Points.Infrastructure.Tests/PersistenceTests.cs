using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Domain.Jewels;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class PersistenceTests
{
    private static AppDbContext NewContext(ServiceProvider provider) =>
        provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    private static async Task SeedCatalogAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    }

    private static async Task<string> CreateUserAsync(ServiceProvider provider, string name)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = name,
            Nick = name,
            ActiveFrom = new DateOnly(2026, 10, 26),
            CreatedAt = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc),
        };
        Assert.True((await userManager.CreateAsync(user, "Password1")).Succeeded);
        return user.Id;
    }

    private static Attendance NewAttendance(string userId, int activityId, EventSlot slot) => new()
    {
        UserId = userId,
        ActivityId = activityId,
        Date = new DateOnly(2026, 10, 27),
        Slot = slot,
        CreatedAtUtc = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc),
        CreatedByUserId = userId,
    };

    private static async Task<int> ActivityIdAsync(AppDbContext db, string code) =>
        (await db.Activities.SingleAsync(a => a.Code == code)).Id;

    [Fact]
    public async Task Attendance_unique_per_user_activity_date_slot()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);
        var userId = await CreateUserAsync(provider, "player");

        await using (var db = NewContext(provider))
        {
            var activityId = await ActivityIdAsync(db, "ChaoticCore");
            db.Attendances.Add(NewAttendance(userId, activityId, EventSlot.Evening));
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext(provider))
        {
            var activityId = await ActivityIdAsync(db, "ChaoticCore");
            db.Attendances.Add(NewAttendance(userId, activityId, EventSlot.Evening));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext(provider))
        {
            var activityId = await ActivityIdAsync(db, "ChaoticCore");
            db.Attendances.Add(NewAttendance(userId, activityId, EventSlot.Morning));
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.Attendances.CountAsync());
        }
    }

    [Fact]
    public async Task Attendance_deleted_with_user()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);
        var userId = await CreateUserAsync(provider, "player");

        await using (var db = NewContext(provider))
        {
            db.Attendances.Add(NewAttendance(userId, await ActivityIdAsync(db, "QueenAnt"), EventSlot.None));
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(userId);
            Assert.True((await userManager.DeleteAsync(user!)).Succeeded);
        }

        await using var verify = NewContext(provider);
        Assert.Equal(0, await verify.Attendances.CountAsync());
    }

    [Fact]
    public async Task DateTime_reads_back_as_utc()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);
        var userId = await CreateUserAsync(provider, "player");

        await using (var db = NewContext(provider))
        {
            db.Attendances.Add(NewAttendance(userId, await ActivityIdAsync(db, "QueenAnt"), EventSlot.None));
            await db.SaveChangesAsync();
        }

        await using var verify = NewContext(provider);
        var attendance = await verify.Attendances.SingleAsync();
        var user = await verify.Users.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, attendance.CreatedAtUtc.Kind);
        Assert.Equal(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc), attendance.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
    }

    [Fact]
    public async Task LoadActivitiesAsync_returns_ordered_catalog_with_spawns()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);

        await using (var db = NewContext(provider))
        {
            (await db.Activities.SingleAsync(a => a.Code == "Orfen")).IsActive = false;
            await db.SaveChangesAsync();
        }

        await using var read = NewContext(provider);
        var activities = await read.LoadActivitiesAsync();

        Assert.Equal(14, activities.Count);
        Assert.Equal("QueenAnt", activities[0].Code);
        Assert.Equal(activities.Select(a => a.SortOrder).Order(), activities.Select(a => a.SortOrder));
        Assert.Contains(activities, a => a.Code == "Orfen" && !a.IsActive);
        Assert.Equal(6, activities.Single(a => a.Code == "ChaoticZaken").Spawns.Count);
    }

    [Fact]
    public async Task GetTrackingStartAsync_returns_seeded_date()
    {
        await using var provider = TestServices.Create();
        await using (var empty = NewContext(provider))
        {
            Assert.Equal(new DateOnly(2026, 10, 26), await empty.GetTrackingStartAsync());
        }

        await SeedCatalogAsync(provider);
        await using (var db = NewContext(provider))
        {
            Assert.Equal(new DateOnly(2026, 10, 26), await db.GetTrackingStartAsync());

            (await db.AppSettings.SingleAsync()).Value = "2027-01-04";
            await db.SaveChangesAsync();
        }

        await using var verify = NewContext(provider);
        Assert.Equal(new DateOnly(2027, 1, 4), await verify.GetTrackingStartAsync());
    }

    private static async Task AddJewelsAsync(ServiceProvider provider, string userId, params string[] typeCodes)
    {
        await using var db = NewContext(provider);
        foreach (var code in typeCodes)
        {
            var typeId = await db.JewelTypes.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
            db.UserJewels.Add(new UserJewel { UserId = userId, JewelTypeId = typeId });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UserJewels_deleted_with_user()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);
        var userId = await CreateUserAsync(provider, "player");
        await AddJewelsAsync(provider, userId, "QueenAntRing", "BaiumRing");

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(userId);
            Assert.True((await userManager.DeleteAsync(user!)).Succeeded);
        }

        await using var verify = NewContext(provider);
        Assert.Equal(0, await verify.UserJewels.CountAsync());
        Assert.Equal(8, await verify.JewelTypes.CountAsync());
    }

    [Fact]
    public async Task LoadJewelsByPlayerAsync_groups_holdings()
    {
        await using var provider = TestServices.Create();
        await SeedCatalogAsync(provider);
        var userId = await CreateUserAsync(provider, "player");
        await AddJewelsAsync(provider, userId, "QueenAntRing", "ValakasNecklace", "QueenAntRing");

        await using var db = NewContext(provider);
        var jewels = await db.LoadJewelsByPlayerAsync();

        Assert.Equal(
            [("QueenAntRing", 2), ("ValakasNecklace", 1)],
            jewels.Of(userId).Select(j => (j.Type.Code, j.Count)));
        Assert.Empty(jewels.Of("unknown"));
    }
}
