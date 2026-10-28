using L2Points.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(ct);
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync(ct);
    }
}
