namespace L2Points.Infrastructure.Services;

/// <summary>A player as shown in the admin list.</summary>
public sealed record AdminPlayer(
    string UserId,
    string Login,
    string Nick,
    bool IsActive,
    DateOnly? DeactivatedAt,
    bool IsAdmin,
    bool IsSuperAdmin);
