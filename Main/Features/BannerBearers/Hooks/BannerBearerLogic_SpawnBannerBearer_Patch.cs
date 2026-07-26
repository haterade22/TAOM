using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.BannerBearers.Domain;

namespace TAOM.Features.BannerBearers.Hooks;

/// <summary>
/// Guarded reimplementation of the engine's reinforcement banner-bearer spawn (issue #360).
///
/// WHY: <c>BannerBearerLogic.SpawnBannerBearer</c> spawns the troop, then UNCONDITIONALLY reads
/// its ExtraWeaponSlot weapon entity via an unvalidated P/Invoke. The reinforcement path installs
/// the banner through <c>Mission.SpawnTroop</c>'s validating gate (which can silently decline)
/// and wields initial weapons natively (which can DROP the HeldInOffHand+DropOnWeaponChange
/// banner) — either way the native slot-4 record is absent and the read is a 0xC0000005 CTD
/// (siege of Glad Thaw, rca-banner-bearers-reinforcement-av-2026-07-25). The engine caller
/// discards the return value, so declining the banner bookkeeping is always safe.
///
/// Mirrors the engine's five statements, adding: (1) the race/formation-group eligibility gate
/// the engine's reinforcement path lacks, toggle-folded in the service (disabled == vanilla
/// parity — deep-review Flow-4). Known divergence: the deployment gate's agent-level base checks
/// (IsHuman, is CharacterObject) cannot run pre-spawn; the slot-4 guard below is the backstop.
/// (2) a managed slot-4 check BEFORE the native read — an anomaly logs the mechanism (the
/// Iron-Law confirmation channel) instead of crashing; (3) an AV-only catch on the clean-path
/// read (Patch62 precedent). Binding drift reddens BannerBearersBindingTests; unresolved
/// bindings fall back to the original engine method (vanilla behavior, no new failure mode).
/// Cold path — runs only when a reinforcement wave finds GetMissingBannerCount > 0.
/// </summary>
[HarmonyPatch(typeof(BannerBearerLogic), "SpawnBannerBearer")]
[HarmonyPatchCategory("Patch63_BannerBearerSpawnGuard")]
public static class BannerBearerLogic_SpawnBannerBearer_Patch
{
    private static IBannerBearerService? _service;
    private static IModLogger? _logger;

    public static void Initialize(IBannerBearerService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        if (!BannerBearerLogicReflection.TryResolve(out var missing))
            logger.LogWarning(
                "[BannerBearers] Patch63 bindings unresolved (" + string.Join(", ", missing) +
                ") — spawn guard inactive, engine SpawnBannerBearer runs unguarded (issue #360).");
    }

    public static bool Prefix(
        BannerBearerLogic __instance, ref Agent? __result,
        IAgentOriginBase troopOrigin, bool isPlayerSide, Formation formation, bool spawnWithHorse,
        bool isReinforcement, int formationTroopCount, int formationTroopIndex, bool isAlarmed,
        bool wieldInitialWeapons, Vec3? initialPosition, Vec2? initialDirection,
        string? specialActionSetSuffix, bool useTroopClassForSpawn)
    {
        var service = _service;
        if (service == null || !BannerBearerLogicReflection.BindingsOk) return true;

        var mission = __instance.Mission;
        var troop = troopOrigin?.Troop;
        object? controller = null;
        ItemObject? bannerItem = null;
        try
        {
            controller = BannerBearerLogicReflection.GetFormationController(__instance, formation);
            if (controller != null) bannerItem = BannerBearerLogicReflection.GetBannerItem(controller);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[BannerBearers] Patch63 controller lookup failed: {ex.Message} — spawning plain troop");
        }

        // Eligibility gate the engine's reinforcement path lacks (deployment-only in vanilla).
        // Toggle-folded in the service: feature disabled => always allowed (vanilla parity).
        var eligible = troop != null
            && service.IsReinforcementBearerAllowed(troop.Race, troop.DefaultFormationClass);

        if (controller == null || bannerItem == null || !eligible)
        {
            if (controller != null && bannerItem != null)
                _logger?.LogInfo(
                    $"[BannerBearers] Patch63: troop '{troop?.StringId ?? "<null>"}' is not bearer-eligible " +
                    "(excluded race or formation group) — spawned as a plain reinforcement instead.");
            __result = mission.SpawnTroop(troopOrigin, isPlayerSide, hasFormation: true, spawnWithHorse,
                isReinforcement, formationTroopCount, formationTroopIndex, isAlarmed, wieldInitialWeapons,
                initialPosition, initialDirection, specialActionSetSuffix, null,
                formation.FormationIndex, useTroopClassForSpawn);
            return false;
        }

        var agent = mission.SpawnTroop(troopOrigin, isPlayerSide, hasFormation: true, spawnWithHorse,
            isReinforcement, formationTroopCount, formationTroopIndex, isAlarmed, wieldInitialWeapons,
            initialPosition, initialDirection, specialActionSetSuffix, bannerItem,
            (BannerBearerLogicReflection.GetControllerFormation(controller) ?? formation).FormationIndex,
            useTroopClassForSpawn);
        agent.ForceUpdateCachedAndFormationValues(updateOnlyMovement: false, arrangementChangeAllowed: false);
        __result = agent;

        try
        {
            // THE GUARD — the engine reads the native slot-4 entity with no check. Confirm the
            // managed slot actually holds the banner first; an anomaly names the mechanism
            // (issue #360's standing confirmation log) and skips the bookkeeping instead of AVing.
            var slot4 = agent.Equipment[EquipmentIndex.ExtraWeaponSlot];
            var verdict = service.EvaluateSpawnedBearerSlot(
                slot4.IsEmpty ? null : slot4.Item?.StringId, bannerItem.StringId);
            if (verdict != BearerSlotVerdict.Ok)
            {
                var sidearm = agent.Equipment[EquipmentIndex.WeaponItemBeginSlot];
                _logger?.LogWarning(
                    $"[BannerBearers] Patch63 ANOMALY ({verdict}): reinforcement bearer '{troop!.StringId}' " +
                    $"(culture={troop.Culture?.StringId ?? "<null>"}) expected banner '{bannerItem.StringId}' in " +
                    $"ExtraWeaponSlot but holds '{(slot4.IsEmpty ? "<empty>" : slot4.Item?.StringId ?? "<null>")}'; " +
                    $"sidearm='{(sidearm.IsEmpty ? "<empty>" : sidearm.Item?.StringId ?? "<null>")}' " +
                    $"class={(sidearm.IsEmpty ? "<none>" : sidearm.Item?.WeaponComponent?.PrimaryWeapon?.WeaponClass.ToString() ?? "<none>")}. " +
                    "Would have been the slot-4 AV CTD — bearer left unregistered (issue #360).");
                return false;
            }

            GameEntity? bannerEntity = null;
            try
            {
                bannerEntity = GameEntity.CreateFromWeakEntity(
                    agent.GetWeaponEntityFromEquipmentSlot(EquipmentIndex.ExtraWeaponSlot));
            }
            catch (AccessViolationException)
            {
                _logger?.LogWarning(
                    $"[BannerBearers] Patch63 suppressed AccessViolationException reading banner entity for " +
                    $"'{troop!.StringId}' — managed slot said Ok but the native record is gone (issue #360).");
                return false;
            }

            if (bannerEntity == null)
            {
                _logger?.LogWarning(
                    $"[BannerBearers] Patch63: banner entity for '{troop!.StringId}' is invalid — bearer left unregistered.");
                return false;
            }

            BannerBearerLogicReflection.RegisterBannerEntity(__instance, controller, bannerEntity, agent);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[BannerBearers] Patch63 post-spawn bookkeeping failed: {ex.Message}");
        }
        return false;
    }
}
