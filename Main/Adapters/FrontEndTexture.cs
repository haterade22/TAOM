namespace TAOM.Adapters;

/// <summary>
/// An engine texture loaded from a module PNG for the themed front end (#704), opaque to the services
/// that decide when it is loaded and released. Only <see cref="FrontEndResourceAdapter"/> reads
/// <see cref="Native"/>.
/// </summary>
public sealed class FrontEndTexture
{
    public FrontEndTexture(object native, int memorySizeBytes)
    {
        Native = native;
        MemorySizeBytes = memorySizeBytes;
    }

    public object Native { get; }

    /// <summary>The engine's own figure for the texture's memory, for the load and release log lines.</summary>
    public int MemorySizeBytes { get; }
}
