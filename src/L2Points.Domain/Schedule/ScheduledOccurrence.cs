using L2Points.Domain.Activities;

namespace L2Points.Domain.Schedule;

public readonly record struct ScheduledOccurrence(Activity Activity, DateOnly Date, EventSlot Slot, TimeOnly? TimeUtc);
