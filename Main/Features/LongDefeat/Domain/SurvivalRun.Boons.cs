using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What the report lists of the boons: every pick opened (in order), the rerolls earned and used, and the bank left.</summary>
internal sealed record RunBoons(IReadOnlyList<BoonPickRecord> Picks, int Earned, int Used, int Banked);

/// <summary>
/// The boon side of the run (phase2-plan.md section 2, "Boons"): a pick owed every third cleared wave and open until taken, made between waves only, a reroll
/// that swaps the offer, and each boon owned folded into the effective rules, the gold factors, the shop and the affix cut. Every number a boon changes is
/// recomputed from the boons owned, never patched, so a second boon cannot apply the first twice.
/// </summary>
internal sealed partial class SurvivalRun
{
    private readonly BoonDeck _deck;
    private IReadOnlyList<ShopOffer> _shop;
    private GoldFactors _factors = GoldFactors.One;
    private int _affixCut;
    private IReadOnlyList<BoonCard> _offerCards = Array.Empty<BoonCard>();
    private IReadOnlyList<BoonCard> _ownedCards = Array.Empty<BoonCard>();
    private readonly float[] _healthFactors = { 1f, 1f, 1f, 1f };
    private LdBoonApply? _pendingBoon;

    /// <summary>The rules in force: the shipped ones with the fortress boons owned folded in. An intermission boon acts from the next clear.</summary>
    public RunRules Rules => _r;

    /// <summary>The war chest factors in force.</summary>
    public GoldFactors Factors => _factors;

    public IReadOnlyList<BoonRow> OwnedBoons => _deck.Owned;

    /// <summary>The affixes the boons owned cut from every wave's schedule: zero or negative.</summary>
    public int AffixCut => _affixCut;

    /// <summary>The boons taken as the HUD shows them.</summary>
    public IReadOnlyList<BoonCard> OwnedCards => _ownedCards;

    /// <summary>The raw <c>{=KEY}English</c> name of the class the lord plays, or empty: kept by the end panel after the run.</summary>
    public string ClassName => _plan.ClassName;

    /// <summary>The health factor in force for a group of the player's side or for the lord: what a soldier spawned now is multiplied by (the product of the boons' own factors).</summary>
    public float HealthFactor(BoonTarget target) => _healthFactors[(int)target];

    public RunBoons BoonSummary() => new(_deck.Picks, _deck.Earned, _deck.Used, _deck.Banked);

    /// <summary>Takes a card of the open offer. Between waves only; an offer left open stays open into the next intermission.</summary>
    public PickResult TryPickBoon(string boonId)
    {
        if (Result != null)
            return PickResult.RunOver;
        if (!BetweenWaves)
            return PickResult.NotOpen;
        if (_deck.Offer.Count == 0)
            return PickResult.NoOffer;

        var opened = _deck.Picks.Count;
        var pick = _deck.Picks[opened - 1].Pick;
        if (!_deck.TryPick(boonId, _wave, _wave + 1))
            return PickResult.NotOffered;

        var taken = _deck.Owned[_deck.Owned.Count - 1];
        ApplyBoons();
        NoteTargets(taken);
        _purse.Earn(PurseSource.Boon, RunModifiers.GoldPaid(new[] { taken }));
        _pendingEvents.Add(new RunEvent(RunEventKind.BoonTaken, _wave, $"pick={pick} boon={taken.Id}"));
        AnnounceOpened(opened, _pendingEvents);
        RefreshCards();
        return PickResult.Ok;
    }

    /// <summary>Spends a banked reroll to discard the open offer and draw a new one that leaves out the cards just shown.</summary>
    public RerollResult TryReroll()
    {
        if (Result != null)
            return RerollResult.RunOver;
        if (!BetweenWaves)
            return RerollResult.NotOpen;
        if (_deck.Offer.Count == 0)
            return RerollResult.NoOffer;
        if (!_deck.TryReroll())
            return RerollResult.NoReroll;

        _pendingEvents.Add(new RunEvent(RunEventKind.Rerolled, _wave, $"pick={_deck.Picks[_deck.Picks.Count - 1].Pick} bank={_deck.Banked}"));
        RefreshCards();
        return RerollResult.Ok;
    }

    /// <summary>A wave was cleared: the deck owes its picks and rerolls and opens an offer, and the director hears of each one.</summary>
    private void OpenBoons(RunEffects fx)
    {
        var opened = _deck.Picks.Count;
        _deck.Cleared(_wavesCleared, _wave);
        AnnounceOpened(opened, fx.Events);
        RefreshCards();
    }

    private void AnnounceOpened(int alreadyOpened, List<RunEvent> events)
    {
        for (var i = alreadyOpened; i < _deck.Picks.Count; i++)
        {
            var record = _deck.Picks[i];
            events.Add(new RunEvent(record.Lapsed ? RunEventKind.BoonLapsed : RunEventKind.BoonOffered, _wave, "pick=" + record.Pick));
        }
    }

    private void RefreshCards()
    {
        _offerCards = _deck.Offer.Select(BoonCard.From).ToArray();
        _ownedCards = _deck.Owned.Select(BoonCard.From).ToArray();
    }

    /// <summary>
    /// What the engine is told about a troop or class boon at the next tick: the table to publish, the groups to refresh and the boon's own health factor for those standing.
    /// A boon with no targeted effect (war chest, fortress, Legend) tells it nothing.
    /// </summary>
    private void NoteTargets(BoonRow taken)
    {
        if (!taken.Effects.Concat(taken.Drawback).Any(e => BoonKeys.IsTargeted(e.Key)))
            return;

        // Two picks can land before the engine's next tick (a second offer opens the moment the first is taken), so a pick not yet told to the director is added to, never replaced.
        var health = RunModifiers.HealthTargets(taken).Select(t => new LdHealthScale(t, RunModifiers.OwnHealthFactor(taken, t)));
        var refresh = RunModifiers.StatTargets(taken);
        _pendingBoon = _pendingBoon == null
            ? new LdBoonApply(refresh, health.ToList())
            : new LdBoonApply(_pendingBoon.Refresh.Union(refresh).ToList(), _pendingBoon.Health.Concat(health).ToList());
    }

    private void TakePendingBoon(RunEffects fx)
    {
        if (_pendingBoon == null)
            return;

        fx.ApplyBoon = _pendingBoon;
        fx.PublishStats = StatTableNow();
        _pendingBoon = null;
    }

    /// <summary>The table for the wave in hand and the boons owned: the attackers' part is the wave's (and a war cry's, while one runs), the defenders' part is the boons'.</summary>
    private LdStatTable StatTableNow() => RunModifiers.StatTable(_plan.Affixes, _waveAffixes, _damageFactor, _deck.Owned, _warCry);

    private float DefenderHealth(WaveSoldier soldier) => _healthFactors[(int)_plan.GroupOf(soldier.TroopId, soldier.Slot)];

    /// <summary>Recomputes everything a boon changes from the boons owned: the rules, the gold factors, the affix cut, the health factors and the shop's prices and pack sizes.</summary>
    private void ApplyBoons()
    {
        var owned = _deck.Owned;
        foreach (var target in BoonTargets.All)
            _healthFactors[(int)target] = RunModifiers.HealthFactor(owned, target);
        _r = RunModifiers.Rules(_plan.Rules, owned);
        _factors = RunModifiers.Gold(owned);
        _affixCut = RunModifiers.AffixCut(owned);
        _shop = owned.Count == 0
            ? _plan.Shop.Offers
            : _plan.Shop.Offers
                .Select(o => o with { Price = Math.Max(1, GoldFactors.Apply(o.Price, _factors.Price)), PackSize = Math.Max(1, o.PackSize + _r.PackBonus) })
                .ToArray();
    }
}
