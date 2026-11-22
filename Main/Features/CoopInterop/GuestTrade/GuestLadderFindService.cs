using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Lord's materials for co-op players (docs/features/coop-interop.md, "Lords' Ladder for co-op players"). After a battle
/// won, the co-op server rolls TAOM's own find (<see cref="LordsLadderService.RollMaterialDrop"/>, which reads no player
/// adapter) for each co-op player's hero on the winning side, at the base chance: the hero's kills are counted on the
/// player's PC, and the server takes no count from a client. The find is kept in the server's save
/// (<see cref="ArmourAcquisitionState.PendingFinds"/>) and given to the hero's party on a later hourly tick, once the
/// party is free and out of battle. The local main hero is never rolled here; <c>LordsLadderBehavior</c> rolls that one.
/// </summary>
public sealed class GuestLadderFindService
{
    private readonly LordsLadderService _ladder;
    private readonly ArmourAcquisitionState _state;
    private readonly IGuestTradeHeroAdapter _heroes;
    private readonly IGuestTradeShops _shops;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;
    /// <summary>Heroes whose hand-over threw, warned about once per process; their find stays for a later hour.</summary>
    private readonly HashSet<string> _failureWarned = new(StringComparer.Ordinal);

    public GuestLadderFindService(LordsLadderService ladder, ArmourAcquisitionState state, IGuestTradeHeroAdapter heroes,
        IGuestTradeShops shops, ICoopSessionProvider coop, IModLogger logger)
    {
        _ladder = ladder;
        _state = state;
        _heroes = heroes;
        _shops = shops;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>The co-op server of a live session, with the ladder open to co-op players. Read live.</summary>
    public bool RollsFinds => _coop.IsSessionActive && _coop.IsAuthority && _shops.LadderOpen;

    /// <summary>A battle was won; <paramref name="winners"/> are the leaders on the winning side.</summary>
    public void OnBattleWon(IReadOnlyList<EarnerRef?>? winners, Random rng)
    {
        if (winners == null || !RollsFinds)
            return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var winner in winners)
        {
            if (winner == null || winner.Control != PlayerControl.Player || winner.IsLocalMainHero
                || string.IsNullOrEmpty(winner.HeroId) || !seen.Add(winner.HeroId))
                continue;
            if (_ladder.RollMaterialDrop(winner.HeroId, true, 0, winner.CultureId, rng) is not { } find)
                continue;
            var held = _state.PendingFinds.TryGetValue(winner.HeroId, out var units) ? units : 0;
            _state.PendingFinds[winner.HeroId] = Math.Min(ArmourAcquisitionState.MaxPendingFindUnits, held + find.Units);
            _logger.LogInfo($"[ArmourAcquisition] Lord's material found for co-op player {winner.HeroId}: {find.Units} x {find.ItemId}, given once the party is free.");
        }
    }

    /// <summary>Gives each held find to its hero's party when that party is free and out of battle.</summary>
    public void HandOver()
    {
        if (_state.PendingFinds.Count == 0 || !_coop.IsSessionActive || !_coop.IsAuthority)
            return;
        foreach (var pair in _state.PendingFinds.ToList())
        {
            try
            {
                HandOverOne(pair.Key, pair.Value);
            }
            catch (Exception ex)
            {
                if (_failureWarned.Add(pair.Key))
                    _logger.LogWarning($"[ArmourAcquisition] Lord's material for {pair.Key} could not be handed over, kept for a later hour: {ex.Message}");
            }
        }
    }

    private void HandOverOne(string heroId, int units)
    {
        var hero = _heroes.Find(heroId);
        if (hero == null || !hero.IsAlive)
        {
            Drop(heroId, hero == null ? "the hero is gone" : "the hero is dead");
            return;
        }
        if (!hero.IsFreeLeader || hero.InBattle)
            return;
        if (_ladder.MaterialFor(hero.Player.CultureId) is not { } material)
        {
            Drop(heroId, $"culture '{hero.Player.CultureId}' has no lord's material");
            return;
        }
        if (!hero.Player.AddPiece(material, null, units))
        {
            Drop(heroId, $"{material} could not be added");
            return;
        }
        _state.PendingFinds.Remove(heroId);
        _logger.LogInfo($"[ArmourAcquisition] Lord's material given to co-op player {heroId}: {units} x {material}.");
    }

    private void Drop(string heroId, string why)
    {
        _logger.LogWarning($"[ArmourAcquisition] Lord's material held for {heroId} dropped: {why}.");
        _state.PendingFinds.Remove(heroId);
    }
}
