using L2Points.Domain.Attendances;
using L2Points.Domain.Activities;
using L2Points.Web.Components.Attendance;

namespace L2Points.Web.Tests.Attendance;

public class GridCellStatesTests
{
    private static WeekGridCell Cell(bool scheduled, bool isChecked, bool editable) =>
        new(new DateOnly(2026, 10, 29), EventSlot.None, null, scheduled, isChecked, editable);

    [Fact]
    public void ReadOnlyGrid_IsReadOnly_EvenWhenCellIsLocked() =>
        Assert.Equal(GridCellState.ReadOnly, GridCellStates.For(true, Cell(scheduled: true, isChecked: true, editable: false), limitReached: false));

    [Fact]
    public void EditableScheduledCell_IsEditable() =>
        Assert.Equal(GridCellState.Editable, GridCellStates.For(false, Cell(scheduled: true, isChecked: false, editable: true), false));

    [Fact]
    public void LockedDay_IsBlocked() =>
        Assert.Equal(GridCellState.Blocked, GridCellStates.For(false, Cell(scheduled: true, isChecked: true, editable: false), false));

    [Fact]
    public void UncheckedOutsideSchedule_IsBlocked() =>
        Assert.Equal(GridCellState.Blocked, GridCellStates.For(false, Cell(scheduled: false, isChecked: false, editable: true), false));

    [Fact]
    public void CheckedOutsideSchedule_IsEditable_SoItCanBeRemoved() =>
        Assert.Equal(GridCellState.Editable, GridCellStates.For(false, Cell(scheduled: false, isChecked: true, editable: true), false));

    [Fact]
    public void UncheckedWhenLimitReached_IsBlocked() =>
        Assert.Equal(GridCellState.Blocked, GridCellStates.For(false, Cell(scheduled: true, isChecked: false, editable: true), limitReached: true));

    [Fact]
    public void CheckedWhenLimitReached_IsEditable() =>
        Assert.Equal(GridCellState.Editable, GridCellStates.For(false, Cell(scheduled: true, isChecked: true, editable: true), limitReached: true));
}
