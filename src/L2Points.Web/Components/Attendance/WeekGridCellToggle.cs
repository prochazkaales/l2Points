using L2Points.Domain.Activities;

namespace L2Points.Web.Components.Attendance;

/// <summary>A request to set one grid cell to <paramref name="Attended"/>.</summary>
public sealed record WeekGridCellToggle(int ActivityId, DateOnly Date, EventSlot Slot, bool Attended);
