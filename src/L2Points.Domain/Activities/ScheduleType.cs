namespace L2Points.Domain.Activities;

public enum ScheduleType
{
    /// <summary>No fixed day; tracked against an expected count per week.</summary>
    Flexible,

    /// <summary>Every week on <see cref="Activity.WeeklyDay"/>.</summary>
    Weekly,

    /// <summary>Every second week on <see cref="Activity.WeeklyDay"/>, counted from <see cref="Activity.BiweeklyAnchorDate"/>.</summary>
    Biweekly,

    /// <summary>Several spawns per week, each in an <see cref="EventSlot"/>.</summary>
    Slotted,
}
