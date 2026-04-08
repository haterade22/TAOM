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

[HarmonyPatchCategory("Patch28_SettlementGuards")]
public static class GuardsCampaignBehavior_TakeGuardAgentData_Patch
{
    private static ISettlementGuardService _service;

    public static void Initialize(ISettlementGuardService service)
    {
        _service = service;
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

            // Delegate to the vanilla PrepareGuardAgentDataFromGarrison for equipment assembly.
            // We can't call it directly (private static), so we let the original method run
            // but swap the garrison troop list to force our character.
            // Instead, skip and return false — but we need the AgentData built.
            // The cleanest approach: let vanilla run but ensure it picks our character.
            // Since we can't easily inject into the weighted random, we use a different approach:
            // We don't skip vanilla — instead we just return true and let it run normally
            // when there's no config. When there IS config, we need to build the AgentData ourselves.

            // Call the private static PrepareGuardAgentDataFromGarrison via reflection
            var prepareMethod = AccessTools.Method(
                typeof(GuardsCampaignBehavior),
                "PrepareGuardAgentDataFromGarrison",
                new[] { typeof(CharacterObject), typeof(bool), typeof(bool) });

            if (prepareMethod != null)
            {
                __result = (AgentData)prepareMethod.Invoke(null, new object[] { character, overrideWeaponWithSpear, unarmed });
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
