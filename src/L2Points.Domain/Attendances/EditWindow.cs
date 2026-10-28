namespace L2Points.Domain.Attendances;

/// <summary>Rule limiting which dates a player may edit for their own attendance. Admins are not limited.</summary>
public static class EditWindow
{
    public const int DaysBack = 6;

    /// <summary>True when <paramref name="date"/> is within [today - <see cref="DaysBack"/>, today].</summary>
    public static bool CanPlayerEdit(DateOnly date, DateOnly today) =>
        date <= today && date >= today.AddDays(-DaysBack);
}
