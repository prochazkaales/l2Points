using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_resolves_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = @"Server=(localdb)\MSSQLLocalDB;Database=L2PointsDiTest;Trusted_Connection=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IdentitySeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AttendanceService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PlayerStatsService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OverviewService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>());
    }
}
