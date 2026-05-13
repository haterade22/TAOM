using System;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards.Hooks;

public static class GuardsCampaignBehavior_TakeGuardAgentData_Patch
{
    private static ISettlementGuardService _service;
    private static MethodInfo _prepareMethod;
    // Phase 9b #157 — one-shot logging guard so a future TaleWorlds API drift surfaces ONE
    // diagnostic line instead of either crashing the spawn or silently falling back forever.
    // We do not want per-spawn log spam (TakeGuardAgentDataFromGarrisonTroopList fires for every
    // settlement enter).
    private static bool _exceptionLogged;

    public static void Initialize(ISettlementGuardService service)
    {
        _service = service;
        _prepareMethod = AccessTools.Method(
            typeof(GuardsCampaignBehavior),
            "PrepareGuardAgentDataFromGarrison",
            new[] { typeof(CharacterObject), typeof(bool), typeof(bool) });
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(GuardsCampaignBehavior),
            "TakeGuardAgentDataFromGarrisonTroopList",
            new[] { typeof(CultureObject), typeof(bool), typeof(bool) });
    }

    [HarmonyPrefix]
    public static bool Prefix(
        CultureObject culture,
        bool overrideWeaponWithSpear,
        bool unarmed,
        ref AgentData __result)
    {
        if (_service == null) return true;

        try
        {
            var settlement = PlayerEncounter.LocationEncounter?.Settlement;
            if (settlement == null) return true;

            string spawnPointTag = GetSpawnPointTag(overrideWeaponWithSpear, unarmed);

            var context = new SettlementGuardContext(
                settlement.StringId,
                settlement.OwnerClan?.StringId,
                culture?.StringId);

            var troopId = _service.ResolveGuardTroopId(context, spawnPointTag);
            if (troopId == null) return true;

            var character = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
            if (character == null) return true;

            if (_prepareMethod != null)
            {
                __result = (AgentData)_prepareMethod.Invoke(null, new object[] { character, overrideWeaponWithSpear, unarmed });
                return false;
            }
        }
        catch (Exception ex)
        {
            // Phase 9b #157 — verified via ilspycmd that v1.3.15 still has
            // `private static AgentData PrepareGuardAgentDataFromGarrison(CharacterObject, bool, bool)`
            // so the `Invoke(null, ...)` static call shape is correct as of writing. Any exception
            // here is therefore unexpected — log it ONCE per process so a future TaleWorlds API
            // drift surfaces a diagnostic instead of falling back silently forever. We then
            // degrade gracefully by returning true to let vanilla run.
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try
                {
                    IoC.Resolve<IModLogger>()?.LogError(
                        $"[SettlementGuards] PrepareGuardAgentDataFromGarrison invoke failed: " +
                        $"{ex.GetType().Name}: {ex.Message}. Falling back to vanilla guards. " +
                        $"This log fires once per process — verify v1.3.15 signature via ilspycmd " +
                        $"on SandBox.dll if behavior is unexpected.");
                }
                catch { /* logger resolution failure must not surface to vanilla path */ }
            }
        }

        return true;
    }

    private static string GetSpawnPointTag(bool overrideWeaponWithSpear, bool unarmed)
    {
        // Note: vanilla calls this method with (spear=true, unarmed=false) for BOTH
        // sp_guard_castle and sp_guard_with_spear. We can't distinguish them here
        // since the spawn point tag isn't passed to TakeGuardAgentDataFromGarrisonTroopList.
        // We return null for the spear case to match ALL spear-eligible guards
        // (those mapped to sp_guard_castle OR sp_guard_with_spear).
        if (unarmed) return "sp_guard_unarmed";
        if (overrideWeaponWithSpear) return null;
        return "sp_guard";
    }
}
