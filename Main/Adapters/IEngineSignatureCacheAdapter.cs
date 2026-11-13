namespace TAOM.Adapters;

/// <summary>
/// The disk half of the engine signature address cache (ADR-007): one small text file of per-user state in the player's
/// Documents (Documents\Mount and Blade II Bannerlord\TAOM), never in the module folder, so no release package can carry it.
/// Documents may be redirected to a synced folder, so the file can travel between a player's PCs; the service's key and the
/// byte check at each address make a foreign file a miss. Holds no decisions: the service decides what the text says and
/// how far to trust it.
/// </summary>
public interface IEngineSignatureCacheAdapter
{
    /// <summary>
    /// The cache file's text, at most <paramref name="maxChars"/> characters of it (a longer file comes back cut to that
    /// length, so the caller can tell it is too long without the whole file being read), or null when there is no file yet.
    /// Throws when the file exists and cannot be read.
    /// </summary>
    string? ReadCache(int maxChars);

    /// <summary>Replaces the cache file's text. Throws when the file cannot be written.</summary>
    void WriteCache(string text);

    /// <summary>
    /// The size and last-write time of the file the named module was loaded from in this process, as one text (for example
    /// <c>1234|638000000000000000</c>), or null when the module is not loaded or the loader cannot give its path. Throws when the module is loaded but its file
    /// cannot be read.
    /// </summary>
    string? ModuleFileStamp(string moduleFileName);
}
