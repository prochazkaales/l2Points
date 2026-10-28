using L2Points.Domain.Activities;

namespace L2Points.Domain.Stats;

/// <summary>One concrete event (activity + day + slot) of a week together with the players who attended it.</summary>
public sealed record WeekEvent(
    Activity Activity,
    DateOnly Date,
    EventSlot Slot,
    TimeOnly? TimeUtc,
    IReadOnlyList<PlayerInfo> Attendees);
