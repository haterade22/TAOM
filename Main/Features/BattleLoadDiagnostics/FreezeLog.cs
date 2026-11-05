using System;
using System.IO;
using System.Text;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// The [Freeze] line formats. A log contract: FreezeLogTests pins each literal and
/// tools/tests/test_triage_battle_load.py parses the same strings. An unmeasured value is omitted,
/// never written as zero.
/// </summary>
internal static class FreezeLog
{
    public const string Tag = "[Freeze]";

    public const string NotSampledMissionFrame =
        "    <stack not sampled: a mission frame is in flight and [MissionStall] samples this thread>";
    public const string NoManagedFrames = "    <no managed frames: the main thread is in native code>";
    public const string ThreadUnknown = "    <thread unknown>";

    public static string NotSampledHeapMoving(int gen0Delta, int windowSeconds)
        => $"    <stack not sampled: the managed heap is moving ({gen0Delta} gen0 collections in {windowSeconds}s), so the game is busy in managed code such as a save or a load>";

    public static string CaptureFailed(Exception ex)
        => $"    <capture failed: {ex.GetType().Name}: {ex.Message}>";

    public static string FormatSample(int silentSeconds, int sampleNumber, string context, int shadersInFlight,
        bool rglAvailable, int rglCompiles, int? lastCompileAgoSeconds, string memTokens, string statusLine)
    {
        var sb = new StringBuilder(256);
        sb.Append(Tag).Append(" main thread silent ").Append(silentSeconds).Append("s sample#").Append(sampleNumber)
            .Append(" context=").Append(context);
        if (shadersInFlight >= 0) sb.Append(" shaders=").Append(shadersInFlight);
        AppendRgl(sb, rglAvailable, rglCompiles);
        if (rglAvailable && lastCompileAgoSeconds.HasValue) sb.Append(" lastCompileAgoS=").Append(lastCompileAgoSeconds.Value);
        sb.Append(' ').Append(memTokens);
        sb.Append(" last ").Append(statusLine);
        return sb.ToString();
    }

    public static string FormatRecovered(int silentSeconds, bool rglAvailable, int rglCompiles)
    {
        var sb = new StringBuilder(80);
        sb.Append(Tag).Append(" main thread recovered after about ").Append(silentSeconds).Append('s');
        AppendRgl(sb, rglAvailable, rglCompiles);
        return sb.ToString();
    }

    public static string FormatStarted(string? rglPath)
    {
        return rglPath == null
            ? Tag + " main-thread freeze sampler started; engine log not found, compile counts unavailable"
            : Tag + " main-thread freeze sampler started; engine log " + Path.GetFileName(rglPath);
    }

    private static void AppendRgl(StringBuilder sb, bool rglAvailable, int rglCompiles)
    {
        if (rglAvailable) sb.Append(" rglCompiles=").Append(rglCompiles);
        else sb.Append(" rgl=unavailable");
    }
}
