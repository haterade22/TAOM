using System;
using System.Diagnostics;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.ScreenSystem;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// The Patch101 frame boundary (the <c>MapState.OnTick</c> prefix) and the engine reads behind it: the campaign,
/// the loading window and the top screen at each boundary, the party count and the raw MCM top-N for the session
/// hooks. Everything after the reads is <see cref="MapSessionHooks.Step"/>, which takes plain values and so runs in
/// tests without the engine. Main thread only.
/// </summary>
public static class MapFrameBoundary
{
    /// <summary>The Patch101 frame boundary (MapState.OnTick prefix): the engine reads, then Step.</summary>
    public static void OnFrameBoundary()
    {
        var profiler = MapFrameProfilerHooks.Profiler;
        if (profiler == null) return;
        Campaign? campaign = null;
        try
        {
            campaign = Campaign.Current;
            if (campaign == null) return;
            if (MapSessionHooks.IsSettled(profiler, campaign)) return;
            var skip = LoadingWindow.IsLoadingWindowActive ? MapSkip.Loading
                : ScreenManager.TopScreen is MapScreen ? MapSkip.None : MapSkip.NotTop;
            MapSessionHooks.Step(campaign, Stopwatch.GetTimestamp(), AllocationCounter.ReadOrZero(), skip);
        }
        catch (Exception ex) { MapSessionHooks.Fault(profiler, "frame boundary", ex, campaign); }
    }

    // parties=na on the line when unreadable.
    internal static int ReadPartyCount() { try { return Campaign.Current?.MobileParties?.Count ?? -1; } catch { return -1; } }

    // The raw MCM value of the shared top-N, to report a fallback the validating provider applied silently.
    internal static int? ReadRawTopN() { try { return BattleLoadDiagnosticsSettings.Instance?.TickProfilerTopN; } catch { return null; } }
}
