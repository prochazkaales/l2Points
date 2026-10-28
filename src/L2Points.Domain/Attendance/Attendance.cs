using L2Points.Domain.Activities;

namespace L2Points.Domain.Attendance;

/// <summary>One user's recorded participation in an activity on a date (and slot, for slotted activities).</summary>
public class Attendance
{
    public long Id { get; set; }

    public string UserId { get; set; } = "";

    public int ActivityId { get; set; }

    public DateOnly Date { get; set; }

    public EventSlot Slot { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = "";
}
