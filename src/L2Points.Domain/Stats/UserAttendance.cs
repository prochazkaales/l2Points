namespace L2Points.Domain.Stats;

/// <summary>An attendance entry tagged with the player it belongs to.</summary>
public readonly record struct UserAttendance(string UserId, AttendanceEntry Entry);
