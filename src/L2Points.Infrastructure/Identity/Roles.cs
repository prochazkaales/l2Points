namespace L2Points.Infrastructure.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Player = "Player";

    /// <summary>The single config admin (Admin__UserName); only this account grants or revokes Admin.</summary>
    public const string SuperAdmin = "SuperAdmin";
}
