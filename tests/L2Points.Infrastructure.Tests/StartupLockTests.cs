using L2Points.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class StartupLockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EnsureAcquired_accepts_granted_codes(int code)
    {
        StartupLock.EnsureAcquired(code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    [InlineData(-999)]
    public void EnsureAcquired_throws_when_lock_not_granted(int code)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => StartupLock.EnsureAcquired(code));

        Assert.Contains(code.ToString(), ex.Message);
    }

    [Fact]
    public async Task RunExclusiveAsync_runs_action_once_on_non_sql_server_provider()
    {
        await using var provider = TestServices.Create();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var calls = 0;

        await StartupLock.RunExclusiveAsync(db, () =>
        {
            calls++;
            return Task.CompletedTask;
        }, TimeSpan.FromSeconds(5));

        Assert.Equal(1, calls);
    }
}
