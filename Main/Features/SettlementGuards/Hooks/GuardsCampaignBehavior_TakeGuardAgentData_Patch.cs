using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards.Hooks;

public static class GuardsCampaignBehavior_TakeGuardAgentData_Patch
{
    private static ISettlementGuardService _service;
    private static MethodInfo _prepareMethod;

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
        catch
        {
            // Degrade gracefully — let vanilla run
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
