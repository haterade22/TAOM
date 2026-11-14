using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.AlignmentDesertion.Hooks;

/// <summary>
/// Patch150, #1189: prefix on the private <c>RecruitPrisonersCampaignBehavior.GenerateConformityForTroop(MobileParty,
/// CharacterObject, int)</c> (v1.5.7). The hourly tick of the main party and the daily tick of AI lord parties give conformity to
/// the first prisoner stack whose recruitable count is below its size, and stop there because vanilla always returns true.
/// <c>TaomPrisonerRecruitmentCalculationModel</c> returns 0 for a stack that would desert the party, so such a stack always
/// qualified and took the tick every time it came first. For a stack <see cref="IDesertionRecruitGuard.WouldDesertFromParty"/>
/// blocks, the prefix skips vanilla and returns false: no conformity is added and the loop goes on to the next stack.
/// The guard reads per-peer settings, so while <see cref="ICoopSessionProvider.ShouldDeferToHost"/> holds every stack runs vanilla.
/// Notes: docs/reference/harmony-patch-registry.md "Patch150_DesertionConformity".
/// </summary>
[HarmonyPatchCategory(AlignmentDesertionModule.PatchCategory)]
public static class Patch150_ConformityTick_Prefix
{
    private static IDesertionRecruitGuard? _guard;
    private static ICoopSessionProvider? _coop;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IDesertionRecruitGuard guard, ICoopSessionProvider coop, IModLogger logger)
    {
        _guard = guard;
        _coop = coop;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RecruitPrisonersCampaignBehavior), "GenerateConformityForTroop",
            new[] { typeof(MobileParty), typeof(CharacterObject), typeof(int) });

    [HarmonyPrefix]
    public static bool Prefix(MobileParty mobileParty, CharacterObject troop, ref bool __result)
    {
        var guard = _guard;
        if (guard == null || mobileParty == null)
            return true;

        try
        {
            // The guard first: it is off or false for most stacks, and the co-op probe re-reads Coop's state per call.
            if (!guard.WouldDesertFromParty(mobileParty.Party, troop) || _coop?.ShouldDeferToHost != false)
                return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger?.LogError($"[AlignDesert] Patch150 prefix failed, keeping vanilla's conformity tick: {ex.GetType().Name}");
            }

            return true;
        }
    }
}
