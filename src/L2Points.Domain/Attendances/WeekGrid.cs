using L2Points.Domain.Activities;
using L2Points.Domain.Time;

namespace L2Points.Domain.Attendances;

/// <summary>
/// One checkbox of the weekly grid. <paramref name="IsScheduled"/> is false for an attendance without a matching schedule slot.
/// <paramref name="IsTracked"/> is false before the global tracking start: such a day never counts, even when checked.
/// </summary>
public sealed record WeekGridCell(
    DateOnly Date, EventSlot Slot, TimeOnly? TimeUtc, bool IsScheduled, bool IsChecked, bool IsEditable, bool IsTracked = true);

/// <summary>Grid row of one activity; <paramref name="Cells"/> are ordered by date, then slot.</summary>
public sealed record WeekGridRow(Activity Activity, IReadOnlyList<WeekGridCell> Cells);

public sealed record WeekGrid(Week Week, IReadOnlyList<WeekGridRow> Rows);
