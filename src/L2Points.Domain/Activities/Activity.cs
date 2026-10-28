namespace L2Points.Domain.Activities;

public class Activity
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public ActivityCategory Category { get; set; }

    public ScheduleType ScheduleType { get; set; }

    public int Points { get; set; }

    public int? ExpectedPerWeek { get; set; }

    public DayOfWeek? WeeklyDay { get; set; }

    public DateOnly? BiweeklyAnchorDate { get; set; }

    public TimeOnly? TimeUtc { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ActivitySpawn> Spawns { get; set; } = [];

    public List<ActivitySlotPoints> SlotPoints { get; set; } = [];
}
