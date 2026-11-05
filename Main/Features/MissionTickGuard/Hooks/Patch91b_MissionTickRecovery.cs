using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionTickGuard.Hooks;

// Patch91b (port decision D17). Installed v1.4.8 (taom-src): public void Mission.OnTick(float, float,
// bool, bool) (Mission.cs:3636); public void Mission.TickAgentsAndTeamsAsync(float) (:1825, one caller,
// :3770); private bool Mission.tickCompleted (:1040, written only at :3613 and :3740). The rule and
// why it is safe: TickHandshakeRecovery and docs/features/mission-tick-guard.md.

/// <summary>Arms Patch91b after its category applied. Never throws into the game-init batch.</summary>
public static class MissionTickRecoveryWiring
{
    public const string Category = "Patch91b_MissionTickRecovery";
    private const string Tag = "[TickRecovery]";
    private static IModLogger? _logger;

    internal static AccessTools.FieldRef<Mission, bool>? TickCompleted { get; private set; }

    public static void VerifyAndArm(IModLogger logger)
    {
        _logger = logger;
        try
        {
            AccessTools.FieldRef<Mission, bool>? field = null;
            string fieldNote = "resolved";
            try { field = AccessTools.FieldRefAccess<Mission, bool>("tickCompleted"); }
            catch (Exception ex) { fieldNote = $"{ex.GetType().Name}: {ex.Message}"; }

            bool launchMark = HasPatch(LaunchTarget(), typeof(Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch), needPrefix: false);
            bool recovery = HasPatch(OnTickTarget(), typeof(Mission_OnTick_TickRecovery_Patch), needPrefix: true);

            TickCompleted = field;
            if (TickHandshakeRecovery.Instance.Arm(field != null, launchMark, recovery))
                logger.LogInfo($"{Tag} armed: a throw out of Mission.OnTick that launched no agent tick no longer leaves the next frame waiting");
            else
                logger.LogWarning($"{Tag} off (field {fieldNote}, launch mark {launchMark}, OnTick prefix and finalizer {recovery}): a throw out of a mission tick can still freeze the game");
        }
        catch (Exception ex)
        {
            TickHandshakeRecovery.Instance.Arm(false, false, false);
            try { logger.LogWarning($"{Tag} off: arming threw {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never throw into the game-init batch */ }
        }
    }

    internal static void LogRestore(Exception? exception, long count)
    {
        try { _logger?.LogError($"{Tag} Mission.OnTick threw {exception?.GetType().Name}: {exception?.Message} after it cleared tickCompleted and before the agent tick started; restored the flag so the next frame does not wait forever. This frame's agent tick is skipped (restore #{count})."); }
        catch { /* a log failure must never replace the mission's exception */ }
    }

    private static MethodBase? OnTickTarget() => AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });
    private static MethodBase? LaunchTarget() => AccessTools.Method(typeof(Mission), nameof(Mission.TickAgentsAndTeamsAsync), new[] { typeof(float) });

    private static bool HasPatch(MethodBase? target, Type patchClass, bool needPrefix)
    {
        if (target == null) return false;
        var info = Harmony.GetPatchInfo(target);
        if (info == null) return false;
        bool finalizer = info.Finalizers.Any(p => p.PatchMethod?.DeclaringType == patchClass);
        bool prefix = !needPrefix || info.Prefixes.Any(p => p.PatchMethod?.DeclaringType == patchClass);
        return finalizer && prefix;
    }
}

// The launch mark is a finalizer, not a prefix: PatchShield's rescue path strips prefixes,
// postfixes and transpilers but never finalizers, and a finalizer runs even if the launch threw.
// Keep this class ABOVE the OnTick class: categories apply in assembly order, and this small target
// can be inlined into an OnTick rebuilt before it was patched, which would lose the mark.
[HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsAsync), new[] { typeof(float) })]
[HarmonyPatchCategory(MissionTickRecoveryWiring.Category)]
public static class Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch
{
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer() => TickHandshakeRecovery.Instance.MarkLaunch();
}

// Prefix and finalizer share Harmony's __state, so they must stay in this one class.
[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory(MissionTickRecoveryWiring.Category)]
public static class Mission_OnTick_TickRecovery_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(Mission __instance, out bool __state)
    {
        var field = MissionTickRecoveryWiring.TickCompleted;
        __state = field != null && TickHandshakeRecovery.Instance.Enter(field(__instance));
    }

    // Void, so Harmony rethrows the original exception and Patch37 reports it with its stack.
    // Priority.First, so it sees the exception before PatchShield's finalizer can swallow it.
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Mission __instance, Exception? __exception, bool __state)
    {
        var field = MissionTickRecoveryWiring.TickCompleted;
        if (field == null) return;
        var decision = TickHandshakeRecovery.Instance.Exit(__state, __exception != null, field(__instance));
        if (decision == RecoveryDecision.None) return;
        field(__instance) = true;
        if (decision == RecoveryDecision.RestoreAndLog)
            MissionTickRecoveryWiring.LogRestore(__exception, TickHandshakeRecovery.Instance.RestoreCount);
    }
}
