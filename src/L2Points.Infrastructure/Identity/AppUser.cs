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
    /// Whether the player appears in rankings, stats and the attendance overview. Readers are never
    /// counted (always false for them); players are always true. No database default on purpose:
    /// it would turn an inserted false back into true.
    /// </summary>
    public bool ShowInStats { get; set; } = true;

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; set; }
}
