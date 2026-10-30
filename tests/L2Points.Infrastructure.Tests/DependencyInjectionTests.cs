using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Reports;
using L2Points.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_resolves_services()
    {
        using var provider = BuildProvider();

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IdentitySeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AttendanceService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PlayerStatsService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OverviewService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<JewelService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PlayerAdminService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IScreenshotReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDiscordReportService>());
    }

    [Fact]
    public void Data_protection_keys_are_encrypted_with_key_vault_when_configured()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [DependencyInjection.KeyVaultKeyUriKey] = "https://kv-test.vault.azure.net/keys/dataprotection",
        });

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.NotNull(options.XmlEncryptor);
    }

    [Fact]
    public void Data_protection_keys_are_not_encrypted_without_key_vault()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.Null(options.XmlEncryptor);
    }

    private static ServiceProvider BuildProvider(IDictionary<string, string?>? extraSettings = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = @"Server=(localdb)\MSSQLLocalDB;Database=L2PointsDiTest;Trusted_Connection=True",
        };
        if (extraSettings is not null)
        {
            foreach (var (key, value) in extraSettings)
            {
                settings[key] = value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
