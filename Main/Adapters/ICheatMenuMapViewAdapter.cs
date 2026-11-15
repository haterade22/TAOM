namespace TAOM.Adapters;

/// <summary>
/// Attaches the cheat menu's map view (it reads the map key) to the campaign map screen when it is missing (ADR-013). The
/// view is client-only, so core registers NullCheatMenuMapViewAdapter and the client composition replaces it.
/// </summary>
public interface ICheatMenuMapViewAdapter
{
    /// <summary>Called every campaign tick; does nothing without a map screen or when the view is already attached.</summary>
    void EnsureAttached();
}
