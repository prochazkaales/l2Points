using L2Points.Domain.Jewels;

namespace L2Points.Web.Tests.Jewels;

public class JewelImageTests
{
    [Fact]
    public void Every_default_jewel_has_an_image_file()
    {
        var root = FindRepoRoot();
        var imageDir = Path.Combine(root, "src", "L2Points.Web", "wwwroot", "images", "jewels");

        // File.Exists is case-insensitive on Windows; compare names ordinally, as the Linux container does.
        var fileNames = Directory.GetFiles(imageDir).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        foreach (var type in DefaultJewels.CreateJewelTypes())
        {
            Assert.Equal("images/jewels", Path.GetDirectoryName(type.ImagePath)?.Replace('\\', '/'));
            var fileName = Path.GetFileName(type.ImagePath);
            Assert.True(fileNames.Contains(fileName),
                $"Missing icon for {type.Code}: {fileName} (case-sensitive) in {imageDir}");
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
