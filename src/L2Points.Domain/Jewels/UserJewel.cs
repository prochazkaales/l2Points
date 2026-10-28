namespace L2Points.Domain.Jewels;

/// <summary>One jewel piece owned by a player; a player can own several pieces of the same type.</summary>
public class UserJewel
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";

    public int JewelTypeId { get; set; }
}
