// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Features.MissionPerf;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// The six call sites Patch103 swaps inside <c>Mission.AfterStart</c>, each a public virtual instance call that occurs
/// exactly once in the installed v1.5.4 body (<c>MissionStartGuardBindingTests</c> reads the real IL). Every swap
/// pairs the call with the helper of the same name on <see cref="MissionStartGuardCalls"/>. The spawn-path selector,
/// the deployment plan and the weather model, which the same method also calls, are engine internals and stay unwrapped.
/// </summary>
internal static class MissionStartGuardSwaps
{
    /// <summary>How many sites the transpiler swapped on its last run; 0 until it ran, and 0 when it soft-failed.</summary>
    internal static int LastSwapped { get; set; }

    internal static IReadOnlyList<CallSwap> Build() => new[]
    {
        Swap(typeof(MBSubModuleBase), nameof(MBSubModuleBase.OnBeforeMissionBehaviorInitialize), typeof(Mission)),
        Swap(typeof(MissionBehavior), nameof(MissionBehavior.OnBehaviorInitialize)),
        Swap(typeof(MBSubModuleBase), nameof(MBSubModuleBase.OnMissionBehaviorInitialize), typeof(Mission)),
        Swap(typeof(MissionBehavior), nameof(MissionBehavior.EarlyStart)),
        Swap(typeof(MissionBehavior), nameof(MissionBehavior.AfterStart)),
        Swap(typeof(MissionObject), nameof(MissionObject.AfterMissionStart)),
    };

    private static CallSwap Swap(Type owner, string name, params Type[] arguments) => new CallSwap(
        AccessTools.Method(owner, name, arguments),
        AccessTools.Method(typeof(MissionStartGuardCalls), name));
}
