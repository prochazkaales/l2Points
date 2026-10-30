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
public sealed class DiscordReportService(
    IDbContextFactory<AppDbContext> dbFactory,
    IScreenshotReader reader,
    AttendanceService attendance,
    TimeProvider time)
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
        var current = await saveDb.DiscordReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (current is null)
            return MissingView(reportId, DiscordReportOutcome.NotFound);
        if (current.Status != DiscordReportStatus.Processing)
            return await ViewOfAsync(saveDb, current, StatusOutcome(current.Status), ct);

        if (!read.PanelFound || read.Nicks.Count == 0)
        {
            return await SaveAnalysisAsync(
                saveDb,
                reportId,
                DiscordReportStatus.Failed,
                Now(),
                new DiscordReportState { PanelFound = read.PanelFound, ReadNicks = read.Nicks },
                ct);
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

        return await SaveAnalysisAsync(
            saveDb,
            reportId,
            DiscordReportStatus.Pending,
            null,
            new DiscordReportState
            {
                PanelFound = true,
                ReadNicks = read.Nicks,
                Players = players,
                UnknownNicks = match.Unknown,
                Date = date,
                SelectedEvents = preselected.Where(e => e.Date == date).ToList(),
            },
            ct);
    }

    /// <summary>
    /// Stores the analysis only while the report is still <c>Processing</c>, so a cancel committed while the reader ran wins.
    /// Returns the report as stored afterwards.
    /// </summary>
    private async Task<DiscordReportView> SaveAnalysisAsync(
        AppDbContext db, Guid reportId, DiscordReportStatus status, DateTime? completedAtUtc, DiscordReportState state, CancellationToken ct)
    {
        var saved = await TrySaveAsync(db, reportId, DiscordReportStatus.Processing, status, completedAtUtc, Serialize(state), ct);
        return await StoredViewAsync(db, reportId, saved ? DiscordReportOutcome.Ok : null, ct);
    }

    /// <summary>
    /// Writes status, completion time and state, but only while the report still has <paramref name="expectedStatus"/>; a change committed
    /// meanwhile by another call (cancel, submit, expiry) is never overwritten. Returns false when the report was no longer in that status.
    /// </summary>
    private static async Task<bool> TrySaveAsync(
        AppDbContext db,
        Guid reportId,
        DiscordReportStatus expectedStatus,
        DiscordReportStatus status,
        DateTime? completedAtUtc,
        string stateJson,
        CancellationToken ct) =>
        await db.DiscordReports
            .Where(r => r.Id == reportId && r.Status == expectedStatus)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, status)
                    .SetProperty(r => r.CompletedAtUtc, completedAtUtc)
                    .SetProperty(r => r.StateJson, stateJson),
                ct) == 1;

    /// <summary>The report as stored now; without an explicit <paramref name="outcome"/> the outcome follows the stored status.</summary>
    private async Task<DiscordReportView> StoredViewAsync(AppDbContext db, Guid reportId, DiscordReportOutcome? outcome, CancellationToken ct)
    {
        var stored = await db.DiscordReports.AsNoTracking().SingleAsync(r => r.Id == reportId, ct);
        return await ViewOfAsync(db, stored, outcome ?? StatusOutcome(stored.Status), ct);
    }

    public async Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.DiscordReports.SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return MissingView(reportId, DiscordReportOutcome.NotFound);

        if (report.Status == DiscordReportStatus.Processing)
            await TrySaveAsync(db, reportId, DiscordReportStatus.Processing, DiscordReportStatus.Failed, Now(), report.StateJson, ct);

        return await StoredViewAsync(db, reportId, null, ct);
    }

    public Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
        RunAsync(reportId, discordUserId, _ => false, _ => Task.CompletedTask, ct);

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

    public async Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var (report, early) = await GuardAsync(db, reportId, discordUserId, ct);
        if (early is not null)
            return early;

        var state = Deserialize(report!.StateJson);
        var players = state.Players.Where(p => p.Confirmed).ToList();
        if (report.Status != DiscordReportStatus.Pending || players.Count == 0 || state.SelectedEvents.Count == 0)
            return await ViewOfAsync(db, report, DiscordReportOutcome.Ok, ct);

        // Only one caller can move the report from Pending to Submitting; the others get the finished report.
        var claimed = await db.DiscordReports
            .Where(r => r.Id == reportId && r.Status == DiscordReportStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, DiscordReportStatus.Submitting), ct);
        // A lost claim means another call got there first: show what is stored (Submitting is still in progress, not finished).
        if (claimed == 0)
            return await StoredViewAsync(db, reportId, null, CancellationToken.None);

        // From here on the report is Submitting and nothing else settles it, so the phase ignores the caller's cancellation
        // and any failure moves the report to Failed (with the partial result) instead of leaving it stuck.
        var results = new List<ReportEventResult>();
        try
        {
            // A choice committed between the guard read and the claim is what the reporter saw last, so the state is read again.
            state = Deserialize(await db.DiscordReports
                .AsNoTracking()
                .Where(r => r.Id == reportId)
                .Select(r => r.StateJson)
                .SingleAsync(CancellationToken.None));
            players = state.Players.Where(p => p.Confirmed).ToList();
            if (players.Count == 0 || state.SelectedEvents.Count == 0)
            {
                // Nothing to write any more: release the claim so the reporter can change the choice.
                await db.DiscordReports
                    .Where(r => r.Id == reportId && r.Status == DiscordReportStatus.Submitting)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, DiscordReportStatus.Pending), CancellationToken.None);
                return await StoredViewAsync(db, reportId, DiscordReportOutcome.Ok, CancellationToken.None);
            }

            var activities = await db.LoadActivitiesAsync(CancellationToken.None);
            var userIds = players.Select(p => p.UserId).ToList();
            var dates = state.SelectedEvents.Select(e => e.Date).Distinct().ToList();
            var existing = (await db.Attendances
                    .AsNoTracking()
                    .Where(a => userIds.Contains(a.UserId) && dates.Contains(a.Date))
                    .Select(a => new { a.UserId, a.ActivityId, a.Date, a.Slot })
                    .ToListAsync(CancellationToken.None))
                .Select(a => (a.UserId, new AttendanceEntry(a.ActivityId, a.Date, a.Slot)))
                .ToHashSet();

            var actor = ActorId(reportId);
            foreach (var entry in state.SelectedEvents)
            {
                int added = 0, alreadyHad = 0;
                var issues = new List<ReportPlayerIssue>();
                var name = activities.FirstOrDefault(a => a.Id == entry.ActivityId)?.Name ?? string.Empty;
                try
                {
                    foreach (var player in players)
                    {
                        if (existing.Contains((player.UserId, entry)))
                        {
                            alreadyHad++;
                            continue;
                        }

                        var result = await attendance.SetAttendanceAsync(
                            actor, true, player.UserId, entry.ActivityId, entry.Date, entry.Slot, true, CancellationToken.None);
                        if (result == AttendanceChangeResult.Ok)
                            added++;
                        else
                            issues.Add(new ReportPlayerIssue(player.Nick, result));
                    }
                }
                catch
                {
                    results.Add(new ReportEventResult(entry, name, added, alreadyHad, issues));
                    throw;
                }

                results.Add(new ReportEventResult(entry, name, added, alreadyHad, issues));
            }

            await TrySaveAsync(
                db,
                reportId,
                DiscordReportStatus.Submitting,
                DiscordReportStatus.Completed,
                Now(),
                Serialize(state with { Result = new ReportResult(results) }),
                CancellationToken.None);
        }
        catch
        {
            try
            {
                await TrySaveAsync(
                    db,
                    reportId,
                    DiscordReportStatus.Submitting,
                    DiscordReportStatus.Failed,
                    Now(),
                    Serialize(state with { Result = new ReportResult(results) }),
                    CancellationToken.None);
            }
            catch
            {
                // Settling is best effort; the original failure is what the caller has to see.
            }

            throw;
        }

        return await StoredViewAsync(db, reportId, DiscordReportOutcome.Ok, CancellationToken.None);
    }

    public async Task<int> FailStaleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = Now();
        var createdBefore = now - Lifetime;
        return await db.DiscordReports
            .Where(r => (r.Status == DiscordReportStatus.Processing || r.Status == DiscordReportStatus.Submitting)
                && r.CreatedAtUtc < createdBefore)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, DiscordReportStatus.Failed)
                    .SetProperty(r => r.CompletedAtUtc, now),
                ct);
    }

    /// <summary>Id stored as <c>CreatedByUserId</c> of attendance written by a report.</summary>
    public static string ActorId(Guid reportId) => $"discord-report:{reportId:N}";

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
        var (loaded, early) = await GuardAsync(db, reportId, discordUserId, ct);
        if (early is not null)
            return early;
        var report = loaded!;

        var loadedStatus = report.Status;
        if (canRun(loadedStatus))
        {
            var activities = await db.LoadActivitiesAsync(ct);
            var work = new Work(
                db, report, Deserialize(report.StateJson), activities, AppTime.TodayInPrague(time), await db.GetTrackingStartAsync(ct));
            await operation(work);

            // Conditional on the status seen at load: a submit or cancel committed meanwhile wins and is shown as stored.
            if (!await TrySaveAsync(db, reportId, loadedStatus, report.Status, report.CompletedAtUtc, Serialize(work.State), ct))
                return await StoredViewAsync(db, reportId, null, ct);
            return await StoredViewAsync(db, reportId, DiscordReportOutcome.Ok, ct);
        }

        return await ViewOfAsync(db, report, DiscordReportOutcome.Ok, ct);
    }

    /// <summary>
    /// The checks every user call starts with: unknown id, owner, finished, expired. Returns the loaded (tracked) report when the call may
    /// go on, otherwise the view to return. A <c>Submitting</c> report never expires: its submit is in progress and settles the status itself.
    /// </summary>
    private async Task<(DiscordReport? Report, DiscordReportView? Early)> GuardAsync(
        AppDbContext db, Guid reportId, string discordUserId, CancellationToken ct)
    {
        var report = await db.DiscordReports.SingleOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return (null, MissingView(reportId, DiscordReportOutcome.NotFound));
        if (report.DiscordUserId != discordUserId)
            return (null, MissingView(reportId, DiscordReportOutcome.NotOwner));

        if (IsFinished(report.Status))
            return (null, await ViewOfAsync(db, report, DiscordReportOutcome.Finished, ct));

        if (report.Status != DiscordReportStatus.Submitting && time.GetUtcNow().UtcDateTime > report.CreatedAtUtc + Lifetime)
        {
            var expired = await TrySaveAsync(db, reportId, report.Status, DiscordReportStatus.Expired, Now(), report.StateJson, ct);
            return (null, await StoredViewAsync(db, reportId, expired ? DiscordReportOutcome.Expired : null, ct));
        }

        return (report, null);
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
