namespace L2Points.Infrastructure.Services;

public enum PlayerAdminStatus
{
    Ok,
    UnknownPlayer,
    NickTaken,
    LoginTaken,
    Invalid,
    CannotChangeSelf,
    ActorNotAllowed,
    RequiresSuperAdmin,
    SuperAdminProtected,
    AdminProtected,
    AdminCannotBeReader,
    ReaderCannotBeAdmin,
    NotReader,
}

/// <param name="Status">Outcome of the operation.</param>
/// <param name="Password">Generated password; only set after creating a player or resetting a password.</param>
/// <param name="Message">Description of the problem for <see cref="PlayerAdminStatus.Invalid"/>.</param>
public sealed record PlayerAdminResult(PlayerAdminStatus Status, string? Password = null, string? Message = null);
