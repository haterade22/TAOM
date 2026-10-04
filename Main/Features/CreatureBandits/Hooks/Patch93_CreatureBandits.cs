using HarmonyLib;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// Spawns a creature troop as a riderless creature (#692). The battle spawn loop calls <c>SpawnTroop</c> for each
/// troop origin and ignores the agent it returns (v1.5.3 <c>MissionBattleSideSpawnContext.cs:375-389</c>), so a
/// creature can stand in for the troop. Returning false also skips <c>SpawnTroopWithAgentBuildData</c>, which reads
/// <c>agent.Character.IsHero</c> and wields weapons the creature does not have (the June crash #4). Anything the
/// rules decline, or a spawn that fails, falls through to vanilla: the troop's husk on its mount. So does a banner
/// bearer's spawn: <c>BannerBearerLogic.SpawnBannerBearer</c> is the one caller that uses the agent it gets back, to hand
/// it the banner (<c>BannerBearerLogic.cs:760-769</c>). A brood has no hero captain, so no formation banner, and that
/// path never reaches a creature today.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnTroop))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditSpawn
{
    [HarmonyPrefix]
    public static bool Prefix(Mission __instance, ref Agent __result, IAgentOriginBase troopOrigin, bool isPlayerSide,
        bool hasFormation, bool isReinforcement, int formationTroopCount, int formationTroopIndex, bool isAlarmed,
        Vec3? initialPosition, Vec2? initialDirection, ItemObject? bannerItem, FormationClass formationIndex,
        bool useTroopClassForSpawn)
    {
        if (bannerItem != null)
            return true;
        BasicCharacterObject? troop = troopOrigin?.Troop;
        Equipment? equipment = troop?.Equipment;
        EquipmentElement creature = equipment?[EquipmentIndex.Horse] ?? default;
        bool hasCreatureItem = creature.Item?.HasHorseComponent == true;
        if (!CreatureBanditRules.ShouldSpawnAsCreature(troop?.StringId, isPlayerSide, __instance.IsFieldBattle, hasCreatureItem))
        {
            if (CreatureBanditRules.IsCreatureTroop(troop?.StringId))
                CreatureBanditDiag.NoteDeclined(troop!.StringId, isPlayerSide, __instance.IsFieldBattle, hasCreatureItem,
                    __instance.CurrentTime);
            return true;
        }

        AgentBuildData buildData = __instance.GetAgentBuildDataToSpawnTroop(troopOrigin, isPlayerSide, hasFormation,
            spawnWithHorse: true, isReinforcement, formationTroopCount, formationTroopIndex, initialPosition,
            initialDirection, null, formationIndex, useTroopClassForSpawn);
        Agent? agent = CreatureBanditSpawner.TrySpawn(__instance, troopOrigin!, buildData, creature,
            equipment![EquipmentIndex.HorseHarness], isAlarmed);
        if (agent == null)
            return true;

        __result = agent;
        return false;
    }
}

/// <summary>
/// A riderless AI mount panics on any hit of 1 or more (v1.5.3 <c>CommonAIComponent.cs:198-201</c>), then retreats and
/// fades out at the map edge. A creature bandit stands and fights. <c>OnHit</c> does nothing else (its base is empty,
/// <c>AgentComponent.cs:65-67</c>), so skipping it for a creature loses nothing.
/// </summary>
[HarmonyPatch(typeof(CommonAIComponent), nameof(CommonAIComponent.OnHit))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditNoPanic
{
    [HarmonyPrefix]
    public static bool Prefix(Agent ___Agent)
    {
        if (!CreatureBanditAgents.Is(___Agent)) return true;
        CreatureBanditDiag.NoteOnHitPanicBlocked(___Agent);
        return false;
    }
}

/// <summary>
/// The rout gate for a riderless agent that is retreating, running away or wandering (<c>Mission.cs:5323-5334</c>):
/// a creature bandit stands and fights, and a routed creature route A left Mountable fades out with no attacker, which
/// SandBox never counts removed (CreatureBanditMissionBehavior's backstop). Postfix rather than the
/// engine's <c>CanAgentRout_AdditionalCondition</c> event: that is a multicast <c>Func&lt;Agent, bool&gt;</c> whose
/// invocation returns only the last subscriber's answer, so it cannot be shared with other mods. Runs on the
/// TWParallel workers (CommonAIComponent.OnTickParallel); the check only reads, the note only counts.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.CanAgentRout))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditNoRout
{
    [HarmonyPostfix]
    public static void Postfix(Agent agent, ref bool __result)
    {
        if (!__result || !CreatureBanditAgents.Is(agent)) return;
        __result = false;
        CreatureBanditDiag.NoteRoutBlocked(agent);
    }
}

/// <summary>
/// A mount-built agent has no native weapon state unless CanWieldWeapon is in its creation flags (native
/// <c>FUN_1805b89c0</c>); its wield-index pointers are then the bare block offset, not a live block. The June spider
/// NRE'd reading them and AV'd writing them, and guarded this closed set of three
/// (<c>rca-spider-troop-2026-06-04.md:30, 62</c>). Route A arms the flag at creation, so every creature it unmounts has
/// the state (the 2026-09-28 spike logged no guard answer); the guards cover a creature route A skipped. A creature
/// holds no weapon, so None and 0 are the true answers. These run for every agent, often on worker threads: the check is read-only; a mount
/// is ruled out by its managed <c>Character</c> field and a humanoid by one flags read, before the troop id is read,
/// and the three targets are on
/// <c>PatchShieldPolicy.ExcludedTargetMethods</c>. CreatureBanditDiag counts every answer and logs the first per kind
/// per mission with its caller stack, so the log says whether the guards are ever needed.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditPrimaryWieldGuard
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref EquipmentIndex __result)
    {
        if (!CreatureBanditAgents.Is(__instance)) return true;
        CreatureBanditDiag.NoteGuard(CreatureGuardKind.PrimaryWield, __instance);
        __result = EquipmentIndex.None;
        return false;
    }
}

[HarmonyPatch(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditOffhandWieldGuard
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref EquipmentIndex __result)
    {
        if (!CreatureBanditAgents.Is(__instance)) return true;
        CreatureBanditDiag.NoteGuard(CreatureGuardKind.OffhandWield, __instance);
        __result = EquipmentIndex.None;
        return false;
    }
}

[HarmonyPatch(typeof(Agent), nameof(Agent.GetMissileRange))]
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditMissileRangeGuard
{
    [HarmonyPrefix]
    public static bool Prefix(Agent __instance, ref float __result)
    {
        if (!CreatureBanditAgents.Is(__instance)) return true;
        CreatureBanditDiag.NoteGuard(CreatureGuardKind.MissileRange, __instance);
        __result = 0f;
        return false;
    }
}
