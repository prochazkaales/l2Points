using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Web.Formatting;

/// <summary>Czech display formatting shared by the attendance pages.</summary>
public static class DisplayFormat
{
    private static readonly string[] DayAbbreviations = ["Po", "Út", "St", "Čt", "Pá", "So", "Ne"];
    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs-CZ");

    /// <summary>Whole percent with a trailing " %", or an en dash when there is nothing to compare.</summary>
    public static string Percent(double? value) =>
        value is null
            ? "–"
            : $"{Math.Round(value.Value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)} %";

    /// <summary>
    /// Colour band of a participation percentage: "high" from 80 %, "mid" from 50 %, otherwise "low"; null when missing.
    /// The rounded value decides, so the band matches the shown number.
    /// </summary>
    public static string? PercentTone(double? value) => (value is null ? (double?)null : Math.Round(value.Value, MidpointRounding.AwayFromZero)) switch
    {
        null => null,
        >= 80 => "high",
        >= 50 => "mid",
        _ => "low",
    };

    /// <summary>Points with a thousands separator, e.g. "1 420".</summary>
    public static string Points(int value) => value.ToString("#,0", Czech);

    /// <summary>Czech count of points ("1 bod", "2 body", "5 bodů").</summary>
    public static string PointsCount(int count) => count switch
    {
        1 => "1 bod",
        >= 2 and <= 4 => $"{count} body",
        _ => $"{Points(count)} bodů",
    };

    /// <summary>Column header such as "Po 26. 10.".</summary>
    public static string DayHeader(DateOnly d) =>
        $"{DayAbbreviations[((int)d.DayOfWeek + 6) % 7]} {ShortDate(d)}";

    /// <summary>Week range such as "26. 10. – 1. 11. 2026"; years are repeated only when the week spans two years.</summary>
    public static string WeekLabel(Week w) =>
        w.Monday.Year == w.Sunday.Year
            ? $"{ShortDate(w.Monday)} – {ShortDate(w.Sunday)} {w.Sunday.Year}"
            : $"{ShortDate(w.Monday)} {w.Monday.Year} – {ShortDate(w.Sunday)} {w.Sunday.Year}";

    public static string SlotLabel(EventSlot s) => s switch
    {
        EventSlot.Evening => "Večerní",
        EventSlot.Morning => "Ranní",
        _ => "",
    };

    /// <summary>Short kind of an event for its badge: "Normal", the chaotic slot ("Večerní", "Ranní") or "CS".</summary>
    public static string EventKindLabel(ActivityCategory category, EventSlot slot) => category switch
    {
        ActivityCategory.ChaoticEpic => SlotLabel(slot),
        ActivityCategory.CastleSiege => CategoryLabel(StatCategory.CastleSiege),
        _ => CategoryLabel(StatCategory.Normal),
    };

    /// <summary>CSS suffix of the event kind badge: "normal", "evening", "morning" or "cs".</summary>
    public static string EventKindKey(ActivityCategory category, EventSlot slot) => (category, slot) switch
    {
        (ActivityCategory.ChaoticEpic, EventSlot.Morning) => "morning",
        (ActivityCategory.ChaoticEpic, _) => "evening",
        (ActivityCategory.CastleSiege, _) => "cs",
        _ => "normal",
    };

    /// <summary>CSS suffix of a stats category, same keys as <see cref="EventKindKey"/>: "normal", "evening", "morning" or "cs".</summary>
    public static string StatCategoryKey(StatCategory c) => c switch
    {
        StatCategory.Evening => "evening",
        StatCategory.Morning => "morning",
        StatCategory.CastleSiege => "cs",
        _ => "normal",
    };

    /// <summary>Spawn time of an event in Prague ("19:00"), or null for an event without a fixed time.</summary>
    public static string? EventTime(DateOnly date, TimeOnly? timeUtc) =>
        timeUtc is { } time ? AppTime.ToPragueTime(date, time).ToString("HH:mm", CultureInfo.InvariantCulture) : null;

    public static string CategoryLabel(StatCategory c) => c switch
    {
        StatCategory.Normal => "Normal",
        StatCategory.Evening => "Večerní",
        StatCategory.Morning => "Ranní",
        StatCategory.CastleSiege => "CS",
        _ => c.ToString(),
    };

    /// <summary>Link to a player's detail page, optionally pinned to a week. The id is escaped for use in a path.</summary>
    public static string PlayerUrl(string userId, Week? week = null)
    {
        var url = $"/players/{Uri.EscapeDataString(userId)}";
        return week is null ? url : $"{url}?week={week.Value.Monday:yyyy-MM-dd}";
    }

    /// <summary>Label of a jewel icon: the name, with " ×N" when the player holds more than one piece.</summary>
    public static string JewelTitle(string name, int count) => count > 1 ? $"{name} ×{count}" : name;

    /// <summary>Czech count of pieces ("1 kus", "2 kusy", "5 kusů").</summary>
    public static string Pieces(int count) => count switch
    {
        1 => "1 kus",
        >= 2 and <= 4 => $"{count} kusy",
        _ => $"{count} kusů",
    };

    /// <summary>Czech description of when an activity happens, e.g. "Týdně v sobotu 20:00 UTC".</summary>
    public static string ScheduleLabel(Activity a) => a.ScheduleType switch
    {
        ScheduleType.Flexible => $"Flexibilní, {a.ExpectedPerWeek}× týdně",
        ScheduleType.Weekly => a.TimeUtc is { } time
            ? $"Týdně {DayWithPreposition(a.WeeklyDay)} {time.ToString("HH:mm", CultureInfo.InvariantCulture)} UTC"
            : $"Týdně {DayWithPreposition(a.WeeklyDay)}",
        ScheduleType.Biweekly => $"Ob týden {DayWithPreposition(a.WeeklyDay)}",
        ScheduleType.Slotted => $"Rozpis: {Spawns(a.Spawns.Count)} týdně",
        _ => a.ScheduleType.ToString(),
    };

    /// <summary>Czech message for the outcome of an admin catalog change.</summary>
    public static string CatalogMessage(CatalogChangeResult r) => r switch
    {
        CatalogChangeResult.Ok => "Uloženo.",
        CatalogChangeResult.UnknownActivity => "Aktivita už neexistuje.",
        CatalogChangeResult.InvalidValue => "Neplatná hodnota.",
        CatalogChangeResult.InvalidSpawnTime => "Čas spawnu musí být před 22:00 UTC.",
        CatalogChangeResult.DuplicateSpawn => "Pro tento den a slot už spawn existuje.",
        CatalogChangeResult.AnchorNotOnWeeklyDay => "Datum musí padnout na správný den v týdnu.",
        _ => "Uložení se nepovedlo, zkus to znovu.",
    };

    /// <summary>Czech count of spawns ("1 spawn", "2 spawny", "5 spawnů").</summary>
    private static string Spawns(int count) => count switch
    {
        1 => "1 spawn",
        >= 2 and <= 4 => $"{count} spawny",
        _ => $"{count} spawnů",
    };

    private static string DayWithPreposition(DayOfWeek? day) => day switch
    {
        DayOfWeek.Monday => "v pondělí",
        DayOfWeek.Tuesday => "v úterý",
        DayOfWeek.Wednesday => "ve středu",
        DayOfWeek.Thursday => "ve čtvrtek",
        DayOfWeek.Friday => "v pátek",
        DayOfWeek.Saturday => "v sobotu",
        DayOfWeek.Sunday => "v neděli",
        _ => "",
    };

    private static string ShortDate(DateOnly d) => $"{d.Day}. {d.Month}.";
}
