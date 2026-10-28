using L2Points.Domain.Jewels;

namespace L2Points.Domain.Tests.Jewels;

public class DefaultJewelsTests
{
    [Fact]
    public void Catalog_has_eight_types_in_order()
    {
        var types = DefaultJewels.CreateJewelTypes();

        Assert.Equal(
            [
                "QueenAntRing", "CoreRing", "OrfenEarring", "ZakenEarring",
                "FrintezzaNecklace", "BaiumRing", "AntharasEarring", "ValakasNecklace",
            ],
            types.Select(t => t.Code));
        Assert.Equal(Enumerable.Range(1, 8), types.Select(t => t.SortOrder));
        Assert.Equal("Ring of Core", types.Single(t => t.Code == "CoreRing").Name);
    }

    [Fact]
    public void Codes_are_unique_and_short()
    {
        var codes = DefaultJewels.CreateJewelTypes().Select(t => t.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.True(code.Length <= JewelType.CodeMaxLength));
    }

    [Fact]
    public void Image_path_follows_code()
    {
        Assert.All(DefaultJewels.CreateJewelTypes(), t => Assert.Equal($"images/jewels/{t.Code}.png", t.ImagePath));
    }

    [Fact]
    public void Creates_fresh_instances()
    {
        var first = DefaultJewels.CreateJewelTypes();
        var second = DefaultJewels.CreateJewelTypes();

        Assert.All(first.Zip(second), pair => Assert.NotSame(pair.First, pair.Second));
        Assert.All(first.Concat(second), t => Assert.Equal(0, t.Id));
    }
}
