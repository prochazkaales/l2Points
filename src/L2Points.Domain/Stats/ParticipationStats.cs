namespace L2Points.Domain.Stats;

public sealed record ParticipationStats(IReadOnlyDictionary<StatCategory, CategoryStat> ByCategory)
{
    public static ParticipationStats Empty { get; } = new(new Dictionary<StatCategory, CategoryStat>());

    public CategoryStat Total => ByCategory.Values.Aggregate(default(CategoryStat), (sum, stat) => sum.Add(stat));

    /// <summary>Stat of a category; a missing category is (0, 0).</summary>
    public CategoryStat this[StatCategory category] =>
        ByCategory.TryGetValue(category, out var stat) ? stat : default;
}
