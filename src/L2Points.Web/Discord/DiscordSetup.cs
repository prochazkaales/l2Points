using L2Points.Infrastructure.Reports;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace L2Points.Web.Discord;

public static class DiscordSetup
{
    public const string InteractionsPath = "/discord/interactions";

    /// <summary>Interactions are a few kB; the endpoint is anonymous, so it does not buffer more than this.</summary>
    private const long MaxBodyBytes = 1024 * 1024;

    /// <summary>
    /// Registers the report bot. The background services run only when Discord and the Anthropic API key are configured;
    /// without them the endpoint answers 404 and nothing calls Discord.
    /// </summary>
    public static IServiceCollection AddDiscordReports(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));
        services.AddSingleton<DiscordJobQueue>();
        services.AddScoped<DiscordInteractionHandler>();

        // Request URLs contain the interaction token, so the client logs nothing. Discord's CDN serves attachments
        // directly; without redirects a download can never leave the trusted hosts.
        services.AddHttpClient<DiscordRestClient>(client =>
            {
                client.BaseAddress = DiscordRestClient.ApiBaseAddress;
                client.Timeout = DiscordRestClient.RequestTimeout;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();

        var discord = configuration.GetSection(DiscordOptions.SectionName).Get<DiscordOptions>() ?? new DiscordOptions();
        var anthropic = configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>() ?? new AnthropicOptions();
        if (DiscordOptions.IsEnabled(discord, anthropic))
        {
            services.AddHostedService<DiscordJobWorker>();
            services.AddHostedService<DiscordCommandRegistrar>();
        }

        return services;
    }

    /// <summary><c>POST /discord/interactions</c>: anonymous (requests are authenticated by their Ed25519 signature).</summary>
    public static WebApplication MapDiscordInteractions(this WebApplication app)
    {
        app.MapPost(InteractionsPath, async (HttpContext context, DiscordInteractionHandler handler) =>
            {
                // Discord needs the bare status code (401 on a bad signature); a re-executed status page would turn it
                // into the app's login redirect.
                if (context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
                    statusCodePages.Enabled = false;

                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
                    bodyLimit.MaxRequestBodySize = MaxBodyBytes;

                using var body = new MemoryStream();
                try
                {
                    await context.Request.Body.CopyToAsync(body, context.RequestAborted);
                }
                catch (BadHttpRequestException e)
                {
                    return Results.StatusCode(e.StatusCode);
                }

                var result = await handler.HandleAsync(
                    body.ToArray(),
                    context.Request.Headers["X-Signature-Ed25519"],
                    context.Request.Headers["X-Signature-Timestamp"],
                    context.RequestAborted);

                return result.Body is null
                    ? Results.StatusCode(result.StatusCode)
                    : Results.Json(result.Body, DiscordJson.Options, statusCode: result.StatusCode);
            })
            .AllowAnonymous()
            .DisableAntiforgery();

        return app;
    }
}
