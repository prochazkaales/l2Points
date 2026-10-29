using System.Globalization;

namespace L2Points.Web.Formatting;

/// <summary>Parsing and formatting of times typed by the admin ("19:30", "3:00").</summary>
public static class TimeInput
{
    private static readonly string[] Formats = ["H:mm", "HH:mm"];

    /// <summary>Parses <c>H:mm</c> or <c>HH:mm</c>; surrounding whitespace is ignored.</summary>
    public static bool TryParse(string? text, out TimeOnly time)
    {
        time = default;
        return !string.IsNullOrWhiteSpace(text)
            && TimeOnly.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>Two-digit hours and minutes, e.g. "03:00".</summary>
    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
