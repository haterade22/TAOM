using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One Fords of War match, engine-free. The engine layer feeds it a <see cref="MatchSnapshot"/> every tick and applies the
/// returned <see cref="MatchEffects"/>; commanders (humans through the panel, AIs through <see cref="AiCommander"/>) act
/// through the Try* methods; engine callbacks report kills, tower hits and fallen towers. Not thread safe: the engine
/// layer serialises every call (RunOrDefer or a lock).
/// </summary>
internal sealed partial class FordsMatch
{
    private static readonly MatchSide[] Sides = { MatchSide.West, MatchSide.East };

    private readonly FordsMatchConfig _config;
    private readonly MapLayout _layout;
    private readonly IReadOnlyDictionary<MatchSide, FactionCatalog> _factions;
    private readonly IReadOnlyList<CommanderSlot> _slots;
    private readonly Dictionary<int, CommanderSlot> _slotsById = new();
    private readonly Dictionary<int, FordsAiProfile> _profiles = new();
    private readonly List<(int Slot, AiCommander Ai)> _ais = new();
    private readonly Dictionary<int, double> _incomeCarry = new();

    private readonly MatchClock _clock;
    private readonly GoldLedger _ledger;
    private readonly BountyRules _bounty;
    private readonly TowerChain _chain;
    private readonly TowerDamageRules _damage;
    private readonly TowerAssaultLimit _assaultLimit;
    private readonly TowerWear _wear;
    private bool _wearAnnounced;
    private readonly float _timeLimitMinutes;
    private bool _timeWarned;
    private bool _endedOnTime;
    private readonly UpgradeBook _upgrades;
    private readonly PoisonRules _poison = new();
    private readonly TowerRegen _regen;
    private readonly TowerFireRules _fire;
    private readonly HeroRoster _heroes;
    private readonly LaneOrderBook _orders;
    private readonly LaneMarchRules _march;
    private readonly CapturePointRules _capture;
    private readonly CampRules _camps;
    private readonly Dictionary<MatchSide, RegionDeck> _decks = new();
    private readonly Dictionary<int, float> _creatureReadyAt = new();
    private readonly SeizeRules _seize;
    private readonly WaveScheduler _waves;

    private readonly Dictionary<AgentKey, UnitRecord> _units = new();
    private readonly RecentDeathLog _recentDeaths = new();
    private readonly HashSet<string> _fallen = new();
    private readonly List<SpawnRequest> _pendingSpawns = new();
    private readonly List<MatchEvent> _pendingEvents = new();

    private readonly Dictionary<(MatchSide, int), LaneSnapshot> _seenLanes = new();
    private readonly Dictionary<string, int> _seenThreats = new();
    private readonly Dictionary<MatchSide, int> _seenLiveBySide = new();

    private MatchSide? _winner;

    internal MatchPhase Phase => _clock.Phase;

    /// <summary>How far this tower fires, which its owner's feats decide.</summary>
    public float FireRange(string towerId) => _chain.FireRange(towerId);

    public FordsMatch(FordsMatchConfig config, MapLayout layout, IReadOnlyDictionary<MatchSide, FactionCatalog> factions,
        IReadOnlyList<CommanderSlot> slots, IFordsRandom random)
    {
        _config = config;
        _layout = layout;
        _factions = factions;
        _slots = slots;

        _clock = new MatchClock(config.CountdownSeconds);
        _timeLimitMinutes = config.TimeLimitMinutes;
        _ledger = new GoldLedger(config.Economy, slots.Select(s => s.Id));
        _bounty = new BountyRules(config.Economy);
        var feats = factions.ToDictionary(f => f.Key, f => f.Value.FeatsOrNone);
        _chain = new TowerChain(layout, config.Towers, feats);
        _upgrades = new UpgradeBook(config.UpgradesOrDefault, factions);
        _damage = new TowerDamageRules(config.Towers, _chain, feats, _upgrades);
        _assaultLimit = new TowerAssaultLimit(config.Towers.AssaultDamagePerSecond);
        _wear = new TowerWear(config.Towers.WearStartMinutes, config.Towers.WearPerMinute);
        _regen = new TowerRegen(config.Towers, _chain, feats);
        _fire = new TowerFireRules(config.Towers, _chain, feats);
        _heroes = new HeroRoster(config.Heroes, feats);
        _orders = new LaneOrderBook(config.Lanes, feats);
        _march = new LaneMarchRules(config.Lanes, config.Towers, layout, _chain);
        _capture = new CapturePointRules(config.Capture, config.Economy, layout.CapturePoints);
        _camps = new CampRules(layout, LaneOwner);
        _seize = new SeizeRules(layout, _camps, _capture);
        _waves = new WaveScheduler(config.Waves, factions, layout.Lanes.Count, LaneOwner);
        foreach (var faction in factions)
        {
            if (faction.Value.Rotation is { Regions.Count: > 0 } rotation)
                _decks[faction.Key] = new RegionDeck(rotation, random);
        }

        foreach (var slot in slots)
        {
            _slotsById[slot.Id] = slot;
            _incomeCarry[slot.Id] = 0d;
            if (slot.IsHuman)
                continue;

            var profile = ProfileFor(slot);
            _profiles[slot.Id] = profile;
            _ais.Add((slot.Id, new AiCommander(slot.Id, profile, random)));
        }
    }

    public bool IsTower(string towerId) => !string.IsNullOrEmpty(towerId) && _chain.IsTower(towerId);

    /// <summary>
    /// Advances the match by <see cref="MatchSnapshot.Dt"/>. Order: clock (a countdown only counts down), passive income,
    /// capture points, camps, tower regeneration, spawns (free and bought heroes, waves, hero respawns), tower volleys, AI decisions,
    /// lane commands. Once a keep has fallen the tick only reports the winner and the last events.
    /// </summary>
    public MatchEffects Tick(MatchSnapshot snapshot)
    {
        var effects = new MatchEffects();
        _decisions = Array.Empty<LaneDecision>();
        if (_clock.Phase == MatchPhase.Ended)
            return Finish(effects);

        var wasLive = _clock.Phase == MatchPhase.Live;
        _clock.Advance(snapshot.Dt);
        if (_clock.Phase == MatchPhase.Countdown)
            return Finish(effects);

        // The tick that ends the countdown only counts the part after it as live time.
        var dt = wasLive ? snapshot.Dt : _clock.Elapsed;
        if (!(dt > 0f))
            return Finish(effects);

        Observe(snapshot);
        AnnounceWear();
        if (TimeIsUp())
            return Finish(effects);

        PayPassiveIncome(dt);
        foreach (var deck in _decks.Values)
            deck.Tick(dt);
        RunCapture(dt, snapshot);
        effects.CampOrders.AddRange(_camps.UpdateAlerts(snapshot.Lanes));
        effects.Heals.AddRange(_regen.Tick(dt));
        effects.PoisonTicks.AddRange(_poison.Tick(dt));
        SpawnUnits(dt, effects);
        effects.Volleys.AddRange(_fire.Tick(dt, snapshot.TowerTargets));
        RunAi();
        IssueLaneCommands(effects);
        return Finish(effects);
    }

    private bool TimeIsUp()
    {
        if (!(_timeLimitMinutes > 0f))
            return false;

        if (!_timeWarned && _clock.Minute >= _timeLimitMinutes - TimeVictory.WarningMinutes)
        {
            _timeWarned = true;
            if (_clock.Minute < _timeLimitMinutes)
                _pendingEvents.Add(new MatchEvent(MatchEventKind.TimeRunningOut, MatchSide.West, string.Empty, 0));
        }

        if (_clock.Minute < _timeLimitMinutes)
            return false;

        var west = _chain.Standing(MatchSide.West);
        var east = _chain.Standing(MatchSide.East);
        _winner = TimeVictory.Winner(_chain);
        _endedOnTime = true;
        _pendingEvents.Add(new MatchEvent(MatchEventKind.TimeUp, _winner.Value, $"west={west.Towers}/{west.HitPointShare:0.000} east={east.Towers}/{east.HitPointShare:0.000}", 0));
        _clock.End();
        return true;
    }

    private void AnnounceWear()
    {
        if (_wearAnnounced || !_wear.HasBegun(_clock.Minute))
            return;

        _wearAnnounced = true;
        _pendingEvents.Add(new MatchEvent(MatchEventKind.WallsWeaken, MatchSide.West, string.Empty, 0));
    }

    public CommandResult TryBuyPack(int slot, int lane, string offerId)
    {
        var gate = CheckLane(slot, lane, out var owner);
        if (gate != CommandResult.Ok)
            return gate;

        var offer = UnitOffersOf(owner!.Side).FirstOrDefault(o => o.OfferId == offerId);
        if (offer == null)
            return CommandResult.UnknownOffer;
        if (_ledger.Balance(slot) < offer.PackPrice)
            return CommandResult.NotEnoughGold;

        var queued = _waves.TryQueuePack(owner.Side, lane, slot, offer);
        if (queued != CommandResult.Ok)
            return queued;

        _ledger.TrySpend(slot, offer.PackPrice, GoldUse.Units);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.PackBought, owner.Side, offer.OfferId, offer.PackPrice));
        return CommandResult.Ok;
    }

    /// <summary>Shows the named region of the slot's rotation now, for its fee. The region already showing costs nothing.</summary>
    public CommandResult TryCallRegion(int slot, string regionId)
    {
        if (!_slotsById.TryGetValue(slot, out var owner))
            return CommandResult.LaneNotOwned;
        if (!_decks.TryGetValue(owner.Side, out var deck) || deck.Regions.All(r => r.Id != regionId))
            return CommandResult.UnknownOffer;
        if (_clock.Phase == MatchPhase.Ended)
            return CommandResult.NotLive;
        if (deck.Current.Id == regionId)
            return CommandResult.Ok;
        if (!_ledger.TrySpend(slot, deck.CallFee, GoldUse.Regions))
            return CommandResult.NotEnoughGold;

        deck.Call(regionId);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.RegionCalled, owner.Side, deck.Current.Name, deck.CallFee));
        return CommandResult.Ok;
    }

    /// <summary>Calls a creature into the lane: pays for it, queues it like a bought pack, and starts the slot's shared timer.</summary>
    public CommandResult TryCallCreature(int slot, int lane, string offerId)
    {
        var gate = CheckLane(slot, lane, out var owner);
        if (gate != CommandResult.Ok)
            return gate;

        var side = owner!.Side;
        var call = _factions.TryGetValue(side, out var faction) ? faction.Creatures : null;
        var offer = call?.Offers.FirstOrDefault(o => o.OfferId == offerId && !_lostPacks.Contains((side, o.OfferId)));
        if (offer == null)
            return CommandResult.UnknownOffer;
        if (CreatureCooldownLeft(slot) > 0f)
            return CommandResult.OnCooldown;
        if (_ledger.Balance(slot) < offer.PackPrice)
            return CommandResult.NotEnoughGold;

        var queued = _waves.TryQueuePack(side, lane, slot, offer);
        if (queued != CommandResult.Ok)
            return queued;

        _ledger.TrySpend(slot, offer.PackPrice, GoldUse.Creatures);
        _creatureReadyAt[slot] = _clock.Elapsed + call!.CooldownSeconds;
        _pendingEvents.Add(new MatchEvent(MatchEventKind.CreatureCalled, side, offer.TroopId, offer.PackPrice));
        return CommandResult.Ok;
    }

    private float CreatureCooldownLeft(int slot) =>
        _creatureReadyAt.TryGetValue(slot, out var readyAt) ? Math.Max(0f, readyAt - _clock.Elapsed) : 0f;

    public CommandResult TryBuyHero(int slot, int lane, string heroId)
    {
        var gate = CheckLane(slot, lane, out var owner);
        if (gate != CommandResult.Ok)
            return gate;

        var offer = HeroOffersOf(owner!.Side).FirstOrDefault(h => h.HeroId == heroId);
        if (offer == null)
            return CommandResult.UnknownOffer;
        if (_ledger.Balance(slot) < offer.Price)
            return CommandResult.NotEnoughGold;

        var fielded = _heroes.TryField(owner.Side, offer.HeroId, slot, lane, offer.Price);
        if (fielded != CommandResult.Ok)
            return fielded;

        _ledger.TrySpend(slot, offer.Price, GoldUse.Heroes);
        QueueFieldedHero(owner.Side, offer, offer.Price);
        return CommandResult.Ok;
    }

    public CommandResult TryBuyback(int slot, string heroId)
    {
        var refusal = CheckBuyback(slot, heroId, out var owner);
        if (refusal != CommandResult.Ok)
            return refusal;

        var price = _heroes.BuybackPrice(owner!.Side, heroId, _clock.Minute);
        if (_ledger.Balance(slot) < price)
            return CommandResult.NotEnoughGold;

        var result = _heroes.TryBuyback(owner.Side, heroId, _clock.Elapsed);
        if (result == CommandResult.Ok)
        {
            _ledger.TrySpend(slot, price, GoldUse.Heroes);
            _buybackPaid[(owner.Side, heroId)] = price;
        }

        return result;
    }

    /// <summary>Every buyback refusal except gold, in the order <see cref="TryBuyback"/> reports them. The view's CanBuyback is this being Ok.</summary>
    private CommandResult CheckBuyback(int slot, string heroId, out CommanderSlot? owner)
    {
        _slotsById.TryGetValue(slot, out owner);
        if (_clock.Phase != MatchPhase.Live)
            return CommandResult.NotLive;
        if (owner == null)
            return CommandResult.LaneNotOwned;

        var hero = _heroes.Get(owner.Side, heroId);
        if (hero == null || !hero.CanBuyback(_clock.Elapsed))
            return CommandResult.HeroNotDead;
        if (hero.OwnerSlot != slot)
            return CommandResult.LaneNotOwned;

        return _heroes.AliveCount(owner.Side) >= _heroes.MaxAlive(owner.Side) ? CommandResult.HeroLimit : CommandResult.Ok;
    }

    public CommandResult TrySetLaneOrder(int slot, int lane, LaneOrder order)
    {
        var gate = CheckLane(slot, lane, out var owner);
        if (gate != CommandResult.Ok)
            return gate;

        var side = owner!.Side;
        if (order == LaneOrder.Seize && _orders.Order(side, lane) != LaneOrder.Seize && !_seize.HasOpen(side, lane))
            return CommandResult.NothingToSeize;

        var result = _orders.TrySet(side, lane, order, _clock.Elapsed);
        if (result != CommandResult.Ok)
            return result;

        if (order == LaneOrder.Seize)
            _seize.Start(side, lane, _seenLanes.TryGetValue((side, lane), out var seen) ? seen.Center : null);
        else
            _seize.Stop(side, lane);
        return CommandResult.Ok;
    }

    /// <summary>Adds gold to a slot outside the economy (the dev console's grant); the purse still stops at MaxGold. Returns what was added.</summary>
    public int GrantGold(int slot, int amount) => _ledger.Earn(slot, amount, GoldSource.Granted);

    /// <summary>The engine layer calls this once per spawned agent. The request id lets the match see when a charged request is fully out.</summary>
    public void RegisterUnit(int requestId, UnitRecord unit)
    {
        _units[unit.Key] = unit;
        _recentDeaths.Forget(unit.Key);
        NoteSpawned(requestId);
    }

    /// <summary>
    /// A registered agent died. Pays the killer's owner (a hero victim pays the hero bounty), puts a dead hero on its
    /// respawn timer. A killer that died earlier still counts while it is in the bounded death record. Unknown victims,
    /// unknown killers and kills within a side pay nothing; a victim is handled once.
    /// </summary>
    public KillCredit OnUnitKilled(AgentKey victim, AgentKey? killer)
    {
        if (!_units.TryGetValue(victim, out var dead))
            return KillCredit.Unregistered;

        _units.Remove(victim);
        _poison.Forget(victim);
        if (_clock.Phase == MatchPhase.Ended)
            return KillCredit.AfterEnd;

        if (dead.HeroId != null && _heroes.OnDied(dead.Side, dead.HeroId, _clock.Elapsed, _clock.Minute))
            _pendingEvents.Add(new MatchEvent(MatchEventKind.HeroDied, dead.Side, dead.HeroId, 0));

        // Look the killer up before this death enters the record, so the victim cannot push the killer out of a full record.
        var killerSide = MatchSide.West;
        var killerSlot = -1;
        var killerKnown = killer is { } key && TryFindKiller(key, out killerSide, out killerSlot);
        _recentDeaths.Record(victim, dead.Side, dead.OwnerSlot);
        if (!killerKnown)
            return KillCredit.KillerUnknown;
        if (killerSide == dead.Side)
            return KillCredit.SameSide;

        var bounty = dead.HeroId != null ? _bounty.HeroKillBounty : _bounty.KillBounty(dead.Value);
        var factor = _factions.TryGetValue(killerSide, out var killerFaction) ? killerFaction.FeatsOrNone.Bounty : 1f;
        _ledger.Earn(killerSlot, (int)Math.Round(bounty * factor, MidpointRounding.AwayFromZero), GoldSource.Bounty);
        return KillCredit.Paid;
    }

    /// <summary>
    /// A camp's creature is gone. When it was the last one the camp is cleared: the killer's side is paid and hears of it (a killer
    /// the match does not know pays nobody). Returns the cleared camp, null while units are left and after the match.
    /// </summary>
    private int CampRewardFor(MatchSide side, int reward) =>
        _factions.TryGetValue(side, out var faction) ? (int)Math.Round(reward * faction.FeatsOrNone.CampReward, MidpointRounding.AwayFromZero) : reward;

    public CampCleared? OnCampUnitKilled(string campId, AgentKey? killer)
    {
        if (_clock.Phase == MatchPhase.Ended)
            return null;

        MatchSide? side = null;
        if (killer is { } key && TryFindKiller(key, out var found, out _))
            side = found;

        var cleared = _camps.OnUnitKilled(campId, side);
        if (cleared?.Side is { } paid && cleared.Slot >= 0)
        {
            var reward = CampRewardFor(paid, cleared.Reward);
            _ledger.Earn(cleared.Slot, reward, GoldSource.Camps);
            _pendingEvents.Add(new MatchEvent(MatchEventKind.CampCleared, paid, campId, reward));
        }

        return cleared;
    }

    /// <summary>A living registered unit, else one from the bounded record of recent deaths (the killer died first).</summary>
    private bool TryFindKiller(AgentKey key, out MatchSide side, out int ownerSlot)
    {
        if (_units.TryGetValue(key, out var alive))
        {
            side = alive.Side;
            ownerSlot = alive.OwnerSlot;
            return true;
        }

        return _recentDeaths.TryGet(key, out side, out ownerSlot);
    }

    /// <summary>
    /// A tower or keep entity was destroyed. The destroying side's slots share the tower bounty (the killer does not change
    /// that split); a keep ends the match for the other side. Repeated or unknown ids do nothing.
    /// </summary>
    public void OnTowerDestroyed(string towerId, AgentKey? killer)
    {
        var slot = string.IsNullOrEmpty(towerId) ? null : _chain.Slot(towerId);
        if (slot == null || _clock.Phase == MatchPhase.Ended || !_fallen.Add(towerId))
            return;

        _chain.OnDestroyed(towerId);
        var scorer = MatchSides.Opponent(slot.Owner);
        if (slot.Kind == TowerKind.Keep)
        {
            _pendingEvents.Add(new MatchEvent(MatchEventKind.KeepDestroyed, slot.Owner, towerId, 0));
            _winner = scorer;
            _clock.End();
            return;
        }

        var share = _bounty.TowerBountyPerSlot(SlotsOf(scorer).Count);
        foreach (var commander in SlotsOf(scorer))
            _ledger.Earn(commander.Id, share, GoldSource.Towers);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.TowerDestroyed, slot.Owner, towerId, share));
    }

    /// <summary>
    /// The tower hit hook: the damage the entity should really take. An unknown id comes back unchanged; a locked or
    /// destroyed tower takes 0. Applied damage is mirrored into the rules and restarts the tower's regeneration delay.
    /// </summary>
    public int AdjustTowerDamage(TowerHit hit) => AdjustTowerDamage(hit, out _);

    /// <summary>Same as <see cref="AdjustTowerDamage(TowerHit)"/>, and says which branch of the table decided.</summary>
    public int AdjustTowerDamage(TowerHit hit, out TowerDamageReason reason)
    {
        var effective = Judge(hit, out reason);
        if (reason == TowerDamageReason.Applied)
        {
            var wear = _wear.Factor(_clock.Minute);
            effective = (int)Math.Round(effective * wear, MidpointRounding.AwayFromZero);
            if (hit.AttackerRole != UnitRole.Hero && hit.AttackerRole != UnitRole.Siege)
            {
                effective = _assaultLimit.Take(hit.TowerId, effective, _clock.Elapsed, wear);
                if (effective == 0)
                    reason = TowerDamageReason.Crowded;
            }
        }

        if (effective > 0)
        {
            _chain.ApplyDamage(hit.TowerId, effective);
            _regen.NotifyDamaged(hit.TowerId);
        }

        return effective;
    }

    /// <summary>Which branch of the damage table a blow takes. Reads the match and changes nothing: no hit points, no regeneration clock.</summary>
    public TowerDamageReason ReasonFor(TowerHit hit)
    {
        Judge(hit, out var reason);
        return reason;
    }

    // The table's verdict with no side effect: the damage the tower would take, and the branch that decided.
    private int Judge(TowerHit hit, out TowerDamageReason reason)
    {
        // The hit hook stays installed through the result window, and a keep that fell there would read as the way the match ended.
        if (_clock.Phase == MatchPhase.Ended)
        {
            reason = TowerDamageReason.MatchEnded;
            return 0;
        }

        if (hit == null)
        {
            reason = TowerDamageReason.RawZero;
            return 0;
        }

        if (!IsTower(hit.TowerId))
        {
            reason = TowerDamageReason.UnknownTower;
            return hit.RawDamage;
        }

        return _damage.Decide(hit, out reason);
    }

    private MatchEffects Finish(MatchEffects effects)
    {
        effects.Events.AddRange(_pendingEvents);
        _pendingEvents.Clear();
        effects.Winner = _winner;
        return effects;
    }

    private void Observe(MatchSnapshot snapshot)
    {
        _seenLanes.Clear();
        foreach (var lane in snapshot.Lanes)
            _seenLanes[(lane.Side, lane.Lane)] = lane;

        _seenThreats.Clear();
        foreach (var entry in snapshot.TowerTargets)
            _seenThreats[entry.TowerId] = entry.EnemiesInRange.Count;

        _seenLiveBySide.Clear();
        foreach (var side in Sides)
        {
            _seenLiveBySide[side] = snapshot.LiveBySide.TryGetValue(side, out var live)
                ? live
                : snapshot.Lanes.Where(l => l.Side == side).Sum(l => l.LiveUnits);
        }
    }

    private int LiveInLane(MatchSide side, int lane) =>
        _seenLanes.TryGetValue((side, lane), out var seen) ? seen.LiveUnits : 0;

    /// <summary>What the scheduler measures room against: the live units plus the ordinary units issued but not yet spawned.</summary>
    private int CommittedInLane(MatchSide side, int lane) => LiveInLane(side, lane) + ReservedInLane(side, lane);

    private int CommittedOnSide(MatchSide side) => LiveOnSide(side) + ReservedOnSide(side);

    private int LiveOnSide(MatchSide side) => _seenLiveBySide.TryGetValue(side, out var live) ? live : 0;

    private void PayPassiveIncome(float dt)
    {
        foreach (var slot in _slots)
        {
            var multiplier = _profiles.TryGetValue(slot.Id, out var profile) ? profile.IncomeMultiplier : 1f;
            if (_factions.TryGetValue(slot.Side, out var faction))
                multiplier *= faction.FeatsOrNone.Income;
            var carry = _incomeCarry[slot.Id] + (double)_config.Economy.IncomePerMinute * multiplier * dt / 60d;
            var whole = (int)Math.Floor(carry + 1e-6);
            _incomeCarry[slot.Id] = Math.Max(0d, carry - whole);
            if (whole > 0)
                _ledger.Earn(slot.Id, whole, GoldSource.Income);
        }
    }

    private void RunCapture(float dt, MatchSnapshot snapshot)
    {
        foreach (var change in _capture.Tick(dt, snapshot.Capture))
            _pendingEvents.Add(new MatchEvent(MatchEventKind.PointCaptured, change.NewOwner, change.PointId, 0));

        foreach (var side in Sides)
        {
            var income = _capture.CollectIncome(side, dt, _factions.TryGetValue(side, out var faction) ? faction.FeatsOrNone.CaptureIncome : 1f);
            if (income <= 0)
                continue;

            // An uneven split hands the remainder out one gold at a time, first slot first.
            var slots = SlotsOf(side);
            for (var i = 0; i < slots.Count; i++)
                _ledger.Earn(slots[i].Id, income / slots.Count + (i < income % slots.Count ? 1 : 0), GoldSource.Captures);
        }
    }

    private void SpawnUnits(float dt, MatchEffects effects)
    {
        RunHeroArrivals();
        effects.Spawns.AddRange(_pendingSpawns);
        _pendingSpawns.Clear();
        foreach (var request in _waves.Tick(dt, _clock.Minute, CommittedInLane, CommittedOnSide, _chain.LaneLost))
        {
            effects.Spawns.Add(request);
            Reserve(request);
            if (_waves.TakePackOffer(request.RequestId) is { } bought)
                TrackCharge(request, 0, bought);
        }

        foreach (var respawn in _heroes.DueRespawns(_clock.Elapsed, _waves.NextRequestId))
        {
            effects.Spawns.Add(respawn);
            TrackCharge(respawn, TakeBuybackPaid(respawn.Side, respawn.TroopId), null);
            _pendingEvents.Add(new MatchEvent(MatchEventKind.HeroRespawned, respawn.Side, respawn.TroopId, 0));
        }
    }

    private void RunAi()
    {
        foreach (var (slot, ai) in _ais)
        {
            if (!ai.IsDue(_clock.Elapsed))
                continue;

            foreach (var action in ai.Decide(GetView(slot), _clock.Elapsed))
                Apply(slot, action);
        }
    }

    private void Apply(int slot, AiAction action)
    {
        switch (action.Kind)
        {
            case AiActionKind.BuyPack:
                TryBuyPack(slot, action.Lane, action.Id);
                break;
            case AiActionKind.BuyHero:
                TryBuyHero(slot, action.Lane, action.Id);
                break;
            case AiActionKind.Buyback:
                TryBuyback(slot, action.Id);
                break;
            case AiActionKind.SetOrder:
                TrySetLaneOrder(slot, action.Lane, action.Order);
                break;
            case AiActionKind.CallCreature:
                TryCallCreature(slot, action.Lane, action.Id);
                break;
            case AiActionKind.BuyUpgrade:
                TryBuyUpgrade(slot, action.Id);
                break;
        }
    }

    /// <summary>The standing tower nearest the middle of the lane; the standing keep once the lane's towers are gone.</summary>
    private TowerSlot? NextTargetOf(MatchSide owner, int lane)
    {
        var tower = _chain.AliveTowers(owner, lane).FirstOrDefault();
        if (tower != null)
            return tower;

        foreach (var id in _chain.Ids)
        {
            var slot = _chain.Slot(id);
            if (slot != null && slot.Kind == TowerKind.Keep && slot.Owner == owner && !_chain.IsDestroyed(id))
                return slot;
        }

        return null;
    }

    /// <summary>Validates a command that targets a lane: live match, a known slot, a real lane, and a lane the slot commands.</summary>
    private CommandResult CheckLane(int slot, int lane, out CommanderSlot? owner)
    {
        owner = null;
        if (_clock.Phase != MatchPhase.Live)
            return CommandResult.NotLive;
        if (!_slotsById.TryGetValue(slot, out var commander))
            return CommandResult.LaneNotOwned;
        if (lane < 0 || lane >= _layout.Lanes.Count)
            return CommandResult.UnknownLane;
        if (!commander.Lanes.Contains(lane))
            return CommandResult.LaneNotOwned;

        owner = commander;
        return CommandResult.Ok;
    }

    private int LaneOwner(MatchSide side, int lane)
    {
        CommanderSlot? fallback = null;
        foreach (var slot in _slots)
        {
            if (slot.Side != side)
                continue;
            if (slot.Lanes.Contains(lane))
                return slot.Id;
            fallback ??= slot;
        }

        return fallback?.Id ?? -1;
    }

    private List<CommanderSlot> SlotsOf(MatchSide side) => _slots.Where(s => s.Side == side).ToList();

    /// <summary>The side's pack offers, without the ones whose spawn failed (see <see cref="OnSpawnFailed"/>).</summary>
    private IReadOnlyList<UnitOffer> UnitOffersOf(MatchSide side) =>
        _factions.TryGetValue(side, out var faction)
            ? faction.Units.Concat(_decks.TryGetValue(side, out var deck) ? deck.Current.Units : Array.Empty<UnitOffer>())
                .Where(o => !_lostPacks.Contains((side, o.OfferId))).ToList()
            : Array.Empty<UnitOffer>();

    /// <summary>The side's hero offers, without the heroes whose spawn failed (see <see cref="OnSpawnFailed"/>).</summary>
    private IReadOnlyList<HeroOffer> HeroOffersOf(MatchSide side) =>
        _factions.TryGetValue(side, out var faction)
            ? faction.Heroes.Where(h => !_lostHeroes.Contains((side, h.HeroId))).ToList()
            : Array.Empty<HeroOffer>();

    private FordsAiProfile ProfileFor(CommanderSlot slot)
    {
        var profiles = _config.AiProfiles;
        if (slot.AiProfileId != null && profiles.TryGetValue(slot.AiProfileId, out var chosen))
            return chosen;
        return profiles.TryGetValue("normal", out var normal) ? normal : FordsMatchConfig.Default.AiProfiles["normal"];
    }
}
