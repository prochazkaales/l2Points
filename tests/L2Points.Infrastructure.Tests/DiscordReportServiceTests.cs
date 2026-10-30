using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Persistence;
using L2Points.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace L2Points.Infrastructure.Tests;

public class DiscordReportServiceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);
    private static readonly DateOnly Thursday = new(2026, 10, 29);
    private static readonly DateOnly Friday = new(2026, 10, 30);
    private static readonly DiscordReporter Reporter = new("1001", "Reporter", "guild-1", "channel-1");

    private sealed record Fixture(
        ServiceProvider Provider,
        IServiceScope Scope,
        FakeTimeProvider Time,
        FakeScreenshotReader Reader,
        IDiscordReportService Service,
        string Mayu) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            Scope.Dispose();
            return Provider.DisposeAsync();
        }

        public async Task<int> ActivityAsync(string code) => await TestData.ActivityIdAsync(Provider, code);

        public async Task<DiscordReport> LoadAsync(Guid id)
        {
            await using var db = Provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            return await db.DiscordReports.AsNoTracking().SingleAsync(r => r.Id == id);
        }

        public async Task<Guid> AnalyzedAsync(params string[] nicks)
        {
            Reader.Nicks = nicks;
            var id = await Service.CreateAsync(Reporter);
            await Service.AnalyzeAsync(id, [1, 2, 3], "image/png");
            return id;
        }
    }

    private static async Task<Fixture> CreateAsync()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-30T19:45:00Z"));
        var provider = TestServices.Create(timeProvider: time);
        await TestData.SeedCatalogAsync(provider);
        await TestData.CreatePlayerAsync(provider, "Hubkins1010", Monday);
        await TestData.CreatePlayerAsync(provider, "Volume", Monday);
        var mayu = await TestData.CreatePlayerAsync(provider, "Mayu", Monday);
        await TestData.CreateReaderAsync(provider, "Ghost", Monday, showInStats: false);
        await TestData.CreatePlayerAsync(provider, "Gone", Monday, deactivatedAt: Thursday);
        var scope = provider.CreateScope();
        return new Fixture(
            provider,
            scope,
            time,
            provider.GetRequiredService<FakeScreenshotReader>(),
            scope.ServiceProvider.GetRequiredService<IDiscordReportService>(),
            mayu.Id);
    }

    [Fact]
    public async Task Create_stores_processing_report()
    {
        await using var f = await CreateAsync();

        var id = await f.Service.CreateAsync(Reporter);

        var report = await f.LoadAsync(id);
        Assert.Equal(DiscordReportStatus.Processing, report.Status);
        Assert.Equal("1001", report.DiscordUserId);
        Assert.Equal("Reporter", report.DiscordUserName);
        Assert.Equal("guild-1", report.GuildId);
        Assert.Equal("channel-1", report.ChannelId);
        Assert.Equal(new DateTime(2026, 10, 30, 19, 45, 0, DateTimeKind.Utc), report.CreatedAtUtc);
        Assert.Null(report.CompletedAtUtc);
        Assert.Equal("{}", report.StateJson);
    }

    [Fact]
    public async Task Analyze_matches_and_preselects_evening_block()
    {
        await using var f = await CreateAsync();
        f.Reader.Nicks = ["Hubkins1010", "V0lume", "Stranger"];
        var id = await f.Service.CreateAsync(Reporter);

        var view = await f.Service.AnalyzeAsync(id, [1, 2, 3], "image/png");

        Assert.Equal(DiscordReportStatus.Pending, view.Status);
        Assert.Equal(DiscordReportOutcome.Ok, view.Outcome);
        Assert.Equal("Reporter", view.ReporterName);
        Assert.True(view.State.PanelFound);
        Assert.Equal(["Hubkins1010", "V0lume", "Stranger"], view.State.ReadNicks);
        Assert.Collection(
            view.State.Players,
            p =>
            {
                Assert.Equal("Hubkins1010", p.Nick);
                Assert.Equal(NickMatchKind.Exact, p.Kind);
                Assert.True(p.Confirmed);
            },
            p =>
            {
                Assert.Equal("Volume", p.Nick);
                Assert.Equal("V0lume", p.ReadNick);
                Assert.Equal(NickMatchKind.Similar, p.Kind);
                Assert.False(p.Confirmed);
            });
        Assert.Equal(["Stranger"], view.State.UnknownNicks);
        Assert.Equal(Friday, view.State.Date);

        var expected = new[] { "ChaoticQueenAnt", "ChaoticCore", "ChaoticOrfen", "ChaoticZaken" };
        var selected = view.State.SelectedEvents.ToList();
        Assert.Equal(4, selected.Count);
        foreach (var code in expected)
            Assert.Contains(new AttendanceEntry(await f.ActivityAsync(code), Friday, EventSlot.Evening), selected);

        Assert.Equal(5, view.DateOptions.Count); // 30.10. back to the tracking start 26.10.
        Assert.Equal(Friday, view.DateOptions[0]);
        Assert.Equal(Friday, view.EventOptions[0].Event.Date);
        Assert.Equal(DiscordReportStatus.Pending, (await f.LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Analyze_excludes_hidden_and_deactivated_accounts()
    {
        await using var f = await CreateAsync();

        var id = await f.AnalyzedAsync("Ghost", "Gone");

        var state = (await f.Service.GetAsync(id, "1001")).State;
        Assert.Empty(state.Players);
        Assert.Equal(["Ghost", "Gone"], state.UnknownNicks);
    }

    [Fact]
    public async Task Analyze_passes_candidate_nicks_to_reader()
    {
        await using var f = await CreateAsync();

        await f.AnalyzedAsync("Hubkins1010");

        Assert.Contains("Hubkins1010", f.Reader.KnownNicks);
        Assert.DoesNotContain("Ghost", f.Reader.KnownNicks);
        Assert.DoesNotContain("Gone", f.Reader.KnownNicks);
    }

    [Fact]
    public async Task Analyze_without_panel_fails_report()
    {
        await using var f = await CreateAsync();
        f.Reader.PanelFound = false;
        f.Reader.Nicks = [];
        var id = await f.Service.CreateAsync(Reporter);

        var view = await f.Service.AnalyzeAsync(id, [1], "image/png");

        Assert.Equal(DiscordReportStatus.Failed, view.Status);
        Assert.False(view.State.PanelFound);
        Assert.Equal(DiscordReportStatus.Failed, (await f.LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Analyze_with_panel_but_no_nicks_fails_report()
    {
        await using var f = await CreateAsync();
        f.Reader.PanelFound = true;
        f.Reader.Nicks = [];
        var id = await f.Service.CreateAsync(Reporter);

        var view = await f.Service.AnalyzeAsync(id, [1], "image/png");

        Assert.Equal(DiscordReportStatus.Failed, view.Status);
        Assert.True(view.State.PanelFound);
    }

    [Fact]
    public async Task Reader_failure_propagates_and_report_can_be_marked_failed()
    {
        await using var f = await CreateAsync();
        f.Reader.Failure = new ScreenshotReadException("boom");
        var id = await f.Service.CreateAsync(Reporter);

        await Assert.ThrowsAsync<ScreenshotReadException>(() => f.Service.AnalyzeAsync(id, [1], "image/png"));
        var view = await f.Service.MarkFailedAsync(id);

        Assert.Equal(DiscordReportStatus.Failed, view.Status);
        Assert.NotNull((await f.LoadAsync(id)).CompletedAtUtc);
    }

    [Fact]
    public async Task Already_recorded_events_are_not_preselected()
    {
        await using var f = await CreateAsync();
        await TestData.AddAttendanceAsync(f.Provider, f.Mayu, "ChaoticQueenAnt", Friday, EventSlot.Evening);

        var id = await f.AnalyzedAsync("Mayu");

        var selected = (await f.Service.GetAsync(id, "1001")).State.SelectedEvents;
        Assert.Equal(3, selected.Count);
        Assert.DoesNotContain(new AttendanceEntry(await f.ActivityAsync("ChaoticQueenAnt"), Friday, EventSlot.Evening), selected);
    }

    [Fact]
    public async Task Select_date_recomputes_events()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010");

        var view = await f.Service.SelectDateAsync(id, "1001", Thursday);

        Assert.Equal(Thursday, view.State.Date);
        Assert.Empty(view.State.SelectedEvents);
        Assert.NotEmpty(view.EventOptions);
        Assert.All(view.EventOptions, o => Assert.Equal(Thursday, o.Event.Date));

        var outside = await f.Service.SelectDateAsync(id, "1001", new DateOnly(2026, 10, 1));

        Assert.Equal(Thursday, outside.State.Date);
        Assert.Equal(Thursday, (await f.Service.GetAsync(id, "1001")).State.Date);
    }

    [Fact]
    public async Task Select_events_ignores_events_not_offered()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010");
        var baium = new AttendanceEntry(await f.ActivityAsync("Baium"), Friday, EventSlot.None);
        var orfen = new AttendanceEntry(await f.ActivityAsync("ChaoticOrfen"), Friday, EventSlot.Evening);

        var view = await f.Service.SelectEventsAsync(id, "1001", [baium, orfen]);

        Assert.Equal([orfen], view.State.SelectedEvents);
        Assert.Equal([orfen], (await f.Service.GetAsync(id, "1001")).State.SelectedEvents);
    }

    [Fact]
    public async Task Confirm_similar_sets_only_listed_players()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010", "V0lume");
        var volume = (await f.Service.GetAsync(id, "1001")).State.Players.Single(p => p.Nick == "Volume");

        var confirmed = await f.Service.ConfirmSimilarAsync(id, "1001", [volume.UserId]);

        Assert.True(confirmed.State.Players.Single(p => p.Nick == "Volume").Confirmed);
        Assert.True(confirmed.State.Players.Single(p => p.Nick == "Hubkins1010").Confirmed);

        var cleared = await f.Service.ConfirmSimilarAsync(id, "1001", []);

        Assert.False(cleared.State.Players.Single(p => p.Nick == "Volume").Confirmed);
        Assert.True(cleared.State.Players.Single(p => p.Nick == "Hubkins1010").Confirmed);
    }

    [Fact]
    public async Task Other_user_cannot_change_report()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010");

        var view = await f.Service.SelectDateAsync(id, "9999", Thursday);

        Assert.Equal(DiscordReportOutcome.NotOwner, view.Outcome);
        Assert.Equal(Friday, (await f.Service.GetAsync(id, "1001")).State.Date);
    }

    [Fact]
    public async Task Unknown_report_is_not_found()
    {
        await using var f = await CreateAsync();

        var view = await f.Service.GetAsync(Guid.NewGuid(), "1001");

        Assert.Equal(DiscordReportOutcome.NotFound, view.Outcome);
    }

    [Fact]
    public async Task Changes_after_lifetime_expire_report()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010");
        f.Time.Advance(TimeSpan.FromMinutes(16));

        var view = await f.Service.SelectDateAsync(id, "1001", Thursday);

        Assert.Equal(DiscordReportOutcome.Expired, view.Outcome);
        Assert.Equal(DiscordReportStatus.Expired, (await f.LoadAsync(id)).Status);
        Assert.Equal(DiscordReportOutcome.Finished, (await f.Service.GetAsync(id, "1001")).Outcome);
    }

    [Fact]
    public async Task Cancel_finishes_report()
    {
        await using var f = await CreateAsync();
        var id = await f.AnalyzedAsync("Hubkins1010");

        var cancelled = await f.Service.CancelAsync(id, "1001");

        Assert.Equal(DiscordReportOutcome.Ok, cancelled.Outcome);
        Assert.Equal(DiscordReportStatus.Cancelled, cancelled.Status);
        Assert.NotNull((await f.LoadAsync(id)).CompletedAtUtc);

        var afterwards = await f.Service.SelectDateAsync(id, "1001", Thursday);

        Assert.Equal(DiscordReportOutcome.Finished, afterwards.Outcome);
        Assert.Equal(Friday, afterwards.State.Date);
    }

    [Fact]
    public async Task Choices_are_ignored_while_processing()
    {
        await using var f = await CreateAsync();
        var id = await f.Service.CreateAsync(Reporter);

        var view = await f.Service.SelectDateAsync(id, "1001", Thursday);

        Assert.Equal(DiscordReportOutcome.Ok, view.Outcome);
        Assert.Equal(DiscordReportStatus.Processing, view.Status);
        Assert.Null(view.State.Date);
        Assert.Empty(view.EventOptions);
    }
}
