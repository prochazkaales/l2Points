using L2Points.Infrastructure;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Tests;

internal static class TestServices
{
    /// <summary>
    /// Builds a service provider backed by an in-memory SQLite database with the same Identity setup as production.
    /// When <paramref name="sharedConnection"/> is given, the caller owns it (needed to open a second provider over the same DB).
    /// </summary>
    public static ServiceProvider Create(AdminOptions? admin = null, SqliteConnection? sharedConnection = null, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();

        if (sharedConnection is not null)
        {
            services.AddSingleton(sharedConnection);
        }
        else
        {
            // Factory registration so the container disposes the connection together with the provider.
            services.AddSingleton(_ =>
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                return connection;
            });
        }

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

        services.AddIdentityServices();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<IOptions<AdminOptions>>(Options.Create(admin ?? new AdminOptions()));
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<PlayerStatsService>();
        services.AddScoped<OverviewService>();
        services.AddScoped<JewelService>();
        services.AddScoped<CatalogAdminService>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        return provider;
    }
}
