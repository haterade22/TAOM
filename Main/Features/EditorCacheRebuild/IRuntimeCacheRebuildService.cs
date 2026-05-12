namespace TAOM.Features.EditorCacheRebuild;

/// <summary>
/// Singleplayer-side cache rebuild trigger. Invoked from MCM. Spawns a background
/// task that constructs a fresh <c>SandBoxNavigationCache</c> against the active
/// campaign's MapSceneWrapper and runs the same parallel builder used by the
/// editor patch path. Result is serialized to the live distance-cache .bin so the
/// next save-load picks it up.
/// </summary>
public interface IRuntimeCacheRebuildService
{
    /// <summary>True iff a build is currently in progress on the background thread.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Fire-and-forget. Returns immediately. The actual rebuild runs on a background
    /// thread; progress + completion are logged + surfaced via in-game popups.
    /// </summary>
    /// <returns>
    /// True if the trigger was accepted (background task started).
    /// False if rejected (no active campaign, or a build is already running).
    /// </returns>
    bool Trigger();
}
