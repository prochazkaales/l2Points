using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Web.Formatting;

/// <summary>Czech display formatting shared by the attendance pages.</summary>
public static class DisplayFormat
{
    private static readonly string[] DayAbbreviations = ["Po", "Út", "St", "Čt", "Pá", "So", "Ne"];

    /// <summary>Whole percent with a trailing " %", or an en dash when there is nothing to compare.</summary>
    public static string Percent(double? value) =>
        value is null
            ? "–"
            : $"{Math.Round(value.Value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)} %";

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

    public static string CategoryLabel(StatCategory c) => c switch
    {
        StatCategory.Normal => "Normal",
        StatCategory.Evening => "Večerní",
        StatCategory.Morning => "Ranní",
        StatCategory.CastleSiege => "CS",
        _ => c.ToString(),
    };

    private static string ShortDate(DateOnly d) => $"{d.Day}. {d.Month}.";
}
