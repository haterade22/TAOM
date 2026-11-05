namespace TAOM.Adapters;

/// <summary>
/// ButterLib's Distance Matrix subsystem, reached by name: its type is internal to the version-specific
/// <c>Bannerlord.ButterLib.Implementation.*.dll</c> (#740, docs/features/butter-lib-distance-matrix.md).
/// </summary>
public interface IButterLibDistanceMatrixAdapter
{
    /// <summary>
    /// Switches the subsystem off for this process. Null when it is off now, with <paramref name="wasAlreadyOff"/> true
    /// when it was off before the call (ButterLib's saved options); otherwise why it is still on.
    /// </summary>
    string? TryDisable(out bool wasAlreadyOff);
}
