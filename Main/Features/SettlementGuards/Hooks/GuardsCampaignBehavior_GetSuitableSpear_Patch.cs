using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.SettlementGuards.Hooks;

public static class GuardsCampaignBehavior_GetSuitableSpear_Patch
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
            "GetSuitableSpear",
            new[] { typeof(CultureObject) });
    }

    [HarmonyPrefix]
    public static bool Prefix(CultureObject culture, ref ItemObject __result)
    {
        if (_service == null) return true;

        try
        {
            var spearItemId = _service.ResolveSpearItemId(culture?.StringId);
            if (spearItemId == null) return true;

            var item = MBObjectManager.Instance.GetObject<ItemObject>(spearItemId);
            if (item == null) return true;

            __result = item;
            return false;
        }
        catch
        {
            // Degrade gracefully — let vanilla hardcode run
        }

        return true;
    }
}
