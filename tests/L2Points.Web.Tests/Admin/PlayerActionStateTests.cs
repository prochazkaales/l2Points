using L2Points.Infrastructure.Services;
using L2Points.Web.Components.Admin;

namespace L2Points.Web.Tests.Admin;

public class PlayerActionStateTests
{
    private const string Actor = "actor-id";
    private const string Other = "other-id";

    private static AdminPlayer Player(string id, bool isAdmin = false, bool isSuperAdmin = false) =>
        new(id, "login", "Nick", true, new DateOnly(2026, 10, 26), null, isAdmin || isSuperAdmin, isSuperAdmin, false, true);

    [Fact]
    public void Self_cannot_reset_deactivate_delete_or_remove_admin()
    {
        var state = PlayerActionState.For(Player(Actor, isAdmin: true), Actor, actorIsSuperAdmin: true);

        Assert.True(state.IsSelf);
        Assert.Null(state.ProtectedText);
        Assert.False(state.CanResetPassword);
        Assert.False(state.CanDeactivate);
        Assert.False(state.CanDelete);
        Assert.False(state.CanRemoveAdmin);
        Assert.Equal(PlayerActionState.OwnPasswordText, state.ResetPasswordHint);
    }

    [Fact]
    public void Self_who_is_super_admin_is_self_not_protected()
    {
        // The own-account check comes before the super admin protection.
        var state = PlayerActionState.For(Player(Actor, isSuperAdmin: true), Actor, actorIsSuperAdmin: true);

        Assert.True(state.IsSelf);
        Assert.Null(state.ProtectedText);
        Assert.False(state.CanResetPassword);
        Assert.False(state.CanDeactivate);
        Assert.False(state.CanDelete);
        Assert.False(state.CanRemoveAdmin);
        Assert.Equal(PlayerActionState.OwnPasswordText, state.ResetPasswordHint);
    }

    [Fact]
    public void Super_admin_is_protected_from_other_admins()
    {
        var state = PlayerActionState.For(Player(Other, isSuperAdmin: true), Actor, actorIsSuperAdmin: false);

        Assert.Equal(PlayerActionState.SuperAdminProtectedText, state.ProtectedText);
        Assert.False(state.CanResetPassword);
        Assert.False(state.CanDeactivate);
        Assert.False(state.CanDelete);
    }

    [Fact]
    public void Super_admin_target_is_protected_from_any_other_account()
    {
        var state = PlayerActionState.For(Player(Other, isSuperAdmin: true), Actor, actorIsSuperAdmin: true);

        Assert.Equal(PlayerActionState.SuperAdminProtectedText, state.ProtectedText);
        Assert.False(state.CanResetPassword);
        Assert.False(state.CanDeactivate);
        Assert.False(state.CanDelete);
        Assert.False(state.CanRemoveAdmin);
    }

    [Fact]
    public void Other_admin_is_protected_from_a_regular_admin()
    {
        var state = PlayerActionState.For(Player(Other, isAdmin: true), Actor, actorIsSuperAdmin: false);

        Assert.Equal(PlayerActionState.AdminProtectedText, state.ProtectedText);
        Assert.False(state.ShowAdminToggle);
        Assert.False(state.CanResetPassword);
        Assert.False(state.CanDeactivate);
        Assert.False(state.CanDelete);
    }

    [Fact]
    public void Super_admin_manages_other_admins()
    {
        var state = PlayerActionState.For(Player(Other, isAdmin: true), Actor, actorIsSuperAdmin: true);

        Assert.Null(state.ProtectedText);
        Assert.True(state.CanResetPassword);
        Assert.True(state.CanDeactivate);
        Assert.True(state.CanDelete);
        Assert.True(state.CanRemoveAdmin);
        Assert.True(state.ShowAdminToggle);
    }

    [Fact]
    public void Regular_player_is_managed_by_any_admin()
    {
        var state = PlayerActionState.For(Player(Other), Actor, actorIsSuperAdmin: false);

        Assert.False(state.IsSelf);
        Assert.Null(state.ProtectedText);
        Assert.True(state.CanResetPassword);
        Assert.True(state.CanDeactivate);
        Assert.True(state.CanDelete);
        Assert.False(state.ShowAdminToggle);
        Assert.Equal("Reset hesla", state.ResetPasswordHint);
    }

    [Fact]
    public void Protected_hint_is_the_reset_hint()
    {
        var state = PlayerActionState.For(Player(Other, isSuperAdmin: true), Actor, actorIsSuperAdmin: false);

        Assert.Equal(state.ProtectedText, state.ResetPasswordHint);
    }
}
