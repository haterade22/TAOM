using System.Diagnostics;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Times <c>MovementOrder.CreateNewOrderWorldPositionMT</c> (build-spec 9.4), which the formation asks every tick: the published companies in one bucket and every
/// other Advance in a second, so the cost of the stand is read beside vanilla's. The prefix reads one volatile table and times nothing while it is null or the order is not an Advance, since only an Advance is recorded.
/// The whole call is timed: vanilla's method, the thrower postfix, the caller's navmesh lookup and any wait for the position lock. No try, no log line.
/// </summary>
[HarmonyPatch(typeof(MovementOrder), nameof(MovementOrder.CreateNewOrderWorldPositionMT))]
[HarmonyPatchCategory("Patch_MissionTime_MarshalsWord")]
public static class MarshalThrowerTimer
{
    [HarmonyPrefix]
    public static void Prefix(ref MovementOrder __instance, out long __state)
    {
        __state = MarshalHooks.Throwers != null && __instance.OrderEnum == MovementOrder.MovementOrderEnum.Advance ? Stopwatch.GetTimestamp() : 0L;
    }

    [HarmonyPostfix]
    public static void Postfix(ref MovementOrder __instance, Formation f, WorldPosition __result, long __state)
    {
        if (__state == 0L)
            return;
        Record(Stopwatch.GetTimestamp() - __state, __instance.OrderEnum == MovementOrder.MovementOrderEnum.Advance, f, __result);
    }

    internal static void Record(long ticks, bool isAdvance, Formation f, WorldPosition result)
    {
        if (!isAdvance)
            return;
        var table = MarshalHooks.Throwers;
        var index = table == null ? -1 : table.IndexOf(f);
        if (index >= 0)
            MarshalThrowerCost.AddPublished(ticks, result.X != table!.X[index] || result.Y != table.Y[index]);
        else
            MarshalThrowerCost.AddOther(ticks);
    }
}
