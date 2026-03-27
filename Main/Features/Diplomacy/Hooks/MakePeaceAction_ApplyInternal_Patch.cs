using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Features.Diplomacy.Hooks;

[HarmonyPatchCategory("Patch12_WarOfTheRing")]
public static class MakePeaceAction_ApplyInternal_Patch
{
    private static IOnPeaceAction _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnPeaceAction hook)
    {
        _hook = hook;
    }

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MakePeaceAction), "ApplyInternal");
    }

    [HarmonyPrefix]
    public static bool Prefix(IFaction faction1, IFaction faction2)
    {
        if (_hook == null)
        {
            _logger?.LogWarning("[WarOfTheRing] MakePeaceAction_ApplyInternal_Patch: hook not initialized");
            return true;
        }

        if (!faction1.IsKingdomFaction || !faction2.IsKingdomFaction)
            return true;

        if (_hook.ShouldPreventPeace(faction1.StringId, faction2.StringId))
        {
            return false;
        }

        return true;
    }
}
