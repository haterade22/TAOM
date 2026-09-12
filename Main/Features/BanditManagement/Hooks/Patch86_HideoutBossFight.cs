using System;
using TAOM.Core.Logging;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch86 — the hideout boss fight is exactly 1 boss + N bodyguards on both routes (#564).
/// Shared category, logger and service resolver for the two prefixes:
/// <see cref="Patch86_HideoutAssaultBossFight"/> (daytime assault) and
/// <see cref="Patch86_HideoutAmbushBossFight"/> (night sneak-in).
///
/// WHY TWO PREFIXES AND A MODEL. The boss party template only sizes the boss PARTY sitting in
/// the hideout; the engine sizes the fight from the whole hideout population, differently on
/// each route:
/// - Assault: <c>Helpers.MapEventHelper.GetPriorityListForHideoutMission</c> (v1.4.8 :147-172)
///   sends <c>min(floor(0.8 * total), FirstFightMax)</c> to phase 1 and everything else to the
///   boss phase. Before it, <c>HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission</c>
///   (:606-656) trims the hideout to <c>FirstFightMax + BossFightMax</c> but never trims the boss
///   party (:615). <c>TaomBanditDensityModel.NumberOfMaximumTroopCountForBossFightInHideout</c>
///   now returns <c>1 + N</c> so that trim keeps phase 1 at its old size; the assault prefix then
///   holds back exactly the boss(es) plus N regulars.
/// - Sneak-in: <c>HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight</c> (:478-555)
///   pads its list UP to <c>Clamp(pop / 2, 4, 20)</c> (:507-511) and spawns every element
///   (:528-542). The clamp is a floor. The ambush prefix trims the list to N first.
///
/// Resolvers rather than instances: both prefixes run in the standard patch batch, and the
/// delegate is invoked on the first hideout fight, by which time the container is complete
/// (the Patch85 shape).
/// </summary>
public static class Patch86_HideoutBossFight
{
    internal const string Category = "Patch86_HideoutBossFight";

    private static IModLogger _logger;
    private static Func<IHideoutBossFightService> _service;

    internal static void Initialize(IModLogger logger, Func<IHideoutBossFightService> service)
    {
        _logger = logger;
        _service = service;
    }

    internal static IModLogger Logger => _logger;

    /// <summary>Null before <see cref="Initialize"/> or after <see cref="ResetForUnload"/>; the prefixes then defer to vanilla.</summary>
    internal static IHideoutBossFightService Service => _service?.Invoke();

    /// <summary>Test seam and unload hook. The bindings are process-global, so a test that rebinds must restore them.</summary>
    internal static void ResetForUnload()
    {
        _logger = null;
        _service = null;
    }
}
