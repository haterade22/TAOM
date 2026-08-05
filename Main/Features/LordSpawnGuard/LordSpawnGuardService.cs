using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.LordSpawnGuard;

/// <summary>
/// Pure decision logic behind <c>Patch65_LandlessCultureSpawnGuard</c>. Operates on string ids;
/// every engine read/write goes through <see cref="ILordSpawnGuardAdapter"/>.
///
/// THE CRASH THIS PREVENTS (report 099f650c, 2026-08-04, TAOM v2.0.16 / Bannerlord v1.4.7):
/// <code>
/// InvalidOperationException: Sequence contains no matching element
///   at Enumerable.First(...)  at HeroSpawnCampaignBehavior.SpawnLordParty(Hero, Boolean)
///   at HeroSpawnCampaignBehavior.ConsiderSpawningLordParties -> CampaignEvents.DailyTickClan
/// </code>
/// Vanilla's tail (v1.4.7 <c>HeroSpawnCampaignBehavior</c>:252-260) is:
/// <code>
/// var s = SettlementHelper.GetBestSettlementToSpawnAround(hero);
/// if (s == null || s.MapFaction != hero.MapFaction) s = hero.MapFaction.InitialHomeSettlement;
/// if (s == null) s = Settlement.All.First(x =&gt; x.Culture == hero.Culture);   // unguarded
/// </code>
/// It needs two independent faults to reach that <c>First</c>:
///   1. the hero's faction has no <c>InitialHomeSettlement</c> — in practice a clan built at
///      runtime by another mod that never called <c>SetInitialHomeSettlement</c>. NOTE: vanilla
///      itself ships NO such clan. An earlier revision of this comment claimed three
///      (guardians / chosen_of_the_sky / freemen); all three sit inside XML comment spans in
///      SandBox spclans.xml — an ElementTree parse finds 95 live factions, none missing the
///      attribute, while a line-oriented grep finds 98. The other non-mod path is a settlement
///      rename that misses spclans.xslt, which re-points 26 vanilla clans whose original
///      initial_home_settlement ids are absent from TAOM_Map; and
///   2. the hero's culture owns no settlement at all.
///
/// Fault 2 is ours. <c>TAOM_Map/settlements.xslt</c> deletes every vanilla <c>&lt;Settlement&gt;</c>,
/// and TAOM_Map's own 988 settlements cover 27 of the 38 defined cultures. Five of the eleven
/// landless cultures can sit on an <c>Occupation.Lord</c> hero — <c>battania</c> (TAOM's Variag),
/// <c>darshi</c>, <c>nord</c>, <c>vakken</c>, <c>neutral_culture</c> — so vanilla's assumption that
/// every culture owns land does not hold here.
///
/// Rather than reimplement the vanilla method, this repairs its precondition: give the faction an
/// <c>InitialHomeSettlement</c> so vanilla takes the branch above the throwing line.
/// <c>Clan.InitialHomeSettlement</c> is a <c>[SaveableProperty]</c>, so the repair persists — one
/// write per broken faction, not a per-tick patch-up.
/// </summary>
public class LordSpawnGuardService : ILordSpawnGuardService
{
    private readonly ILordSpawnGuardAdapter _adapter;
    private readonly IModLogger _logger;

    // Factions we could not anchor. Without this the daily clan tick re-reports the same faction
    // every in-game day; Patch65's finalizer keeps them alive in the meantime.
    private readonly HashSet<string> _unanchorable = new HashSet<string>();

    public LordSpawnGuardService(ILordSpawnGuardAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public void EnsureSpawnAnchor(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return;

        try
        {
            // Cheapest gate first: one property read, and it clears every healthy faction before
            // we ever walk Settlement.All for the culture check.
            if (_adapter.FactionHasInitialHomeSettlement(heroId))
                return;

            // No anchor, but vanilla's culture lookup still resolves — not our crash.
            if (_adapter.AnySettlementHasHeroCulture(heroId))
                return;

            var factionId = _adapter.GetHeroMapFactionId(heroId);
            if (string.IsNullOrEmpty(factionId) || _unanchorable.Contains(factionId))
                return;

            var settlementId = FindAnchor(heroId);
            if (string.IsNullOrEmpty(settlementId))
            {
                _unanchorable.Add(factionId);
                _logger.LogWarning(
                    $"LordSpawnGuard: '{factionId}' has no home settlement and its culture " +
                    $"'{_adapter.GetHeroCultureId(heroId)}' owns none either, and no fallback " +
                    "settlement could be found — its lords will not raise parties this session");
                return;
            }

            if (!_adapter.SetFactionInitialHomeSettlement(heroId, settlementId))
            {
                _unanchorable.Add(factionId);
                _logger.LogWarning(
                    $"LordSpawnGuard: could not write home settlement '{settlementId}' onto " +
                    $"'{factionId}' — its lords will not raise parties this session");
                return;
            }

            _logger.LogInfo(
                $"LordSpawnGuard: '{factionId}' had no home settlement and its culture " +
                $"'{_adapter.GetHeroCultureId(heroId)}' owns none — anchored to '{settlementId}'");
        }
        catch (Exception ex)
        {
            // This runs inside a prefix on the campaign tick. A fault here must never be worse than
            // the crash it guards against; Patch65's finalizer is the remaining backstop.
            _logger.LogError($"LordSpawnGuard: anchor check failed for '{heroId}': {ex}");
        }
    }

    /// <summary>
    /// Deterministic — no RNG, so a reloaded save repairs the same faction the same way.
    /// Closest to a real home first, widening to "anywhere on the map" as a last resort.
    /// </summary>
    private string FindAnchor(string heroId)
    {
        // Evaluated in order and short-circuited — the last two walk Settlement.All.
        var candidate = _adapter.GetHeroHomeSettlementId(heroId);
        if (Usable(candidate)) return candidate;

        candidate = _adapter.GetHeroBornSettlementId(heroId);
        if (Usable(candidate)) return candidate;

        candidate = _adapter.GetClanLeaderSettlementId(heroId);
        if (Usable(candidate)) return candidate;

        candidate = _adapter.GetNearestFriendlySettlementId(heroId);
        if (Usable(candidate)) return candidate;

        candidate = _adapter.GetNearestSettlementId(heroId);
        return Usable(candidate) ? candidate : null;
    }

    private static bool Usable(string settlementId) => !string.IsNullOrWhiteSpace(settlementId);
}
