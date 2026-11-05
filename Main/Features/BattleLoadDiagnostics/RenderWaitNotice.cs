namespace TAOM.Features.BattleLoadDiagnostics;

public enum RenderWaitNoticeKind { Hidden, Compiling, NoProgress }

public readonly struct RenderWaitNoticeDecision
{
    public static readonly RenderWaitNoticeDecision Hidden = new RenderWaitNoticeDecision(RenderWaitNoticeKind.Hidden, 0, 0);

    public RenderWaitNoticeDecision(RenderWaitNoticeKind kind, int compiles, int seconds)
    {
        Kind = kind;
        Compiles = compiles;
        Seconds = seconds;
    }

    public RenderWaitNoticeKind Kind { get; }
    public int Compiles { get; }
    public int Seconds { get; }
}

/// <summary>The window over a battle's render wait; the watchdog publishes to it from its timer thread.</summary>
public interface IRenderWaitNotice
{
    void Show(RenderWaitNoticeDecision decision);
}

/// <summary>
/// What the notice may claim. It says "compiling" only while compile_shader lines are arriving in the
/// engine log, and never takes the main thread's shader count as progress: that count freezes with
/// the thread (#612, player logs of 2026-11-01). Pure: no engine or window types.
/// </summary>
public static class RenderWaitNoticeDecider
{
    public const double ShowAfterSilentSeconds = 5d;
    public const double CompilingWithinSeconds = 20d;
    public const double NoProgressAfterSeconds = 60d;

    public static RenderWaitNoticeDecision Decide(bool inRenderWait, bool walkActive, double silentSeconds,
        bool rglAvailable, int compiles, double? secondsSinceLastCompile)
    {
        if (!inRenderWait || walkActive || !rglAvailable || silentSeconds < ShowAfterSilentSeconds)
            return RenderWaitNoticeDecision.Hidden;
        if (secondsSinceLastCompile is double s && s < CompilingWithinSeconds)
            return new RenderWaitNoticeDecision(RenderWaitNoticeKind.Compiling, compiles, (int)silentSeconds);
        double quiet = secondsSinceLastCompile ?? silentSeconds;
        return silentSeconds >= NoProgressAfterSeconds && quiet >= NoProgressAfterSeconds
            ? new RenderWaitNoticeDecision(RenderWaitNoticeKind.NoProgress, compiles, (int)quiet)
            : RenderWaitNoticeDecision.Hidden;
    }
}
