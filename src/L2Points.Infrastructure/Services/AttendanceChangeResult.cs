namespace L2Points.Infrastructure.Services;

/// <summary>Outcome of an attendance change request.</summary>
public enum AttendanceChangeResult
{
    Ok,
    Forbidden,
    OutsideEditWindow,
    UnknownActivity,
    InvalidSlot,
    WeeklyLimitReached,
}
