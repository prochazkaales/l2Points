using L2Points.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createIfMissing = scope.ServiceProvider.GetService<IConfiguration>()
            ?.GetValue(DependencyInjection.CreateDatabaseIfMissingKey, true) ?? true;

        // During a deployment two revisions can start at the same time and would race on the migration and on
        // seeding (unique Code constraints), so startup work is serialized with a SQL Server app lock.
        await StartupLock.RunExclusiveAsync(db, async () =>
        {
            await db.Database.MigrateAsync(ct);

            await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(ct);
            await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync(ct);
        }, TimeSpan.FromMinutes(3), ct, createIfMissing);
    }
}
