using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Reports;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;
using L2Points.Infrastructure.Reports;
using L2Points.Infrastructure.Services;
using L2Points.Web.Formatting;

namespace L2Points.Web.Discord;

/// <summary>Builds the messages of the report bot. Pure and never throwing; limits of Discord are enforced here.</summary>
public static class DiscordMessages
{
    private const int MaxSelectOptions = 25;
    private const int MaxLabelLength = 100;
    private const int MaxContentLength = 2000;
    private const int SecondaryStyle = 2;
    private const int SuccessStyle = 3;

    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs-CZ");

    /// <summary>Czech copy of the short answers.</summary>
    public static class Texts
    {
        public const string Expired = "Report vypršel, pošli /report znovu.";
        public const string NotOwner = "Tenhle report patří někomu jinému.";
        public const string WrongGuild = "Tenhle bot funguje jen na serveru party.";
        public const string PanelNotFound = "Na screenu jsem nenašel panel Party Members. Pošli screen rozbalené party.";
        public const string ProcessingFailed = "Screen se nepodařilo zpracovat, zkus to znovu.";
        public const string BadAttachment = "Příloha musí být obrázek PNG, JPG nebo WEBP do 10 MB.";
        public const string Cancelled = "Report zrušen, nic se nezapsalo.";
        public const string Closed = "Report už je uzavřený.";
    }

    /// <summary>The interactive panel: found players, day and event selects, confirmation of similar nicks, submit and cancel.</summary>
    public static DiscordMessage ReportPanel(DiscordReportView view)
    {
        var state = view.State;
        var hasPlayers = state.Players.Count > 0;

        var rows = new List<DiscordComponent>();
        if (hasPlayers)
        {
            AddIfSome(rows, DateSelect(view));
            AddIfSome(rows, EventsSelect(view));
            AddIfSome(rows, SimilarSelect(view));
        }

        var canSubmit = state.Players.Any(p => p.Confirmed) && state.SelectedEvents.Count > 0;
        var buttons = new List<DiscordComponent>();
        if (hasPlayers)
            buttons.Add(Button("Zapsat", SuccessStyle, DiscordCustomId.Format(view.Id, DiscordCustomId.Submit), disabled: !canSubmit));
        buttons.Add(Button("Zrušit", SecondaryStyle, DiscordCustomId.Format(view.Id, DiscordCustomId.Cancel), disabled: false));
        rows.Add(Row(buttons));

        return Ephemeral(PanelContent(state), rows);
    }

    /// <summary>Ephemeral confirmation for the reporter after a submit.</summary>
    public static DiscordMessage Result(DiscordReportView view) =>
        SummaryLines(view) is { } lines
            ? Ephemeral("Zapsáno.\n" + lines)
            : Notice(Texts.Closed);

    /// <summary>The message everyone in the channel sees after a submit.</summary>
    public static DiscordMessage PublicSummary(DiscordReportView view) =>
        SummaryLines(view) is { } lines
            ? new DiscordMessage(Cap(lines), AllowedMentions: DiscordAllowedMentions.None)
            : Notice(Texts.Closed);

    public static DiscordMessage Notice(string text) => Ephemeral(text);

    private static DiscordMessage Ephemeral(string content, IReadOnlyList<DiscordComponent>? components = null) =>
        new(Cap(content), DiscordMessage.Ephemeral, components, DiscordAllowedMentions.None);

    private static string Cap(string content) =>
        content.Length <= MaxContentLength ? content : content[..(MaxContentLength - 1)] + "…";

    private static string PanelContent(DiscordReportState state)
    {
        if (state.Players.Count == 0)
            return $"Na screenu není nikdo z aplikace. Přečtené nicky: {JoinOrNone(state.ReadNicks)}";

        var lines = new List<string>();

        var found = state.Players.Where(p => p.Confirmed).Select(p => p.Nick).ToList();
        if (found.Count > 0)
            lines.Add($"**Nalezení hráči:** {string.Join(", ", found)}");

        var unconfirmed = state.Players.Where(p => !p.Confirmed).Select(p => $"{p.ReadNick} → {p.Nick}").ToList();
        if (unconfirmed.Count > 0)
            lines.Add($"**Podobné nicky – potvrď v menu:** {string.Join(", ", unconfirmed)}");

        if (state.UnknownNicks.Count > 0)
            lines.Add($"**Neznámé nicky (nezapíšou se):** {string.Join(", ", state.UnknownNicks)}");

        return string.Join("\n", lines);
    }

    private static string JoinOrNone(IReadOnlyList<string> nicks) => nicks.Count == 0 ? "žádné" : string.Join(", ", nicks);

    private static DiscordComponent? DateSelect(DiscordReportView view)
    {
        var options = view.DateOptions.Take(MaxSelectOptions)
            .Select(d => new DiscordSelectOption(
                d.ToString("ddd d. M.", Czech),
                d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                d == view.State.Date))
            .ToList();

        return options.Count == 0
            ? null
            : Select(view, DiscordCustomId.Date, "Vyber den", min: 1, max: 1, options);
    }

    private static DiscordComponent? EventsSelect(DiscordReportView view)
    {
        var selected = view.State.SelectedEvents.ToHashSet();
        var options = TrimToLimit(view.EventOptions)
            .Select(o => new DiscordSelectOption(EventLabel(o), DiscordCustomId.EventValue(o.Event), selected.Contains(o.Event)))
            .ToList();

        return options.Count == 0
            ? null
            : Select(view, DiscordCustomId.Events, "Vyber epicy", min: 0, max: options.Count, options);
    }

    private static DiscordComponent? SimilarSelect(DiscordReportView view)
    {
        var options = view.State.Players
            .Where(p => p.Kind == NickMatchKind.Similar)
            .Take(MaxSelectOptions)
            .Select(p => new DiscordSelectOption(Truncate($"{p.ReadNick} → {p.Nick}"), p.UserId, p.Confirmed))
            .ToList();

        return options.Count == 0
            ? null
            : Select(view, DiscordCustomId.Similar, "Potvrď podobné nicky", min: 0, max: options.Count, options);
    }

    /// <summary>A select holds 25 options: flexible events (no spawn time) are dropped from the end first, then the rest.</summary>
    private static List<ReportEventOption> TrimToLimit(IReadOnlyList<ReportEventOption> options)
    {
        var result = options.ToList();
        for (var i = result.Count - 1; i >= 0 && result.Count > MaxSelectOptions; i--)
        {
            if (result[i].SpawnUtc is null)
                result.RemoveAt(i);
        }

        return result.Take(MaxSelectOptions).ToList();
    }

    private static string EventLabel(ReportEventOption option)
    {
        var label = SlotLabelOf(option.Activity.Name, option.Event.Slot);
        if (option.SpawnUtc is { } spawn)
        {
            var utc = DateTime.SpecifyKind(spawn, DateTimeKind.Utc);
            label += $" ({TimeZoneInfo.ConvertTimeFromUtc(utc, AppTime.Prague):HH:mm})";
        }

        return Truncate(label);
    }

    private static string SlotLabelOf(string activityName, EventSlot slot) =>
        slot == EventSlot.None ? activityName : $"{activityName} – {DisplayFormat.SlotLabel(slot)}";

    /// <summary>Summary lines of a finished report, or null while the view holds no result.</summary>
    private static string? SummaryLines(DiscordReportView view)
    {
        if (view.State.Result is not { } result)
            return null;

        var events = result.Events;
        var date = view.State.Date ?? events.Select(e => (DateOnly?)e.Event.Date).FirstOrDefault();
        var dateText = date?.ToString("d. M.", Czech) ?? "";
        var labels = string.Join(", ", events.Select(e => SlotLabelOf(e.ActivityName, e.Event.Slot)));

        var lines = new List<string>
        {
            $"{dateText} – {labels}: zapsáno {PlayerCount(CountPlayers(view.State, events))}. Report: {view.ReporterName}",
        };

        foreach (var e in events)
        {
            var activity = SlotLabelOf(e.ActivityName, e.Event.Slot);
            lines.AddRange(e.Issues.Select(i => $"{i.Nick}: {activity} – {Reason(i.Reason)}"));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Distinct players with at least one recorded or already present attendance. The result only has per-event counts,
    /// so a confirmed player counts unless an issue was reported for them in every event.
    /// </summary>
    private static int CountPlayers(DiscordReportState state, IReadOnlyList<ReportEventResult> events)
    {
        if (events.Count == 0)
            return 0;

        var fromPlayers = state.Players
            .Where(p => p.Confirmed)
            .Count(p => events.Any(e => e.Issues.All(i => i.Nick != p.Nick)));
        var largestEvent = events.Max(e => e.Added + e.AlreadyHad);
        return Math.Max(fromPlayers, largestEvent);
    }

    private static string PlayerCount(int count) => count switch
    {
        1 => "1 hráč",
        >= 2 and <= 4 => $"{count} hráči",
        _ => $"{count} hráčů",
    };

    private static string Reason(AttendanceChangeResult reason) => reason switch
    {
        AttendanceChangeResult.WeeklyLimitReached => "týdenní limit",
        AttendanceChangeResult.ReadOnlyAccount => "účet jen ke čtení",
        _ => "nezapsáno",
    };

    private static string Truncate(string text) =>
        text.Length <= MaxLabelLength ? text : text[..(MaxLabelLength - 1)] + "…";

    private static void AddIfSome(List<DiscordComponent> rows, DiscordComponent? select)
    {
        if (select is not null)
            rows.Add(Row([select]));
    }

    private static DiscordComponent Row(IReadOnlyList<DiscordComponent> components) =>
        new(DiscordComponent.ActionRowType, Components: components);

    private static DiscordComponent Button(string label, int style, string customId, bool disabled) =>
        new(DiscordComponent.ButtonType, Style: style, Label: label, CustomId: customId, Disabled: disabled);

    private static DiscordComponent Select(
        DiscordReportView view, string action, string placeholder, int min, int max, IReadOnlyList<DiscordSelectOption> options) =>
        new(
            DiscordComponent.StringSelectType,
            CustomId: DiscordCustomId.Format(view.Id, action),
            Placeholder: placeholder,
            MinValues: min,
            MaxValues: max,
            Options: options);
}
