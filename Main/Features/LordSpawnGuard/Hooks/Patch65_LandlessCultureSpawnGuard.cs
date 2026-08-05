using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.LordSpawnGuard.Hooks;

/// <summary>
/// Patch65 — keeps the daily clan tick alive when a lord's culture owns no settlement.
///
/// Vanilla <c>HeroSpawnCampaignBehavior.SpawnLordParty</c> ends with an unguarded
/// <c>Settlement.All.First(x =&gt; x.Culture == hero.Culture)</c>, reached only when the hero's map
/// faction has no <c>InitialHomeSettlement</c>. In vanilla that is safe — every Calradian culture
/// owns land. In TAOM it is not: <c>TAOM_Map/settlements.xslt</c> drops every vanilla settlement,
/// and five of the cultures a Lord-occupation hero can carry own nothing (<c>battania</c> — TAOM's
/// Variag — plus <c>darshi</c>, <c>nord</c>, <c>vakken</c>, <c>neutral_culture</c>). Pair that with a
/// faction whose <c>InitialHomeSettlement</c> was never set — in practice a clan built at runtime
/// by another mod (vanilla ships none; see <see cref="LordSpawnGuardService"/>) — and the LINQ throws
/// <c>InvalidOperationException</c> straight out of <c>Campaign.Tick</c>. Crash report 099f650c
/// (2026-08-04) is exactly that; see <see cref="LordSpawnGuardService"/> for the full chain.
///
/// Two layers, in order of preference:
///
/// 1. <b>Prefix</b> — repairs the precondition rather than reimplementing the method. It gives the
///    faction a home settlement so vanilla takes the branch above the throwing line and every
///    downstream side effect (party spawn position, initial troops, initial items) stays vanilla.
///    <c>Clan.InitialHomeSettlement</c> is a <c>[SaveableProperty]</c>, so the repair persists.
/// 2. <b>Finalizer</b> — the backstop for anything the prefix could not anchor, scoped to
///    <see cref="InvalidOperationException"/> only; every other exception propagates untouched.
///    Returning null is safe because <c>ConsiderSpawningLordParties</c> already null-checks the
///    result before calling <c>GiveInitialItemsToParty</c> — the lord simply raises no party that
///    day instead of taking the campaign down.
///
/// The target is private, so it is matched by name. Both layers are per-lord-per-day at most, so
/// there is no hot-path cost; the service resolve is cached rather than resolved per call.
/// </summary>
[HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
[HarmonyPatchCategory("Patch65_LandlessCultureSpawnGuard")]
public static class Patch65_LandlessCultureSpawnGuard
{
    private static ILordSpawnGuardService _service;
    private static IModLogger _logger;

    // A swallowed exception with no trace is how a guard becomes an invisible bug: the lord is
    // parked forever and nothing says why. Named once per hero — the daily tick would repeat it.
    private static readonly HashSet<string> _reported = new HashSet<string>();

    private static ILordSpawnGuardService GetService() =>
        _service ??= TAOM.IoC.Resolve<ILordSpawnGuardService>();

    [HarmonyPrefix]
    public static void Prefix(Hero hero)
    {
        if (hero == null) return;

        try
        {
            GetService()?.EnsureSpawnAnchor(hero.StringId);
        }
        catch
        {
            // The service already swallows its own faults; this covers an IoC resolve failing
            // before the container is built. The finalizer below still guards the call.
        }
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, Hero hero, ref MobileParty __result)
    {
        if (!(__exception is InvalidOperationException))
            return __exception;

        __result = null;
        Report(hero, __exception);
        return null;
    }

    private static void Report(Hero hero, Exception ex)
    {
        try
        {
            var heroId = hero?.StringId ?? "(unresolved hero)";
            if (!_reported.Add(heroId)) return;

            _logger ??= TAOM.IoC.Resolve<IModLogger>();
            _logger?.LogWarning(
                $"Patch65: SpawnLordParty threw {ex.GetType().Name} for '{heroId}' " +
                $"(culture '{hero?.Culture?.StringId ?? "?"}', faction " +
                $"'{hero?.MapFaction?.StringId ?? "?"}') — suppressed, so this lord raises no " +
                $"party today instead of crashing the campaign. The anchor repair could not fix " +
                $"this faction; see docs/features/lord-spawn-guard.md. Detail: {ex.Message}");
        }
        catch
        {
            // Reporting must never resurrect the crash the finalizer just suppressed.
        }
    }
}
