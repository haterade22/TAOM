using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

// Guards the three managed Agent methods that dereference a FromHorseObj agent's NATIVE wield/missile
// pointers, returning safe values for the detached spider so the engine never touches its GARBAGE native
// state.
//
// Why: the spider is built via the free-mount (FromHorseObj) path, whose native wield pointers
// (_primaryWieldedItemIndexPointer = 0xee0, _offHandWieldedItemIndexPointer = 0xee4) are never
// initialized. They cannot be READ (AgentHelper.GetPrimaryWieldedItemIndex derefs them -> NRE) nor
// WRITTEN (RemoveEquippedWeapon -> native WeaponEquipped -> AccessViolation, both confirmed in-game,
// 2026-06-04/05). A real riderless mount never hits these because it never joins a formation or fights;
// the spider does both. So every managed method that walks those pointers must be short-circuited:
//   - Agent.GetMissileRange()            -> 0f   (formation team-AI MaximumMissileRange query)
//   - Agent.GetPrimaryWieldedItemIndex() -> None (combat/wield + melee-target selection)
//   - Agent.GetOffhandWieldedItemIndex() -> None (combat/wield)
// The spider has no wielded weapon (it bites via its Monster's CustomAttack), so None / 0 is the CORRECT
// answer, not just crash-avoidance. Always active (not gated by EnableFormationMembership): the garbage
// pointers are unsafe whether the spider is detached or formationed. Detection keys on the spider troop
// anchor (taom_spider_creature) so it covers both the warg render stand-in and the real spider, and is
// stateless (thread-safe — these run on combat worker threads too). Applied in the Patch45 category.
// See docs/reviews/rca-spider-troop-2026-06-04.md.
internal static class SpiderWieldGuard
{
    internal static bool IsSpider(Agent agent)
        => agent?.Character?.StringId == SpiderConfig.SpiderCharacterId;
}

/// <summary>Spider guard: GetMissileRange -> 0 (skips the native read the formation team-AI makes).</summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.GetMissileRange))]
[HarmonyPatchCategory("Patch45_SpiderTroopSpawn")]
public static class Agent_GetMissileRange_SpiderGuard_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref float __result)
    {
        if (!SpiderWieldGuard.IsSpider(__instance))
            return true;
        __result = 0f;
        return false;
    }
}

/// <summary>Spider guard: GetPrimaryWieldedItemIndex -> None (skips the native garbage-pointer deref).</summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex))]
[HarmonyPatchCategory("Patch45_SpiderTroopSpawn")]
public static class Agent_GetPrimaryWieldedItemIndex_SpiderGuard_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref EquipmentIndex __result)
    {
        if (!SpiderWieldGuard.IsSpider(__instance))
            return true;
        __result = EquipmentIndex.None;
        return false;
    }
}

/// <summary>Spider guard: GetOffhandWieldedItemIndex -> None (skips the native garbage-pointer deref).</summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex))]
[HarmonyPatchCategory("Patch45_SpiderTroopSpawn")]
public static class Agent_GetOffhandWieldedItemIndex_SpiderGuard_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref EquipmentIndex __result)
    {
        if (!SpiderWieldGuard.IsSpider(__instance))
            return true;
        __result = EquipmentIndex.None;
        return false;
    }
}
