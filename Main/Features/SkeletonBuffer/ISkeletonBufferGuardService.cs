// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
namespace TAOM.Features.SkeletonBuffer;

/// <summary>Installs the skeleton buffer guard once and says where the watch should read.</summary>
public interface ISkeletonBufferGuardService
{
    /// <summary>
    /// Resolves the engine sites in the loaded <c>TaleWorlds.Native.dll</c> and, when every check passes, installs the
    /// guard. Runs once; later calls do nothing. Never throws. Global steps end in one
    /// <c>[SkeletonBuffer] guard OFF: &lt;reason&gt;</c> line; each pool then installs or reports on its own
    /// (<c>guard ON pool N</c> or <c>guard OFF pool N</c>), plus one line when the second pool cannot be watched.
    /// Main thread, at the first main menu.
    /// </summary>
    void Install();

    /// <summary>The watch's target, or null when the global was not found or the feature stood aside.</summary>
    SkeletonBufferTarget? Target { get; }
}
