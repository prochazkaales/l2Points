namespace L2Points.Domain.Stats;

/// <summary>Counted attendances versus expected occurrences for one category.</summary>
public readonly record struct CategoryStat(int Attended, int Expected)
{
    /// <summary>Participation in percent (0-100), or null when nothing was expected.</summary>
    public double? Percent => Expected == 0 ? null : (double)Attended / Expected * 100.0;

    public CategoryStat Add(CategoryStat other) => new(Attended + other.Attended, Expected + other.Expected);
}
