namespace L2Points.Domain.Activities;

/// <summary>Points override for one slot of an activity.</summary>
public class ActivitySlotPoints
{
    public int ActivityId { get; set; }

    public EventSlot Slot { get; set; }

    public int Points { get; set; }
}
