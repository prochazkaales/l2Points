using Microsoft.AspNetCore.Identity;

namespace L2Points.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public const int NickMaxLength = 32;

    public string Nick { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateOnly ActiveFrom { get; set; }

    public DateOnly? DeactivatedAt { get; set; }

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; set; }
}
