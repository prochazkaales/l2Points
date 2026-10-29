using System.Runtime.CompilerServices;
using Azure.Identity;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: InternalsVisibleTo("L2Points.Infrastructure.Tests")]

namespace L2Points.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Configuration key (env <c>DataProtection__KeyVaultKeyUri</c>) with the Key Vault key URI that encrypts Data Protection keys.</summary>
    public const string KeyVaultKeyUriKey = "DataProtection:KeyVaultKeyUri";

    /// <summary>
    /// Configuration key (env <c>Database__CreateIfMissing</c>, default true). When false, a missing database makes startup fail
    /// instead of being created (set in Azure so the app never creates a default-tier database by accident).
    /// </summary>
    public const string CreateDatabaseIfMissingKey = "Database:CreateIfMissing";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // The factory is the primary registration: interactive components and app services create a short-lived
        // context per operation. It also registers a scoped AppDbContext (used by Identity stores and Data Protection).
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddIdentityServices();

        var dataProtection = services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("L2Points");

        // Keys are stored in the DB; when a Key Vault key is configured they are wrapped by it (unwrapped lazily
        // on first use, so nothing calls Azure during registration). The credential is DefaultAzureCredential:
        // in Azure it picks up the user-assigned managed identity from AZURE_CLIENT_ID.
        var keyVaultKeyUri = configuration[KeyVaultKeyUriKey];
        if (!string.IsNullOrWhiteSpace(keyVaultKeyUri))
        {
            dataProtection.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), new DefaultAzureCredential());
        }

        services.TryAddSingleton(TimeProvider.System);
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<PlayerStatsService>();
        services.AddScoped<OverviewService>();
        services.AddScoped<JewelService>();
        services.AddScoped<CatalogAdminService>();
        services.AddScoped<PlayerAdminService>();

        return services;
    }

    /// <summary>Identity registration shared between production and tests.</summary>
    internal static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<AppSignInManager>()
            .AddDefaultTokenProviders();

        return services;
    }
}
