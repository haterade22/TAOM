using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Throwers on an advance (docs/features/marshals-word.md, build-spec 9.4): a postfix on the private <c>MovementOrder.GetPositionAux</c> moves the Advance
/// point of a company on a thrower run to its stand point. Vanilla runs first; after each answer the postfix moves, it forgets vanilla's sticky melee
/// point (<see cref="MarshalEngagePoint"/>), so the first answer it leaves alone, at the release, is computed from that moment. It runs on the AI
/// thread, the main thread in fast-forward and rarely a worker: one volatile table, no lock, no allocation, no log line. A throw latches it off.
/// </summary>
[HarmonyPatch(typeof(MovementOrder), "GetPositionAux", typeof(Formation), typeof(WorldPosition.WorldPositionEnforcedCache))]
[HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
public static class MarshalThrowerPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref MovementOrder __instance, Formation f, ref WorldPosition __result)
    {
        try
        {
            if (MarshalHooks.ThrowerFault != null)
                return;
            var table = MarshalHooks.Throwers;
            if (table == null || __instance.OrderEnum != MovementOrder.MovementOrderEnum.Advance)
                return;
            if (Stand(table, f, ref __result))
                MarshalEngagePoint.Forget(ref __instance);
        }
        catch (Exception ex)
        {
            MarshalHooks.LatchThrowerFault(ex);
        }
    }

    internal static bool Stand(MarshalThrowerTable table, Formation f, ref WorldPosition result)
    {
        if (Mission.Current?.Mode == MissionMode.Deployment)
            return false;
        var index = table.IndexOf(f);
        if (index < 0 || !result.IsValid)
            return false;
        result.SetVec2MT(new Vec2(table.X[index], table.Y[index]));
        MarshalThrowerCost.AddMoved();
        return true;
    }
}
