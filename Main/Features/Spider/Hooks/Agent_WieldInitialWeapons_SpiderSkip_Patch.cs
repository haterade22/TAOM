using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Thin prefix (Patch45_SpiderTroopSpawn) that makes <see cref="Agent.WieldInitialWeapons"/> a no-op
/// for the detached spider agent, and runs vanilla unchanged for every other agent.
///
/// Why it is needed: <see cref="SpiderDetachedAgentSpawner"/> builds the spider via the engine's
/// FromHorseObj (free-mount) path, which never runs the weapon-equip step — so the agent's native
/// wield-index pointers (<c>_primaryWieldedItemIndexPointer</c>) are left uninitialized. Immediately
/// after the Mission.SpawnAgent prefix returns the agent, vanilla
/// <c>MissionBattleSideSpawnContext.SpawnTroops</c> calls <c>agent.WieldInitialWeapons()</c> on EVERY
/// spawned agent; that method's first action is <c>GetPrimaryWieldedItemIndex()</c>, which unsafe-derefs
/// the uninitialized pointer → NullReferenceException and the battle never finishes loading
/// (confirmed via the SpawnTroops → WieldInitialWeapons:4571 → GetPrimaryWieldedItemIndex:2690 stack).
/// The spider has no wieldable weapons (it bites via its Monster's CustomAttack), so wielding is a
/// semantic no-op for it: skip the call. Co-exists with <see cref="Mission_SpawnAgent_SpiderSwap_Patch"/>
/// in the same category. See docs/reviews/rca-spider-troop-2026-06-04.md.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.WieldInitialWeapons))]
[HarmonyPatchCategory("Patch45_SpiderTroopSpawn")]
public static class Agent_WieldInitialWeapons_SpiderSkip_Patch
{
    // Returns false (skip vanilla) only for the detached spider agent — whose Character is the spider
    // troop anchor (taom_spider_creature) even under the warg render-diagnostic. Any other agent returns
    // true so vanilla weapon-wielding is untouched.
    // Guard: when Enabled=false the goblin cavalry RIDER has StringId "taom_spider_creature" and must
    // wield its weapons normally via the horse-slot mount path.
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance)
    {
        if (!SpiderConfig.Enabled) return true;
        return __instance?.Character?.StringId != SpiderConfig.SpiderCharacterId;
    }
}
