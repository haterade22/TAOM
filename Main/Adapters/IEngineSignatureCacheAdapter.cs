namespace TAOM.Adapters;

/// <summary>
/// The disk half of the engine signature address cache (ADR-007): one small text file of per-machine state in the player's
/// Documents (Documents\Mount and Blade II Bannerlord\TAOM), never in the module folder, so no release package can carry it.
/// Holds no decisions: the service decides what the text says and whether to trust it.
/// </summary>
public interface IEngineSignatureCacheAdapter
{
    /// <summary>The cache file's text, or null when there is no file yet. Throws when the file exists and cannot be read.</summary>
    string? ReadCache();

    /// <summary>Replaces the cache file's text. Throws when the file cannot be written.</summary>
    void WriteCache(string text);

    /// <summary>
    /// The size and last-write time of the file a module loaded in this process came from, as one text (for example
    /// <c>1234|638000000000000000</c>), or null when the module or its file cannot be found.
    /// </summary>
    string? ModuleFileStamp(string moduleFileName);
}
