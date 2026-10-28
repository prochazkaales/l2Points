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

    private static string ShortDate(DateOnly d) => $"{d.Day}. {d.Month}.";
}
