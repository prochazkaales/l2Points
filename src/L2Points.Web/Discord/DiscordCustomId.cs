using System.Globalization;
using L2Points.Domain.Activities;
using L2Points.Domain.Stats;

namespace L2Points.Web.Discord;

/// <summary>Component <c>custom_id</c> (<c>r:{reportId}:{action}</c>) and select option values of the report panel.</summary>
public static class DiscordCustomId
{
    public const string Date = "date";
    public const string Events = "events";
    public const string Similar = "similar";
    public const string Submit = "submit";
    public const string Cancel = "cancel";

    private const string Prefix = "r";
    private const string DateFormat = "yyyy-MM-dd";

    public static string Format(Guid reportId, string action) => $"{Prefix}:{reportId:N}:{action}";

    public static bool TryParse(string? value, out Guid reportId, out string action)
    {
        reportId = Guid.Empty;
        action = "";

        var parts = value?.Split(':');
        if (parts is not [Prefix, var id, { Length: > 0 } name] || !Guid.TryParseExact(id, "N", out reportId))
            return false;

        action = name;
        return true;
    }

    public static string EventValue(AttendanceEntry e) =>
        string.Create(CultureInfo.InvariantCulture, $"{e.ActivityId}|{e.Date.ToString(DateFormat, CultureInfo.InvariantCulture)}|{(int)e.Slot}");

    public static bool TryParseEventValue(string value, out AttendanceEntry e)
    {
        e = default;

        var parts = value.Split('|');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var activityId)
            || !DateOnly.TryParseExact(parts[1], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
            || !Enum.IsDefined((EventSlot)slot))
            return false;

        e = new AttendanceEntry(activityId, date, (EventSlot)slot);
        return true;
    }
}
