using System.Text.Json;
using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Reports;
using L2Points.Infrastructure.Services;
using L2Points.Web.Discord;

namespace L2Points.Web.Tests.Discord;

public class DiscordMessagesTests
{
    private static readonly DateOnly Day = new(2026, 10, 30);

    private static Activity Act(int id, string name, ActivityCategory category = ActivityCategory.NormalEpic) =>
        new() { Id = id, Name = name, Category = category };

    private static ReportEventOption Option(int id, string name, EventSlot slot = EventSlot.None, DateTime? spawnUtc = null) =>
        new(new AttendanceEntry(id, Day, slot), Act(id, name, slot == EventSlot.None ? ActivityCategory.NormalEpic : ActivityCategory.ChaoticEpic), spawnUtc);

    private static ReportPlayer Exact(string nick) => new("u-" + nick, nick, nick, NickMatchKind.Exact, true);

    private static ReportPlayer Similar(string read, string nick, bool confirmed = false) =>
        new("u-" + nick, nick, read, NickMatchKind.Similar, confirmed);

    private static DiscordReportView View(
        DiscordReportState? state = null,
        IReadOnlyList<ReportEventOption>? events = null,
        IReadOnlyList<DateOnly>? dates = null) =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), DiscordReportOutcome.Ok, DiscordReportStatus.Pending, "Alice",
            state ?? new DiscordReportState { PanelFound = true },
            dates ?? [Day, Day.AddDays(-1)],
            events ?? []);

    private static DiscordReportState State(
        IReadOnlyList<ReportPlayer>? players = null,
        IReadOnlyList<AttendanceEntry>? selected = null,
        DateOnly? date = null,
        IReadOnlyList<string>? unknown = null,
        ReportResult? result = null) =>
        new()
        {
            PanelFound = true,
            ReadNicks = (players ?? []).Select(p => p.ReadNick).Concat(unknown ?? []).ToList(),
            Players = players ?? [],
            UnknownNicks = unknown ?? [],
            Date = date ?? Day,
            SelectedEvents = selected ?? [],
            Result = result,
        };

    private static JsonElement Json(DiscordMessage message) =>
        JsonSerializer.SerializeToElement(message, DiscordJson.Options);

    private static List<JsonElement> Rows(DiscordMessage message) =>
        Json(message).GetProperty("components").EnumerateArray().ToList();

    private static JsonElement Select(DiscordMessage message, string action) =>
        Rows(message).Select(r => r.GetProperty("components")[0])
            .First(c => c.TryGetProperty("custom_id", out var id) && id.GetString()!.EndsWith(":" + action));

    private static JsonElement Button(DiscordMessage message, string label) =>
        Rows(message).Last().GetProperty("components").EnumerateArray().First(c => c.GetProperty("label").GetString() == label);

    private static DiscordReportView ViewWithResult(ReportResult result, IReadOnlyList<ReportPlayer>? players = null) =>
        View(State(players ?? [Exact("Bob"), Exact("Cyd")], date: Day, result: result));

    [Fact]
    public void Panel_with_similar_has_four_rows()
    {
        var state = State([Exact("Bob"), Similar("B0b", "Bob2")], [new AttendanceEntry(1, Day, EventSlot.None)]);
        var message = DiscordMessages.ReportPanel(View(state, [Option(1, "Baium")]));

        var rows = Rows(message);
        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.GetProperty("type").GetInt32()));
        Assert.Equal(64, Json(message).GetProperty("flags").GetInt32());

        var similar = Select(message, DiscordCustomId.Similar);
        Assert.Equal("Potvrď podobné nicky", similar.GetProperty("placeholder").GetString());
        Assert.Equal(0, similar.GetProperty("min_values").GetInt32());
        var option = similar.GetProperty("options")[0];
        Assert.Equal("B0b → Bob2", option.GetProperty("label").GetString());
        Assert.Equal("u-Bob2", option.GetProperty("value").GetString());
        Assert.False(option.GetProperty("default").GetBoolean());
    }

    [Fact]
    public void Panel_without_similar_has_three_rows_and_selects_default_to_state()
    {
        var state = State([Exact("Bob")], [new AttendanceEntry(1, Day, EventSlot.None)], Day.AddDays(-1));
        var message = DiscordMessages.ReportPanel(View(state, [Option(1, "Baium"), Option(2, "Valakas")]));

        Assert.Equal(3, Rows(message).Count);

        var date = Select(message, DiscordCustomId.Date);
        Assert.Equal("Vyber den", date.GetProperty("placeholder").GetString());
        Assert.Equal(1, date.GetProperty("max_values").GetInt32());
        var dateOptions = date.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal("pá 30. 10.", dateOptions[0].GetProperty("label").GetString());
        Assert.Equal("2026-10-30", dateOptions[0].GetProperty("value").GetString());
        Assert.False(dateOptions[0].GetProperty("default").GetBoolean());
        Assert.Equal("čt 29. 10.", dateOptions[1].GetProperty("label").GetString());
        Assert.True(dateOptions[1].GetProperty("default").GetBoolean());

        var events = Select(message, DiscordCustomId.Events);
        Assert.Equal("Vyber epicy", events.GetProperty("placeholder").GetString());
        Assert.Equal(0, events.GetProperty("min_values").GetInt32());
        Assert.Equal(2, events.GetProperty("max_values").GetInt32());
        var eventOptions = events.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal("Baium", eventOptions[0].GetProperty("label").GetString());
        Assert.Equal("1|2026-10-30|0", eventOptions[0].GetProperty("value").GetString());
        Assert.True(eventOptions[0].GetProperty("default").GetBoolean());
        Assert.False(eventOptions[1].GetProperty("default").GetBoolean());
    }

    [Fact]
    public void Panel_content_lists_found_similar_and_unknown_nicks()
    {
        var state = State([Exact("Bob"), Similar("B0b", "Bob2"), Similar("Cyd1", "Cyd", confirmed: true)], unknown: ["Zed"]);
        var content = DiscordMessages.ReportPanel(View(state, [Option(1, "Baium")])).Content;

        Assert.Equal(
            "**Nalezení hráči:** Bob, Cyd\n" +
            "**Podobné nicky – potvrď v menu:** B0b → Bob2\n" +
            "**Neznámé nicky (nezapíšou se):** Zed",
            content);
    }

    [Fact]
    public void Submit_disabled_without_confirmed_players()
    {
        var state = State([Similar("B0b", "Bob")], [new AttendanceEntry(1, Day, EventSlot.None)]);
        var message = DiscordMessages.ReportPanel(View(state, [Option(1, "Baium")]));

        var submit = Button(message, "Zapsat");
        Assert.True(submit.GetProperty("disabled").GetBoolean());
        Assert.Equal(3, submit.GetProperty("style").GetInt32());
        Assert.Equal($"r:{View().Id:N}:submit", submit.GetProperty("custom_id").GetString());
    }

    [Fact]
    public void Submit_disabled_without_selected_event_and_enabled_with_both()
    {
        var events = new[] { Option(1, "Baium") };
        var none = DiscordMessages.ReportPanel(View(State([Exact("Bob")]), events));
        Assert.True(Button(none, "Zapsat").GetProperty("disabled").GetBoolean());

        var ready = DiscordMessages.ReportPanel(View(State([Exact("Bob")], [new AttendanceEntry(1, Day, EventSlot.None)]), events));
        Assert.False(Button(ready, "Zapsat").GetProperty("disabled").GetBoolean());

        var cancel = Button(ready, "Zrušit");
        Assert.Equal(2, cancel.GetProperty("style").GetInt32());
        Assert.Equal($"r:{View().Id:N}:cancel", cancel.GetProperty("custom_id").GetString());
    }

    [Fact]
    public void Panel_without_matches_has_no_submit()
    {
        var state = new DiscordReportState { PanelFound = true, ReadNicks = ["Abc", "Def"], UnknownNicks = ["Abc", "Def"], Date = Day };
        var message = DiscordMessages.ReportPanel(View(state, [Option(1, "Baium")]));

        Assert.Equal("Na screenu není nikdo z aplikace. Přečtené nicky: Abc, Def", message.Content);
        var row = Assert.Single(Rows(message));
        var button = Assert.Single(row.GetProperty("components").EnumerateArray());
        Assert.Equal("Zrušit", button.GetProperty("label").GetString());
    }

    [Fact]
    public void Events_are_truncated_to_25_dropping_flexible_ones_first()
    {
        var fixedOptions = Enumerable.Range(1, 20)
            .Select(i => Option(i, "Fixed " + i, spawnUtc: new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc)));
        var flexible = Enumerable.Range(21, 10).Select(i => Option(i, "Flex " + i));
        var message = DiscordMessages.ReportPanel(View(State([Exact("Bob")]), fixedOptions.Concat(flexible).ToList()));

        var events = Select(message, DiscordCustomId.Events);
        var options = events.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(25, options.Count);
        Assert.Equal(25, events.GetProperty("max_values").GetInt32());
        Assert.Equal("Flex 25", options[^1].GetProperty("label").GetString());
        Assert.Equal(20, options.Count(o => o.GetProperty("label").GetString()!.StartsWith("Fixed")));
    }

    [Fact]
    public void Event_label_has_slot_and_prague_time()
    {
        var spawn = new DateTime(2026, 10, 30, 18, 30, 0, DateTimeKind.Utc);
        var message = DiscordMessages.ReportPanel(View(State([Exact("Bob")]),
            [Option(1, "Chaotic Core", EventSlot.Evening, spawn), Option(2, "Chaotic Core", EventSlot.Morning)]));

        var options = Select(message, DiscordCustomId.Events).GetProperty("options");
        Assert.Equal("Chaotic Core – Večerní (19:30)", options[0].GetProperty("label").GetString());
        Assert.Equal("Chaotic Core – Ranní", options[1].GetProperty("label").GetString());
    }

    [Fact]
    public void Public_summary_text_and_no_mentions()
    {
        var evening = new AttendanceEntry(2, Day, EventSlot.Evening);
        var result = new ReportResult(
        [
            new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", 1, 1,
                [new ReportPlayerIssue("Dan", AttendanceChangeResult.ReadOnlyAccount),
                 new ReportPlayerIssue("Eva", AttendanceChangeResult.Forbidden)]),
            new ReportEventResult(evening, "Chaotic Core", 0, 1,
                [new ReportPlayerIssue("Cyd", AttendanceChangeResult.WeeklyLimitReached),
                 new ReportPlayerIssue("Dan", AttendanceChangeResult.ReadOnlyAccount),
                 new ReportPlayerIssue("Eva", AttendanceChangeResult.Forbidden)]),
        ]);

        var message = DiscordMessages.PublicSummary(ViewWithResult(result, [Exact("Bob"), Exact("Cyd"), Exact("Dan"), Exact("Eva")]));

        Assert.Equal(
            "30. 10. – Baium, Chaotic Core – Večerní: zapsáno 2 hráči. Report: Alice\n" +
            "Dan: Baium – účet jen ke čtení\n" +
            "Eva: Baium – nezapsáno\n" +
            "Cyd: Chaotic Core – Večerní – týdenní limit\n" +
            "Dan: Chaotic Core – Večerní – účet jen ke čtení\n" +
            "Eva: Chaotic Core – Večerní – nezapsáno",
            message.Content);
        Assert.Null(message.Flags);
        var json = Json(message);
        Assert.False(json.TryGetProperty("flags", out _));
        Assert.Equal(0, json.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
    }

    [Theory]
    [InlineData(1, "1 hráč")]
    [InlineData(2, "2 hráči")]
    [InlineData(4, "4 hráči")]
    [InlineData(5, "5 hráčů")]
    [InlineData(0, "0 hráčů")]
    public void Summary_uses_czech_plural(int added, string expected)
    {
        var players = Enumerable.Range(0, added).Select(i => Exact("P" + i)).ToList();
        var result = new ReportResult([new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", added, 0, [])]);

        Assert.Contains("zapsáno " + expected + ".", DiscordMessages.PublicSummary(ViewWithResult(result, players)).Content);
    }

    [Fact]
    public void Result_is_ephemeral_and_starts_with_confirmation()
    {
        var result = new ReportResult([new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", 2, 0, [])]);

        var message = DiscordMessages.Result(ViewWithResult(result));

        Assert.Equal("Zapsáno.\n30. 10. – Baium: zapsáno 2 hráči. Report: Alice", message.Content);
        Assert.Equal(64, Json(message).GetProperty("flags").GetInt32());
        Assert.Equal(0, Json(message).GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
    }

    [Fact]
    public void Notice_is_ephemeral_text()
    {
        var message = DiscordMessages.Notice(DiscordMessages.Texts.Expired);

        Assert.Equal("Report vypršel, pošli /report znovu.", message.Content);
        Assert.Equal(64, Json(message).GetProperty("flags").GetInt32());
        Assert.False(Json(message).TryGetProperty("components", out _));
    }

    [Fact]
    public void Missing_result_does_not_throw()
    {
        var view = View(State([Exact("Bob")]));

        Assert.Equal(DiscordMessages.Texts.Closed, DiscordMessages.Result(view).Content);
        Assert.Equal(DiscordMessages.Texts.Closed, DiscordMessages.PublicSummary(view).Content);
    }

    [Fact]
    public void Content_is_capped_to_discord_limit()
    {
        var issues = Enumerable.Range(0, 400).Select(i => new ReportPlayerIssue("Player" + i, AttendanceChangeResult.WeeklyLimitReached)).ToList();
        var result = new ReportResult([new ReportEventResult(new AttendanceEntry(1, Day, EventSlot.None), "Baium", 0, 0, issues)]);

        var content = DiscordMessages.PublicSummary(ViewWithResult(result)).Content!;

        Assert.True(content.Length <= 2000);
    }
}
