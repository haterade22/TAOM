using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.LordSpawnGuard.Hooks;

/// <summary>
/// Patch147 (#482): keeps the daily hero tick alive when a merchant's culture has no caravan
/// template that fits where the merchant stands.
///
/// Vanilla <c>CaravansCampaignBehavior.SpawnCaravan</c> dereferences the template its predicate
/// pick returns, and that pick returns null when nothing matches (an all-land template list in a
/// port town, an empty list, or a merchant with no current settlement). See
/// <see cref="CaravanTemplateGuard"/> for the engine lines.
///
/// The prefix never skips vanilla. It only records in <c>__state</c> whether this call can hit the
/// null pick. The finalizer then suppresses a <see cref="NullReferenceException"/> on a flagged call
/// and nothing else; every other exception, and an NRE on an unflagged call, propagates untouched.
/// The null template is read before <c>CreateCaravanParty</c> runs, so a suppressed call has made
/// no party and changed no state: the merchant simply sends no caravan that day.
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), nameof(CaravansCampaignBehavior.SpawnCaravan))]
[HarmonyPatchCategory("Patch147_CaravanTemplateGuard")]
public static class Patch147_CaravanTemplateGuard
{
    private static IModLogger _logger;

    // One line per culture and port state: the daily tick would otherwise repeat it every day.
    private static readonly HashSet<string> _reported = new HashSet<string>();

    [HarmonyPrefix]
    public static void Prefix(Hero hero, out bool __state)
    {
        __state = false;
        try
        {
            var settlement = hero?.CurrentSettlement;
            var culture = hero?.Culture;
            __state = culture == null || CaravanTemplateGuard.CanFailToResolve(
                settlement != null,
                settlement != null && settlement.HasPort,
                HullCounts(culture.CaravanPartyTemplates),
                HullCounts(culture.EliteCaravanPartyTemplates));
        }
        catch
        {
            // swallow-ok: a read that throws here throws in vanilla too; flagging the call lets
            // the finalizer contain the NRE that follows.
            __state = true;
        }
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, Hero hero, bool __state)
    {
        if (!__state || !(__exception is NullReferenceException))
            return __exception;

        Report(hero);
        return null;
    }

    private static IEnumerable<int> HullCounts(IEnumerable<PartyTemplateObject> templates) =>
        templates?.Select(t => t?.ShipHulls?.Count ?? 0).ToList();

    private static void Report(Hero hero)
    {
        try
        {
            var cultureId = hero?.Culture?.StringId ?? "?";
            var settlement = hero?.CurrentSettlement;
            var key = cultureId + "|" + (settlement == null ? "none" : settlement.HasPort ? "port" : "land");
            if (!_reported.Add(key)) return;

            _logger ??= TAOM.IoC.Resolve<IModLogger>();
            _logger?.LogWarning(
                $"Patch147: SpawnCaravan found no caravan template for '{hero?.StringId ?? "?"}' " +
                $"(culture '{cultureId}', settlement '{settlement?.StringId ?? "none"}', " +
                $"port {settlement?.HasPort.ToString() ?? "n/a"}). Suppressed, so this merchant " +
                "sends no caravan today instead of crashing the campaign. Give the culture a " +
                "caravan template that fits this case (#482).");
        }
        catch
        {
            // swallow-ok: reporting must never bring back the crash the finalizer just contained.
        }
    }
}
