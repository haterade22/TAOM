using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;

namespace TAOM.Features.AlignmentRecruitment.Hooks;

/// <summary>
/// Patch114, #887: postfix on the private <c>AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero, Settlement)</c>
/// (v1.5.5), which counts a settlement's filled volunteer slots with no alignment check when an AI lord scores a recruiting trip.
/// <c>TaomVolunteerModel</c> blocks the actual recruit across the Free/Evil divide, so a lord could plan a trip that yields nothing.
/// When the recruit would be blocked, the estimate becomes (0, 0). Only the estimate changes:
/// food and trade visits keep vanilla's score floor. The keys (recruiter Clan.Kingdom.StringId, source MapFaction.StringId,
/// hero == Hero.MainHero) mirror <c>TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero</c>: change both together. Notes: docs/reference/harmony-patch-registry.md "Patch114_AlignmentRecruitmentScoring".
/// When the recruit is allowed, the estimate is recounted without the volunteers alignment desertion would take from the
/// lord (<see cref="IDesertionRecruitGuard.RecountEstimate"/>), as the model caps the recruit there too; a captured fief's
/// old-culture volunteers no longer draw a trip.
/// </summary>
[HarmonyPatchCategory(AlignmentRecruitmentModule.PatchCategory)]
public static class Patch114_AiVolunteerEstimate_Postfix
{
    private static IRecruitmentAlignmentService? _service;
    private static IDesertionRecruitGuard? _desertionGuard;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IRecruitmentAlignmentService service, IDesertionRecruitGuard desertionGuard, IModLogger logger)
    {
        _service = service;
        _desertionGuard = desertionGuard;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(AiVisitSettlementBehavior), "GetApproximateVolunteersCanBeRecruitedDataFromSettlement",
            new[] { typeof(Hero), typeof(Settlement) });

    [HarmonyPostfix]
    public static void Postfix(Hero hero, Settlement settlement, ref (int, float) __result)
    {
        var service = _service;
        if (service == null || __result.Item1 == 0 || hero == null || settlement == null)
            return;

        try
        {
            var source = settlement.MapFaction;
            // Own map faction: the same kingdom on both sides, or no kingdom (Neutral), so the service never blocks it; most calls.
            if (hero.MapFaction != source
                && service.IsRecruitmentBlocked(hero.Clan?.Kingdom?.StringId, source?.StringId, hero == Hero.MainHero))
            {
                __result = (0, 0f);
                return;
            }

            var recount = _desertionGuard?.RecountEstimate(hero, settlement);
            if (recount.HasValue)
                __result = recount.Value;
        }
        catch (Exception ex)
        {
            if (_logged)
                return;
            _logged = true;
            _logger?.LogError($"[AlignmentRecruitment] Patch114 postfix failed, keeping vanilla's estimate: {ex}");
        }
    }
}
