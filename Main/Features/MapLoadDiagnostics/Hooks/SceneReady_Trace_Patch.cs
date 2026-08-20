using System;
using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Reports which half of the map's scene-ready gate is failing.
///
/// <para>
/// <c>MapScreen.HandleIfBlockerStatesDisabled()</c> runs every frame and lifts the campaign map's
/// loading screen only when
/// <c>SceneView.ReadyToRender() &amp;&amp; SceneView.CheckSceneReadyToRender()</c> has held for
/// three consecutive frames. The heartbeat showed <c>loadingWindow=True</c> indefinitely while the
/// map itself rendered at 69 fps, so that conjunction is false forever, and the whole question is
/// which of the two returns false.
/// </para>
///
/// <para>
/// Both are per-frame native queries, so the trace is throttled to once every five seconds and
/// additionally on any CHANGE of value, which is what would mark the moment the scene either
/// becomes ready or stops being ready.
/// </para>
///
/// <para>
/// Own category: these bind <c>TaleWorlds.Engine</c> internals and a drift here must not take the
/// working heartbeat with it.
/// </para>
/// </summary>
[HarmonyPatch(typeof(SceneView), nameof(SceneView.CheckSceneReadyToRender))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_SceneReady")]
public static class SceneView_CheckSceneReadyToRender_Patch
{
    private static bool _lastValue;
    private static bool _seen;
    private static DateTime _lastLogUtc = DateTime.MinValue;

    [HarmonyPostfix]
    public static void Postfix(bool __result)
        => SceneReadyTraceThrottle.Report("CheckSceneReadyToRender", __result,
                                          ref _lastValue, ref _seen, ref _lastLogUtc);
}

[HarmonyPatch(typeof(SceneView), nameof(SceneView.ReadyToRender))]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics_SceneReady")]
public static class SceneView_ReadyToRender_Patch
{
    private static bool _lastValue;
    private static bool _seen;
    private static DateTime _lastLogUtc = DateTime.MinValue;

    [HarmonyPostfix]
    public static void Postfix(bool __result)
        => SceneReadyTraceThrottle.Report("ReadyToRender", __result,
                                          ref _lastValue, ref _seen, ref _lastLogUtc);
}

/// <summary>Shared throttle so a per-frame native query does not flood the log it is diagnosing.</summary>
internal static class SceneReadyTraceThrottle
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    internal static void Report(string name, bool value, ref bool last, ref bool seen,
                                ref DateTime lastLogUtc)
    {
        try
        {
            var now = DateTime.UtcNow;
            // Always log a transition: that is the event we are hunting.
            var changed = !seen || value != last;
            if (!changed && now - lastLogUtc < Interval) return;

            seen = true;
            last = value;
            lastLogUtc = now;
            MapLoadTracer.Trace($"SCENE-READY {name}={value}{(changed ? " (CHANGED)" : "")}");
        }
        catch { /* diagnostic only */ }
    }
}
