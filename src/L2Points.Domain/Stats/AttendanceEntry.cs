using L2Points.Domain.Activities;

namespace L2Points.Domain.Stats;

/// <summary>Persistence-free view of one attendance record, used by the calculators.</summary>
public readonly record struct AttendanceEntry(int ActivityId, DateOnly Date, EventSlot Slot);
