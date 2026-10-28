namespace L2Points.Infrastructure.Identity;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? Nick { get; set; }
}
