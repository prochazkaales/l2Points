using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Channels;
using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Reports;
using L2Points.Web.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace L2Points.Web.Tests.Discord;

public sealed class DiscordJobWorkerTests : IAsyncDisposable
{
    private const string AttachmentUrl = "https://cdn.discordapp.com/attachments/1/2/party.png";
    private const string OriginalUrl = "https://discord.com/api/v10/webhooks/app-1/token-1/messages/@original";
    private const string FollowupUrl = "https://discord.com/api/v10/webhooks/app-1/token-1";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private static readonly DateOnly Day = new(2026, 10, 30);

    private readonly FakeReports _reports = new();
    private readonly StubHandler _handler = new();
    private readonly DiscordJobQueue _queue = new();
    private readonly ServiceProvider _provider;
    private DiscordJobWorker? _worker;

    public DiscordJobWorkerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDiscordReportService>(_reports);
        services.AddSingleton(new DiscordRestClient(
            new HttpClient(_handler) { BaseAddress = DiscordRestClient.ApiBaseAddress },
            Options.Create(new DiscordOptions { ApplicationId = "app-1", GuildId = "guild-1", BotToken = "bot-token" })));
        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.StopAsync(CancellationToken.None);
            _worker.Dispose();
        }

        await _provider.DisposeAsync();
    }

    private async Task StartAsync(TimeSpan? analyzeTimeout = null)
    {
        _worker = new DiscordJobWorker(_queue, _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DiscordJobWorker>.Instance)
        {
            AnalyzeTimeout = analyzeTimeout ?? TimeSpan.FromMinutes(2),
        };
        await _worker.StartAsync(CancellationToken.None);
    }

    private static AnalyzeJob Analyze(Guid id) => new(id, "token-1", AttachmentUrl, "image/png");

    private static DiscordReportView PanelView(Guid id) =>
        new(id, DiscordReportOutcome.Ok, DiscordReportStatus.Pending, "Alice",
            new DiscordReportState
            {
                PanelFound = true,
                ReadNicks = ["Bob"],
                Players = [new ReportPlayer("u-1", "Bob", "Bob", NickMatchKind.Exact, true)],
                Date = Day,
            },
            [Day],
            []);

    private static DiscordReportView CompletedView(Guid id) =>
        new(id, DiscordReportOutcome.Ok, DiscordReportStatus.Completed, "Alice",
            new DiscordReportState
            {
                PanelFound = true,
                ReadNicks = ["Bob"],
                Players = [new ReportPlayer("u-1", "Bob", "Bob", NickMatchKind.Exact, true)],
                Date = Day,
                Result = new ReportResult([new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", 1, 0, [])]),
            },
            [Day],
            []);

    private static string Content(Recorded request)
    {
        using var body = JsonDocument.Parse(request.Body!);
        return body.RootElement.GetProperty("content").GetString()!;
    }

    private static int ComponentCount(Recorded request)
    {
        using var body = JsonDocument.Parse(request.Body!);
        return body.RootElement.GetProperty("components").GetArrayLength();
    }

    [Fact]
    public async Task Analyze_success_edits_the_original_with_the_panel()
    {
        var id = Guid.NewGuid();
        _reports.Analyze = (_, _) => Task.FromResult(PanelView(id));
        await StartAsync();

        _queue.TryEnqueue(Analyze(id));

        var download = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Get, download.Method);
        Assert.Equal(AttachmentUrl, download.Url);
        var edit = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Patch, edit.Method);
        Assert.Equal(OriginalUrl, edit.Url);
        Assert.Equal(DiscordMessages.ReportPanel(PanelView(id)).Content, Content(edit));
        Assert.NotEqual(0, ComponentCount(edit));
        Assert.Equal(new[] { id }, _reports.Analyzed);
        Assert.Empty(_reports.Failed);
    }

    [Fact]
    public async Task Analyze_failure_marks_the_report_failed_and_the_next_job_runs()
    {
        var failing = Guid.NewGuid();
        var next = Guid.NewGuid();
        _reports.Analyze = (id, _) => id == failing
            ? throw new ScreenshotReadException("The model returned no text.")
            : Task.FromResult(PanelView(id));
        await StartAsync();

        _queue.TryEnqueue(Analyze(failing));
        _queue.TryEnqueue(Analyze(next));

        await _handler.NextAsync();
        var failedEdit = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Patch, failedEdit.Method);
        Assert.Equal(DiscordMessages.Texts.ProcessingFailed, Content(failedEdit));
        Assert.Equal(0, ComponentCount(failedEdit));
        Assert.Equal(new[] { failing }, _reports.Failed);

        await _handler.NextAsync();
        var panelEdit = await _handler.NextAsync();
        Assert.Equal(DiscordMessages.ReportPanel(PanelView(next)).Content, Content(panelEdit));
        Assert.Equal(new[] { failing, next }, _reports.Analyzed);
    }

    [Fact]
    public async Task Analyze_that_never_returns_is_cut_off_by_the_job_timeout()
    {
        var stuck = Guid.NewGuid();
        var next = Guid.NewGuid();
        _reports.Analyze = async (id, ct) =>
        {
            if (id == stuck)
                await Task.Delay(Timeout.Infinite, ct);
            return PanelView(id);
        };
        await StartAsync(TimeSpan.FromMilliseconds(200));

        _queue.TryEnqueue(Analyze(stuck));
        _queue.TryEnqueue(Analyze(next));

        await _handler.NextAsync();
        var failedEdit = await _handler.NextAsync();
        Assert.Equal(DiscordMessages.Texts.ProcessingFailed, Content(failedEdit));
        Assert.Equal(new[] { stuck }, _reports.Failed);

        await _handler.NextAsync();
        var panelEdit = await _handler.NextAsync();
        Assert.Equal(DiscordMessages.ReportPanel(PanelView(next)).Content, Content(panelEdit));
    }

    [Fact]
    public async Task Submit_success_edits_with_the_result_and_posts_the_summary()
    {
        var id = Guid.NewGuid();
        _reports.Submit = (_, _) => Task.FromResult(CompletedView(id));
        await StartAsync();

        _queue.TryEnqueue(new SubmitJob(id, "1001", "token-1"));

        var edit = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Patch, edit.Method);
        Assert.Equal(OriginalUrl, edit.Url);
        Assert.Equal(DiscordMessages.Result(CompletedView(id)).Content, Content(edit));
        var summary = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Post, summary.Method);
        Assert.Equal(FollowupUrl, summary.Url);
        Assert.Equal(DiscordMessages.PublicSummary(CompletedView(id)).Content, Content(summary));
        Assert.Equal(new[] { (id, "1001") }, _reports.Submitted);
    }

    [Fact]
    public async Task Submit_failure_edits_with_submit_failed_and_posts_no_summary()
    {
        var id = Guid.NewGuid();
        _reports.Submit = (_, _) => throw new InvalidOperationException("Database is down.");
        await StartAsync();

        _queue.TryEnqueue(new SubmitJob(id, "1001", "token-1"));

        var edit = await _handler.NextAsync();
        Assert.Equal(HttpMethod.Patch, edit.Method);
        Assert.Equal(DiscordMessages.Texts.SubmitFailed, Content(edit));
        await _worker!.StopAsync(CancellationToken.None);
        Assert.False(_handler.HasMore);
    }

    [Fact]
    public async Task Start_fails_stale_reports_and_a_failure_there_does_not_stop_the_jobs()
    {
        var id = Guid.NewGuid();
        _reports.FailStale = _ => throw new InvalidOperationException("Database is down.");
        _reports.Analyze = (_, _) => Task.FromResult(PanelView(id));
        await StartAsync();

        _queue.TryEnqueue(Analyze(id));

        await _handler.NextAsync();
        var edit = await _handler.NextAsync();
        Assert.Equal(DiscordMessages.ReportPanel(PanelView(id)).Content, Content(edit));
        Assert.Equal(1, _reports.FailStaleCalls);
    }

    private sealed record Recorded(HttpMethod Method, string Url, string? Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Channel<Recorded> _requests = Channel.CreateUnbounded<Recorded>();

        public bool HasMore => _requests.Reader.TryPeek(out _);

        public async Task<Recorded> NextAsync()
        {
            using var timeout = new CancellationTokenSource(WaitLimit);
            return await _requests.Reader.ReadAsync(timeout.Token);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            _requests.Writer.TryWrite(new Recorded(request.Method, request.RequestUri!.AbsoluteUri, body));

            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);

            var image = new ByteArrayContent(new byte[16]);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = image };
        }
    }

    private sealed class FakeReports : IDiscordReportService
    {
        public Func<Guid, CancellationToken, Task<DiscordReportView>> Analyze { get; set; } =
            (_, _) => throw new InvalidOperationException("No analysis configured.");

        public Func<Guid, CancellationToken, Task<DiscordReportView>> Submit { get; set; } =
            (_, _) => throw new InvalidOperationException("No submit configured.");

        public Func<CancellationToken, Task<int>> FailStale { get; set; } = _ => Task.FromResult(0);

        public ConcurrentQueue<Guid> Analyzed { get; } = new();

        public ConcurrentQueue<Guid> Failed { get; } = new();

        public ConcurrentQueue<(Guid, string)> Submitted { get; } = new();

        private int _failStaleCalls;

        public int FailStaleCalls => Volatile.Read(ref _failStaleCalls);

        public Task<DiscordReportView> AnalyzeAsync(Guid reportId, byte[] image, string mediaType, CancellationToken ct = default)
        {
            Analyzed.Enqueue(reportId);
            return Analyze(reportId, ct);
        }

        public Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct = default)
        {
            Failed.Enqueue(reportId);
            return Task.FromResult(new DiscordReportView(
                reportId, DiscordReportOutcome.Ok, DiscordReportStatus.Failed, "Alice", new DiscordReportState(), [], []));
        }

        public Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct = default)
        {
            Submitted.Enqueue((reportId, discordUserId));
            return Submit(reportId, ct);
        }

        public Task<int> FailStaleAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _failStaleCalls);
            return FailStale(ct);
        }

        public Task<Guid> CreateAsync(DiscordReporter reporter, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DiscordReportView> SelectDateAsync(Guid reportId, string discordUserId, DateOnly date, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DiscordReportView> SelectEventsAsync(
            Guid reportId, string discordUserId, IReadOnlyList<AttendanceEntry> events, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DiscordReportView> ConfirmSimilarAsync(
            Guid reportId, string discordUserId, IReadOnlyList<string> userIds, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DiscordReportView> CancelAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
