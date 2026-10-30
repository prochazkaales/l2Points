using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

/// <summary>Shared arrange helpers for service tests.</summary>
internal static class TestData
{
    public static async Task SeedCatalogAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    }

    public static async Task<AppUser> CreatePlayerAsync(ServiceProvider provider, string name, DateOnly activeFrom, DateOnly? deactivatedAt = null)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = name,
            Nick = name,
            ActiveFrom = activeFrom,
            DeactivatedAt = deactivatedAt,
            IsActive = deactivatedAt is null,
            CreatedAt = DateTime.UtcNow,
        };
        var result = await users.CreateAsync(user, "Passw0rd!x");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>Creates a read-only Reader account (role Reader, no role Player), optionally counted in stats.</summary>
    public static async Task<AppUser> CreateReaderAsync(ServiceProvider provider, string name, DateOnly activeFrom, bool showInStats)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = new AppUser
        {
            UserName = name,
            Nick = name,
            ActiveFrom = activeFrom,
            IsActive = true,
            ShowInStats = showInStats,
            CreatedAt = DateTime.UtcNow,
        };
        var result = await users.CreateAsync(user, "Passw0rd!x");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        if (!await roles.RoleExistsAsync(Roles.Reader))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole(Roles.Reader));
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        var addResult = await users.AddToRoleAsync(user, Roles.Reader);
        Assert.True(addResult.Succeeded, string.Join(", ", addResult.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>Inserts an attendance row directly, bypassing service rules (e.g. for deactivated players).</summary>
    public static async Task AddAttendanceAsync(ServiceProvider provider, string userId, string activityCode, DateOnly date, EventSlot slot = EventSlot.None)
    {
        var activityId = await ActivityIdAsync(provider, activityCode);
        await using var db = provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Attendances.Add(new Attendance
        {
            UserId = userId,
            ActivityId = activityId,
            Date = date,
            Slot = slot,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }

    public static async Task<int> ActivityIdAsync(ServiceProvider provider, string code)
    {
        await using var db = provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        return await db.Activities.Where(a => a.Code == code).Select(a => a.Id).SingleAsync();
    }

    public static async Task DeactivateActivityAsync(ServiceProvider provider, string code)
    {
        await using var db = provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        (await db.Activities.SingleAsync(a => a.Code == code)).IsActive = false;
        await db.SaveChangesAsync();
    }
}
