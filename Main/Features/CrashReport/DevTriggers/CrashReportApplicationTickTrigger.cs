using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CrashReport.DevTriggers;

// Application-tick side of the dev trigger. A Postfix on Module.OnApplicationTick
// checks the MCM toggle and throws on the next tick if set. Lets QA exercise the
// CrashReport pipeline without entering a mission (e.g., on the main menu).
//
// Lives in its own patch category so SubModule can register it conditionally
// alongside Patch37_CrashReport when CrashReportSettings.EnableCrashCapture is true.
//
// Settings caching (Codex review #46 LOW-01 fix): see comment in
// CrashReportDevTriggerMissionBehavior. Cache the MCM Instance once it's non-null —
// MCM returns the same singleton on every call and a per-tick provider-scan adds
// up across 60-200 Hz application ticks.
[HarmonyPatch(typeof(Module), "OnApplicationTick")]
[HarmonyPatchCategory("Patch37_CrashReport")]
public static class CrashReportApplicationTickTrigger
{
    private static CrashReportSettings? _cachedSettings;

    [HarmonyPriority(900)]   // higher than the Finalizer priority so we throw INTO the Finalizer
    private static void Postfix()
    {
        var settings = _cachedSettings ??= CrashReportSettings.Instance;
        if (settings == null) return;
        if (!settings.EnableCrashCapture) return; // master toggle gate
        if (!settings.ThrowOnNextApplicationTick) return;
        settings.ThrowOnNextApplicationTick = false;
        throw new TaomDevTriggerException("TAOM CrashReport dev trigger: app-tick throw fired by MCM toggle.");
    }
}
