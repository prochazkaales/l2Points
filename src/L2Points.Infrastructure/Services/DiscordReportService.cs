using System.Text.Json;
using System.Text.Json.Serialization;
using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Services;

/// <summary>
/// Report created from a Discord screenshot: analysis, the reporter's choices and their lifecycle.
/// Every user call checks, in this order: unknown id, owner, finished, expired.
/// </summary>
public sealed class DiscordReportService(IDbContextFactory<AppDbContext> dbFactory, IScreenshotReader reader, TimeProvider time)
    : IDiscordReportService
{
    /// <summary>How long a report accepts changes after it was created.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The report under change plus the data every operation needs; <see cref="State"/> is saved after the operation.</summary>
    private sealed class Work(
        AppDbContext db,
        DiscordReport report,
        DiscordReportState state,
        IReadOnlyList<Activity> activities,
        DateOnly today,
        DateOnly trackingStart)
    {
        public AppDbContext Db { get; } = db;

        public DiscordReport Report { get; } = report;

        public DiscordReportState State { get; set; } = state;

        public IReadOnlyList<Activity> Activities { get; } = activities;

        public DateOnly Today { get; } = today;

        public DateOnly TrackingStart { get; } = trackingStart;

        public IReadOnlyList<DateOnly> DateOptions => ReportEventPlanner.DateOptions(Today, TrackingStart);
    }

    public async Task<Guid> CreateAsync(DiscordReporter reporter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = new DiscordReport
        {
            Id = Guid.NewGuid(),
            DiscordUserId = reporter.DiscordUserId,
            DiscordUserName = reporter.DiscordUserName,
            GuildId = reporter.GuildId,
            ChannelId = reporter.ChannelId,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            Status = DiscordReportStatus.Processing,
            StateJson = "{}",
        };
        db.DiscordReports.Add(report);
        await db.SaveChangesAsync(ct);
        return report.Id;
    }

    public async Task<DiscordReportView> AnalyzeAsync(Guid reportId, byte[] image, string mediaType, CancellationToken ct = default)
    {
        // The reader call can take long, so no context is held while it runs.
        List<NickCandidate> candidates;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var report = await db.DiscordReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null)
                return MissingView(reportId, DiscordReportOutcome.NotFound);
            if (report.Status != DiscordReportStatus.Processing)
                return await ViewOfAsync(db, report, StatusOutcome(report.Status), ct);

            candidates = await db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.ShowInStats)
                .OrderBy(u => u.Nick)
                .Select(u => new NickCandidate(u.Id, u.Nick))
                .ToListAsync(ct);
        }

        var read = await reader.ReadPartyMembersAsync(image, mediaType, candidates.Select(c => c.Nick).ToList(), ct);

        await using var saveDb = await dbFactory.CreateDbContextAsync(ct);
        var current = await saveDb.DiscordReports.SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (current is null)
            return MissingView(reportId, DiscordReportOutcome.NotFound);
        if (current.Status != DiscordReportStatus.Processing)
            return await ViewOfAsync(saveDb, current, StatusOutcome(current.Status), ct);

        if (!read.PanelFound || read.Nicks.Count == 0)
        {
            current.Status = DiscordReportStatus.Failed;
            current.CompletedAtUtc = Now();
            current.StateJson = Serialize(new DiscordReportState { PanelFound = read.PanelFound, ReadNicks = read.Nicks });
            await saveDb.SaveChangesAsync(ct);
            return await ViewOfAsync(saveDb, current, DiscordReportOutcome.Ok, ct);
        }

        var match = NickMatcher.Match(read.Nicks, candidates);
        var players = match.Matches
            .Select(m => new ReportPlayer(m.UserId, m.Nick, m.ReadNick, m.Kind, m.Kind == NickMatchKind.Exact))
            .ToList();

        var activities = await saveDb.LoadActivitiesAsync(ct);
        var today = AppTime.TodayInPrague(time);
        var recorded = await LoadRecordedAsync(saveDb, players, ReportTime(current), ct);
        var preselected = ReportEventPlanner.Preselect(activities, ReportTime(current), recorded);
        var date = ReportEventPlanner.DefaultDate(preselected, today);

        current.Status = DiscordReportStatus.Pending;
        current.StateJson = Serialize(new DiscordReportState
        {
            PanelFound = true,
            ReadNicks = read.Nicks,
            Players = players,
            UnknownNicks = match.Unknown,
            Date = date,
            SelectedEvents = preselected.Where(e => e.Date == date).ToList(),
        });
        await saveDb.SaveChangesAsync(ct);
        return await ViewOfAsync(saveDb, current, DiscordReportOutcome.Ok, ct);
    }

    public async Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.DiscordReports.SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return MissingView(reportId, DiscordReportOutcome.NotFound);

        if (report.Status == DiscordReportStatus.Processing)
        {
            report.Status = DiscordReportStatus.Failed;
            report.CompletedAtUtc = Now();
            await db.SaveChangesAsync(ct);
        }

        return await ViewOfAsync(db, report, StatusOutcome(report.Status), ct);
    }

    public Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, _ => true, _ => Task.CompletedTask, ct);

    public Task<DiscordReportView> SelectDateAsync(Guid reportId, string discordUserId, DateOnly date, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, IsPending, async work =>
        {
            if (!work.DateOptions.Contains(date))
                return;

            var recorded = await LoadRecordedAsync(work.Db, work.State.Players, ReportTime(work.Report), ct);
            var preselected = ReportEventPlanner.Preselect(work.Activities, ReportTime(work.Report), recorded);
            work.State = work.State with
            {
                Date = date,
                SelectedEvents = preselected.Where(e => e.Date == date).ToList(),
            };
        }, ct);

    public Task<DiscordReportView> SelectEventsAsync(
        Guid reportId, string discordUserId, IReadOnlyList<AttendanceEntry> events, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, IsPending, work =>
        {
            var offered = work.State.Date is { } date
                ? ReportEventPlanner.EventOptions(work.Activities, date).Select(o => o.Event).ToHashSet()
                : [];
            work.State = work.State with { SelectedEvents = events.Where(offered.Contains).Distinct().ToList() };
            return Task.CompletedTask;
        }, ct);

    public Task<DiscordReportView> ConfirmSimilarAsync(
        Guid reportId, string discordUserId, IReadOnlyList<string> userIds, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, IsPending, work =>
        {
            var confirmed = userIds.ToHashSet();
            work.State = work.State with
            {
                Players = work.State.Players
                    .Select(p => p.Kind == NickMatchKind.Similar ? p with { Confirmed = confirmed.Contains(p.UserId) } : p)
                    .ToList(),
            };
            return Task.CompletedTask;
        }, ct);

    public Task<DiscordReportView> CancelAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, status => status is DiscordReportStatus.Processing or DiscordReportStatus.Pending, work =>
        {
            work.Report.Status = DiscordReportStatus.Cancelled;
            work.Report.CompletedAtUtc = Now();
            return Task.CompletedTask;
        }, ct);

    public Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    private static bool IsPending(DiscordReportStatus status) => status == DiscordReportStatus.Pending;

    /// <summary>
    /// Runs the guards and, when the report is in a status accepted by <paramref name="canRun"/>, the operation;
    /// the state is saved afterwards. A report in another live status is returned unchanged.
    /// </summary>
    private async Task<DiscordReportView> RunAsync(
        Guid reportId,
        string discordUserId,
        Func<DiscordReportStatus, bool> canRun,
        Func<Work, Task> operation,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.DiscordReports.SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return MissingView(reportId, DiscordReportOutcome.NotFound);
        if (report.DiscordUserId != discordUserId)
            return MissingView(reportId, DiscordReportOutcome.NotOwner);

        if (IsFinished(report.Status))
            return await ViewOfAsync(db, report, DiscordReportOutcome.Finished, ct);

        if (time.GetUtcNow().UtcDateTime > report.CreatedAtUtc + Lifetime)
        {
            report.Status = DiscordReportStatus.Expired;
            report.CompletedAtUtc = Now();
            await db.SaveChangesAsync(ct);
            return await ViewOfAsync(db, report, DiscordReportOutcome.Expired, ct);
        }

        if (canRun(report.Status))
        {
            var activities = await db.LoadActivitiesAsync(ct);
            var work = new Work(
                db, report, Deserialize(report.StateJson), activities, AppTime.TodayInPrague(time), await db.GetTrackingStartAsync(ct));
            await operation(work);
            report.StateJson = Serialize(work.State);
            await db.SaveChangesAsync(ct);
        }

        return await ViewOfAsync(db, report, DiscordReportOutcome.Ok, ct);
    }

    private async Task<DiscordReportView> ViewOfAsync(AppDbContext db, DiscordReport report, DiscordReportOutcome outcome, CancellationToken ct)
    {
        var state = Deserialize(report.StateJson);
        var dateOptions = ReportEventPlanner.DateOptions(AppTime.TodayInPrague(time), await db.GetTrackingStartAsync(ct));
        var eventOptions = state.Date is { } date
            ? ReportEventPlanner.EventOptions(await db.LoadActivitiesAsync(ct), date)
            : [];
        return new DiscordReportView(report.Id, outcome, report.Status, report.DiscordUserName, state, dateOptions, eventOptions);
    }

    /// <summary>View without any report data, for unknown reports and for callers who do not own the report.</summary>
    private static DiscordReportView MissingView(Guid reportId, DiscordReportOutcome outcome) =>
        new(reportId, outcome, DiscordReportStatus.Failed, string.Empty, new DiscordReportState(), [], []);

    private static DiscordReportOutcome StatusOutcome(DiscordReportStatus status) =>
        IsFinished(status) ? DiscordReportOutcome.Finished : DiscordReportOutcome.Ok;

    private static bool IsFinished(DiscordReportStatus status) =>
        status is DiscordReportStatus.Completed or DiscordReportStatus.Cancelled or DiscordReportStatus.Expired or DiscordReportStatus.Failed;

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    private static DateTimeOffset ReportTime(DiscordReport report) =>
        new(DateTime.SpecifyKind(report.CreatedAtUtc, DateTimeKind.Utc));

    /// <summary>Events in the preselect window that any of the matched players already has.</summary>
    private static async Task<IReadOnlySet<AttendanceEntry>> LoadRecordedAsync(
        AppDbContext db, IReadOnlyList<ReportPlayer> players, DateTimeOffset reportTime, CancellationToken ct)
    {
        if (players.Count == 0)
            return new HashSet<AttendanceEntry>();

        var userIds = players.Select(p => p.UserId).ToList();
        var from = DateOnly.FromDateTime((reportTime - ReportEventPlanner.PreselectWindow).UtcDateTime);
        var to = DateOnly.FromDateTime(reportTime.UtcDateTime);

        var recorded = await db.Attendances
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.Date >= from && a.Date <= to)
            .Select(a => new AttendanceEntry(a.ActivityId, a.Date, a.Slot))
            .ToListAsync(ct);
        return recorded.ToHashSet();
    }

    private static string Serialize(DiscordReportState state) => JsonSerializer.Serialize(state, Json);

    private static DiscordReportState Deserialize(string json) =>
        JsonSerializer.Deserialize<DiscordReportState>(json, Json) ?? new DiscordReportState();
}
