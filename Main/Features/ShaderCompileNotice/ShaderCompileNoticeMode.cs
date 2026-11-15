using System;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// How a run of the shader notice window behaves: <see cref="Startup"/> for the rebuild before the main menu,
/// <see cref="Battle"/> for a battle load (docs/features/battle-shader-notice.md). The window and the log reading
/// are the same; these decisions differ.
/// </summary>
public sealed class ShaderCompileNoticeMode
{
    /// <summary>The startup notice exactly as before the battle mode existed.</summary>
    public static readonly ShaderCompileNoticeMode Startup = new(
        "ShaderNotice", readLogFromStart: true, showAfterDelay: null, lowerPriority: true, useRememberedTotal: true,
        giveUpAfter: TimeSpan.FromMinutes(30), closeWhenSilentFor: null);

    /// <summary>
    /// A battle load: counts only the compiles that come after it starts, shows after one second so a queue the
    /// engine clears at once never flashes a window, leaves the process priority alone (the compile is what the
    /// player waits for), and closes after 30 s with no new compile, a net for a mission that ends without another
    /// frame. The longest wait seen on the dev PC was 110 s.
    /// </summary>
    public static readonly ShaderCompileNoticeMode Battle = new(
        "BattleShaderNotice", readLogFromStart: false, showAfterDelay: TimeSpan.FromSeconds(1), lowerPriority: false,
        useRememberedTotal: false, giveUpAfter: TimeSpan.FromMinutes(15), closeWhenSilentFor: TimeSpan.FromSeconds(30));

    private readonly bool _readLogFromStart;
    private readonly TimeSpan? _showAfterDelay;
    private readonly TimeSpan? _closeWhenSilentFor;

    private ShaderCompileNoticeMode(
        string logTag, bool readLogFromStart, TimeSpan? showAfterDelay, bool lowerPriority, bool useRememberedTotal,
        TimeSpan giveUpAfter, TimeSpan? closeWhenSilentFor)
    {
        LogTag = logTag;
        _readLogFromStart = readLogFromStart;
        _showAfterDelay = showAfterDelay;
        LowerPriority = lowerPriority;
        UseRememberedTotal = useRememberedTotal;
        GiveUpAfter = giveUpAfter;
        _closeWhenSilentFor = closeWhenSilentFor;
    }

    /// <summary>The tag of the run's lines in taom_debug.log.</summary>
    public string LogTag { get; }

    /// <summary>Run at BelowNormal priority while the window is up.</summary>
    public bool LowerPriority { get; }

    /// <summary>Read and write the remembered total for "of about N".</summary>
    public bool UseRememberedTotal { get; }

    public TimeSpan GiveUpAfter { get; }

    /// <summary>Where reading the engine log starts, given its length now (-1 when it does not exist yet).</summary>
    public long StartPosition(long logLength) => _readLogFromStart || logLength < 0 ? 0L : logLength;

    public bool ShouldShow(ShaderCompileProgress progress, TimeSpan running) =>
        _showAfterDelay.HasValue ? running >= _showAfterDelay.Value : progress.ShouldShow;

    public bool ShouldCloseIdle(bool shown, TimeSpan sinceLastCompile) =>
        shown && _closeWhenSilentFor.HasValue && sinceLastCompile >= _closeWhenSilentFor.Value;
}
