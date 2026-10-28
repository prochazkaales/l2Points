namespace L2Points.Domain.Activities;

/// <summary>Outcome of validating or applying an admin catalog change.</summary>
public enum CatalogChangeResult
{
    Ok,
    UnknownActivity,
    InvalidValue,
    InvalidSpawnTime,
    DuplicateSpawn,
    AnchorNotOnWeeklyDay,
}
