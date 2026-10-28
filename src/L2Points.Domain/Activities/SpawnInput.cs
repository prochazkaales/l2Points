namespace L2Points.Domain.Activities;

/// <summary>One requested spawn of a slotted activity (time in UTC).</summary>
public readonly record struct SpawnInput(DayOfWeek Day, EventSlot Slot, TimeOnly TimeUtc);
