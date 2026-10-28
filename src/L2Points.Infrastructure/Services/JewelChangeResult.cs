namespace L2Points.Infrastructure.Services;

/// <summary>Outcome of assigning or removing a jewel piece.</summary>
public enum JewelChangeResult
{
    Ok,
    UnknownPlayer,
    UnknownJewelType,
    NotHeld,
}
