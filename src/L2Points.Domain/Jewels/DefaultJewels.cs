namespace L2Points.Domain.Jewels;

/// <summary>Initial jewel catalog used to seed the database.</summary>
public static class DefaultJewels
{
    /// <summary>Creates fresh, unsaved instances (Id = 0) on every call.</summary>
    public static IReadOnlyList<JewelType> CreateJewelTypes()
    {
        var sort = 0;
        return
        [
            Create("QueenAntRing", "Ring of Queen Ant", ++sort),
            Create("CoreRing", "Ring of Core", ++sort),
            Create("OrfenEarring", "Earring of Orfen", ++sort),
            Create("ZakenEarring", "Earring of Zaken", ++sort),
            Create("FrintezzaNecklace", "Necklace of Frintezza", ++sort),
            Create("BaiumRing", "Ring of Baium", ++sort),
            Create("AntharasEarring", "Earring of Antharas", ++sort),
            Create("ValakasNecklace", "Necklace of Valakas", ++sort),
        ];
    }

    private static JewelType Create(string code, string name, int sortOrder) => new()
    {
        Code = code,
        Name = name,
        ImagePath = $"images/jewels/{code}.png",
        SortOrder = sortOrder,
    };
}
