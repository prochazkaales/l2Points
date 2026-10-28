namespace L2Points.Domain.Attendances;

/// <summary>Size of the window a player may edit back in time; see <see cref="AttendanceRules"/>.</summary>
public static class EditWindow
{
    public const int DaysBack = 6;
}
