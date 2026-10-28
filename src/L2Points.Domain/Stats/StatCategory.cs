using L2Points.Domain.Activities;

namespace L2Points.Domain.Stats;

/// <summary>Participation percentage category.</summary>
public enum StatCategory
{
    Normal,
    Evening,
    Morning,
    CastleSiege,
}

public static class StatCategories
{
    /// <summary>Maps an activity category and event slot to the participation category it counts in.</summary>
    public static StatCategory Of(ActivityCategory category, EventSlot slot) => category switch
    {
        ActivityCategory.NormalEpic => StatCategory.Normal,
        ActivityCategory.CastleSiege => StatCategory.CastleSiege,
        ActivityCategory.ChaoticEpic => slot switch
        {
            EventSlot.Evening => StatCategory.Evening,
            EventSlot.Morning => StatCategory.Morning,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Chaotic activities require an Evening or Morning slot."),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
