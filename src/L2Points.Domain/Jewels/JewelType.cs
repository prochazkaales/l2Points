namespace L2Points.Domain.Jewels;

public class JewelType
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 64;
    public const int ImagePathMaxLength = 128;

    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Path of the jewel icon relative to wwwroot.</summary>
    public string ImagePath { get; set; } = "";

    public int SortOrder { get; set; }
}
