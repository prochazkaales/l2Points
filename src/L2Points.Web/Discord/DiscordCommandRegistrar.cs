namespace L2Points.Web.Discord;

/// <summary>Registers the <c>/report</c> command once at startup; a failure is only logged and never stops the app.</summary>
public sealed class DiscordCommandRegistrar(
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordCommandRegistrar> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DiscordRestClient>().RegisterCommandsAsync(stoppingToken);
            logger.LogInformation("Discord command /report registered.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown before Discord answered.
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Registering the Discord command /report failed.");
        }
    }
}
