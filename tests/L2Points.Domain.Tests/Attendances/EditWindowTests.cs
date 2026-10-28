using L2Points.Domain.Attendances;

namespace L2Points.Domain.Tests.Attendances;

public class EditWindowTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    [Fact]
    public void CanPlayerEdit_today_and_six_days_back()
    {
        Assert.True(EditWindow.CanPlayerEdit(new DateOnly(2026, 10, 28), Today));
        Assert.True(EditWindow.CanPlayerEdit(new DateOnly(2026, 10, 22), Today));
    }

    [Fact]
    public void CanPlayerEdit_rejects_older_and_future()
    {
        Assert.False(EditWindow.CanPlayerEdit(new DateOnly(2026, 10, 21), Today));
        Assert.False(EditWindow.CanPlayerEdit(new DateOnly(2026, 10, 29), Today));
    }
}
