namespace L2Points.Domain.Activities;

public class ActivitySpawn
{
    public int Id { get; set; }

    public int ActivityId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public EventSlot Slot { get; set; }

    public TimeOnly TimeUtc { get; set; }
}
