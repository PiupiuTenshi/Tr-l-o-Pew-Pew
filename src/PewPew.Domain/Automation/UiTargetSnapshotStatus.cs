namespace PewPew.Domain.Automation;

/// <summary>
/// Status of a <see cref="UiTargetSnapshot"/> in the domain lifecycle.
/// </summary>
public enum UiTargetSnapshotStatus
{
    Active = 1,
    Expired = 2,
    Invalidated = 3
}
