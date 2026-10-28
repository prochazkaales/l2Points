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
