using System.Runtime.CompilerServices;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // The factory is the primary registration: interactive components and app services create a short-lived
        // context per operation. It also registers a scoped AppDbContext (used by Identity stores and Data Protection).
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddIdentityServices();

        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("L2Points");

        services.TryAddSingleton(TimeProvider.System);
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<ReferenceDataSeeder>();

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
