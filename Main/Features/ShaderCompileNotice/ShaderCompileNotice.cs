using System;
using System.Threading;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// While the engine rebuilds its runtime shader cache before the main menu (about 1,340 shaders after
/// a module-list change, every core busy, the loading symbol frozen for a minute or more), shows a
/// small window over the game with the progress and runs the game at BelowNormal priority so the rest
/// of the computer stays usable. Started from <c>SubModule.OnSubModuleLoad</c>, about six seconds
/// before the first compile; stopped at the first <c>OnBeforeInitialModuleScreenSetAsRoot</c>.
/// </summary>
/// <remarks>
/// The engine's main thread is blocked while it compiles, so nothing can be drawn inside the game:
/// this runs a Windows message loop on a thread of its own and reads the progress from the engine's
/// own log, one <c>compile_shader</c> line per shader in <c>rgl_log_&lt;pid&gt;.txt</c>. It never
/// touches the engine. Idea from yotthani's VanillaTuning (docs/features/shader-compile-notice.md).
/// The window and its message loop are WinForms, so they are TAOM.Client's (ADR-013): this class keeps the start
/// and stop flags and the STA thread, and hands the thread to <see cref="IShaderCompileNoticeWindowHost"/>. It stays
/// in TAOM.dll under its own name because VanillaTuning may look it up by full type name.
/// </remarks>
public static class ShaderCompileNotice
{
    private static int _started;
    private static volatile bool _stopRequested;

    public static void Start(ShaderCompileNoticeTexts texts, IModLogger logger, IShaderCompileNoticeWindowHost host)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        var thread = new Thread(() => Run(texts, logger, host)) { IsBackground = true, Name = "TAOM shader compile notice" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>Idempotent; the main menu calls it every time it is shown.</summary>
    public static void Stop() => _stopRequested = true;

    private static void Run(ShaderCompileNoticeTexts texts, IModLogger logger, IShaderCompileNoticeWindowHost host)
    {
        try
        {
            host.Run(texts, ShaderCompileNoticeMode.Startup, logger, () => _stopRequested);
        }
        catch (Exception e)
        {
            logger.LogWarning($"[ShaderNotice] stopped after an error: {e.GetType().Name}: {e.Message}");
        }
    }
}
