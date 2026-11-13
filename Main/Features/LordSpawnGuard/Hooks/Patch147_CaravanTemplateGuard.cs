using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.LordSpawnGuard.Hooks;

/// <summary>
/// Patch147 (#482): a merchant whose culture has no caravan template that fits where he stands
/// sends no caravan that day, instead of crashing the campaign.
///
/// Vanilla <c>CaravansCampaignBehavior.SpawnCaravan</c> dereferences the template its pick returns,
/// and the pick returns null when nothing matches. The prefix hands vanilla's own inputs to
/// <see cref="CaravanTemplateGuard.Judge"/>, which repeats the list choice and the predicate, and
/// skips vanilla only when the pick fails. Vanilla throws at that point before it changes any
/// state, so the skip drops nothing, and every other call runs vanilla untouched. Patch147 has no
/// finalizer of its own. Callers, limits and the finalizers other code puts on the method:
/// <c>docs/reference/harmony-patch-registry.md</c>, section <c>Patch147_CaravanTemplateGuard</c>.
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.SpawnCaravan))]
[HarmonyPatchCategory("Patch147_CaravanTemplateGuard")]
public static class Patch147_CaravanTemplateGuard
{
    private static IModLogger _logger;

    // Process lifetime (nothing reloads TAOM inside a process). A key carries the campaign id, so
    // a second campaign reports its own gaps, and it is added only after its line was written.
    private static readonly HashSet<string> _reported = new HashSet<string>();

    /// <summary>Test seam: sets the logger and forgets what was reported.</summary>
    internal static void UseLogger(IModLogger logger)
    {
        _logger = logger;
        _reported.Clear();
    }

    [HarmonyPrefix]
    public static bool Prefix(Hero hero)
    {
        try
        {
            var culture = hero?.Culture;
            var model = culture == null ? null : Campaign.Current?.Models?.CaravanModel;
            if (model == null)
                return true; // not this guard's case: vanilla decides, as with no patch

            var settlement = hero.CurrentSettlement;
            var failure = CaravanTemplateGuard.Judge(
                eliteSpawnChance: model.GetEliteCaravanSpawnChance(hero),
                heroRandomFloat: hero.RandomFloat(),
                hasCurrentSettlement: settlement != null,
                settlementHasPort: settlement != null && settlement.HasPort,
                normalHullCounts: HullCounts(culture.CaravanPartyTemplates),
                eliteHullCounts: HullCounts(culture.EliteCaravanPartyTemplates),
                out var elite);
            if (failure == CaravanPickFailure.None)
                return true;

            ReportOnce($"{culture.StringId}|{elite}|{failure}|{settlement?.HasPort}",
                () => Describe(hero, culture, settlement, elite, failure));
            return false;
        }
        catch (Exception ex)
        {
            // The guard could not judge this call, so vanilla runs as it would with no patch.
            ReportOnce("fault|" + ex.GetType().Name,
                () => $"Patch147: the caravan template guard failed and is off for this call: {ex}");
            return true;
        }
    }

    // A null entry stands for a template vanilla cannot read: a null in the list, or a template
    // whose ShipHulls was never filled (PartyTemplateObject sets it only in Deserialize).
    private static List<int?> HullCounts(IEnumerable<PartyTemplateObject> templates) =>
        templates?.Select(t => t?.ShipHulls == null ? (int?)null : t.ShipHulls.Count).ToList();

    private static string Describe(Hero hero, CultureObject culture, Settlement settlement, bool elite,
        CaravanPickFailure failure)
    {
        var list = elite ? "elite_caravan_party_templates" : "caravan_party_templates";
        string cause;
        switch (failure)
        {
            case CaravanPickFailure.NoCurrentSettlement:
                cause = "the merchant is in no settlement, which vanilla reads to choose a template";
                break;
            case CaravanPickFailure.BrokenTemplateEntry:
                cause = $"the {list} list of culture '{culture.StringId}' holds an entry with no template data " +
                        "(a child with no id attribute, or an id that no party template defines)";
                break;
            default:
                var port = settlement != null && settlement.HasPort;
                cause = $"the {list} list of culture '{culture.StringId}' has no template for a " +
                        $"{(port ? "port" : "land")} settlement ('{settlement?.StringId}'); it needs one " +
                        $"{(port ? "with" : "without")} a ship_hulls element";
                break;
        }

        return $"Patch147: SpawnCaravan skipped for '{hero.StringId}': {cause}. " +
               "This merchant sends no caravan today (#482).";
    }

    private static void ReportOnce(string key, Func<string> message)
    {
        try
        {
            key = (Campaign.Current?.UniqueGameId ?? "") + "|" + key;
            if (_reported.Contains(key)) return;

            _logger ??= TAOM.IoC.Resolve<IModLogger>();
            if (_logger == null) return;
            _logger.LogWarning(message());
            _reported.Add(key);
        }
        catch
        {
            // swallow-ok: a failed report must not decide whether the caravan spawns.
        }
    }
}
