using L2Points.Domain.Attendances;

namespace L2Points.Web.Components.Attendance;

/// <summary>How a week grid cell behaves and is drawn.</summary>
public enum GridCellState
{
    /// <summary>The player can toggle the cell.</summary>
    Editable,

    /// <summary>The cell cannot be changed (locked day, outside the schedule, weekly limit); drawn dimmed.</summary>
    Blocked,

    /// <summary>The whole grid is view-only (read-only page or loading); drawn like a normal cell, without interaction.</summary>
    ReadOnly,
}

public static class GridCellStates
{
    /// <summary>
    /// An unchecked cell outside the schedule cannot be added (the server rejects it); only removal is allowed.
    /// The same holds once a flexible boss has its weekly count checked.
    /// </summary>
    public static GridCellState For(bool gridReadOnly, WeekGridCell cell, bool limitReached)
    {
        if (gridReadOnly)
        {
            return GridCellState.ReadOnly;
        }

        var blocked = !cell.IsEditable
            || (!cell.IsScheduled && !cell.IsChecked)
            || (limitReached && !cell.IsChecked);
        return blocked ? GridCellState.Blocked : GridCellState.Editable;
    }
}
