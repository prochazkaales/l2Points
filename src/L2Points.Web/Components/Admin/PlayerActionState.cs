using L2Points.Infrastructure.Services;

namespace L2Points.Web.Components.Admin;

/// <summary>
/// What the acting admin may do with one player in the player list. The server enforces the same rules in
/// <c>PlayerAdminService</c>; this only decides which actions the UI offers or disables.
/// </summary>
/// <param name="IsSelf">The row is the acting admin's own account.</param>
/// <param name="ProtectedText">Why the target is untouchable (super admin, or another admin for a regular admin); null otherwise.</param>
/// <param name="CanResetPassword">Reset password is allowed.</param>
/// <param name="ResetPasswordHint">Tooltip and reason of the reset action: the protection or own-account reason, else the action name.</param>
/// <param name="CanDeactivate">Deactivating an active player is allowed (activating an inactive one is always allowed).</param>
/// <param name="CanDelete">Deleting the player is allowed.</param>
/// <param name="ShowAdminToggle">The actor may grant or remove the Admin role (super admin only).</param>
/// <param name="CanRemoveAdmin">Removing the Admin role from this player is allowed.</param>
/// <param name="CanSwitchToReader">Switching a player to a reader is allowed (not for readers, the own account or admins).</param>
/// <param name="SwitchToReaderHint">Why switching to a reader is not possible (own account, admin); null otherwise.</param>
public sealed record PlayerActionState(
    bool IsSelf,
    string? ProtectedText,
    bool CanResetPassword,
    string ResetPasswordHint,
    bool CanDeactivate,
    bool CanDelete,
    bool ShowAdminToggle,
    bool CanRemoveAdmin,
    bool CanSwitchToReader,
    string? SwitchToReaderHint)
{
    public const string SuperAdminProtectedText = "Super admina nelze měnit.";
    public const string AdminProtectedText = "Jiného admina může měnit jen super admin.";
    public const string AdminReaderText = "Admina nejde přepnout na čtenáře.";
    public const string OwnPasswordText = "Vlastní heslo změníš v profilu.";

    private const string ResetPasswordText = "Reset hesla";

    public static PlayerActionState For(AdminPlayer player, string? actorId, bool actorIsSuperAdmin)
    {
        var isSelf = player.UserId == actorId;

        // Nobody touches the super admin and only the super admin touches other admins.
        var protectedText = isSelf ? null
            : player.IsSuperAdmin ? SuperAdminProtectedText
            : player.IsAdmin && !actorIsSuperAdmin ? AdminProtectedText
            : null;
        var isProtected = protectedText is not null;
        var canManage = !isSelf && !isProtected;

        // Readers are never admins; switching a reader back to a player is always allowed, so only the way to a reader is gated.
        var canSwitchToReader = !player.IsReader && !isSelf && !player.IsAdmin;
        var switchHint = player.IsReader ? null
            : isSelf ? "Vlastní účet"
            : player.IsAdmin ? AdminReaderText
            : null;

        var resetHint = isSelf ? OwnPasswordText : protectedText ?? ResetPasswordText;

        return new PlayerActionState(
            IsSelf: isSelf,
            ProtectedText: protectedText,
            CanResetPassword: canManage,
            ResetPasswordHint: resetHint,
            CanDeactivate: canManage,
            CanDelete: canManage,
            ShowAdminToggle: actorIsSuperAdmin,
            CanRemoveAdmin: canManage,
            CanSwitchToReader: canSwitchToReader,
            SwitchToReaderHint: switchHint);
    }
}
