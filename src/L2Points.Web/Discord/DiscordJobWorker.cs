using L2Points.Infrastructure.Reports;
using static L2Points.Web.Discord.DiscordMessages;

namespace L2Points.Web.Discord;

/// <summary>
/// Runs the queued Discord jobs one by one, each in its own DI scope. A failing job is logged and the next one runs;
/// logs name the report, never the interaction token.
/// </summary>
public sealed class DiscordJobWorker(
    DiscordJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await RunAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    // AnalyzeJob and SubmitJob print only their report id.
                    logger.LogError(e, "Discord job {Job} failed.", job);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }
    }

    private async Task RunAsync(DiscordJob job, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IDiscordReportService>();
        var rest = scope.ServiceProvider.GetRequiredService<DiscordRestClient>();

        switch (job)
        {
            case AnalyzeJob analyze:
                await AnalyzeAsync(analyze, reports, rest, ct);
                break;
            case SubmitJob submit:
                await SubmitAsync(submit, reports, rest, ct);
                break;
        }
    }

    private async Task AnalyzeAsync(AnalyzeJob job, IDiscordReportService reports, DiscordRestClient rest, CancellationToken ct)
    {
        DiscordMessage? message;
        try
        {
            var image = await rest.DownloadAttachmentAsync(job.AttachmentUrl, ct);
            var mediaType = job.ContentType.Split(';')[0].Trim();
            message = DiscordViews.ToMessage(await reports.AnalyzeAsync(job.ReportId, image, mediaType, ct));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Reading the screenshot of Discord report {ReportId} failed.", job.ReportId);
            await MarkFailedAsync(job.ReportId, reports);
            message = Notice(Texts.ProcessingFailed);
        }

        // The reporter is told even when the app is stopping, hence no stopping token from here on.
        if (message is not null)
            await rest.EditOriginalAsync(job.InteractionToken, message, CancellationToken.None);
    }

    private async Task MarkFailedAsync(Guid reportId, IDiscordReportService reports)
    {
        try
        {
            await reports.MarkFailedAsync(reportId, CancellationToken.None);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Marking Discord report {ReportId} as failed failed.", reportId);
        }
    }

    private async Task SubmitAsync(SubmitJob job, IDiscordReportService reports, DiscordRestClient rest, CancellationToken ct)
    {
        DiscordReportView view;
        try
        {
            view = await reports.SubmitAsync(job.ReportId, job.DiscordUserId, ct);
        }
        catch (Exception e)
        {
            // The service has already settled the report as failed.
            logger.LogError(e, "Submitting Discord report {ReportId} failed.", job.ReportId);
            await rest.EditOriginalAsync(job.InteractionToken, Notice(Texts.ProcessingFailed), CancellationToken.None);
            return;
        }

        // The attendance is recorded at this point: the panel edit and the public summary go out even during shutdown,
        // and a failed edit does not stop the summary.
        if (DiscordViews.ToMessage(view) is { } message)
        {
            try
            {
                await rest.EditOriginalAsync(job.InteractionToken, message, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Updating the panel of Discord report {ReportId} failed.", job.ReportId);
            }
        }

        if (view is { Outcome: DiscordReportOutcome.Ok, Status: DiscordReportStatus.Completed })
            await rest.SendFollowupAsync(job.InteractionToken, PublicSummary(view), CancellationToken.None);
    }
}
