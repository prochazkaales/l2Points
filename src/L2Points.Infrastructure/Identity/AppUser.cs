using Microsoft.AspNetCore.Identity;

namespace L2Points.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public const int NickMaxLength = 32;

    public string Nick { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateOnly ActiveFrom { get; set; }

    public DateOnly? DeactivatedAt { get; set; }

    /// <summary>
    /// Whether the account appears in rankings, stats and the attendance overview. Players are always true;
    /// only a reader may be false (hidden from all statistics). No EF model default on purpose
    /// (HasDefaultValue would turn an inserted false into true); the migration's DB default only
    /// backfills existing rows.
    /// </summary>
    public bool ShowInStats { get; set; } = true;

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; set; }
}
