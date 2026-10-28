using L2Points.Domain.Jewels;

namespace L2Points.Web.Tests.Jewels;

public class JewelImageTests
{
    [Fact]
    public void Every_default_jewel_has_an_image_file()
    {
        var root = FindRepoRoot();
        var wwwroot = Path.Combine(root, "src", "L2Points.Web", "wwwroot");

        foreach (var type in DefaultJewels.CreateJewelTypes())
        {
            var path = Path.Combine(wwwroot, type.ImagePath);
            Assert.True(File.Exists(path), $"Missing icon for {type.Code}: {path}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "L2Points.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (L2Points.slnx) not found.");
    }
}
