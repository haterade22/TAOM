namespace TAOM.Adapters;

/// <summary>There is no map view to attach (a dedicated server, or a client where TAOM.Client did not load).</summary>
public sealed class NullCheatMenuMapViewAdapter : ICheatMenuMapViewAdapter
{
    public void EnsureAttached()
    {
    }
}
