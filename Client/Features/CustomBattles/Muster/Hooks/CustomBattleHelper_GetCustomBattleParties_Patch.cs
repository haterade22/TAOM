using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Client.Features.CustomBattles.Muster.Hooks;

/// <summary>
/// Fills the two Custom Battle sides exactly as the muster screen set them (MusterArmyBuilder). A battle started from
/// the vanilla screen has no pending muster and keeps vanilla's sides. Applied with Patch19_CustomBattles.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch19_CustomBattles")]
internal static class CustomBattleHelper_GetCustomBattleParties_Patch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(CustomBattleHelper), nameof(CustomBattleHelper.GetCustomBattleParties));

    [HarmonyPostfix]
    private static void Postfix(ref CustomBattleCombatant[] __result) => MusterArmyBuilder.Apply(ref __result);
}
