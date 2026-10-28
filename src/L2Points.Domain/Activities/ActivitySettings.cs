namespace L2Points.Domain.Activities;

/// <summary>Admin-editable values of an activity. Slot points are null when there is no override.</summary>
public sealed record ActivitySettings(
    int Points,
    int? EveningPoints,
    int? MorningPoints,
    int? ExpectedPerWeek,
    bool IsActive);
