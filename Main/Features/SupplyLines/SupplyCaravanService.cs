using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SupplyLines.Components;
using TAOM.Features.SupplyLines.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Engine-boundary implementation of <see cref="ISupplyCaravanService"/>: owns the caravan map
/// parties end to end (spawn, teleport-along-path movement, escort release, teardown, post-load
/// repair).
///
/// <para>Hardened relative to the source module: party lookups go through a per-order cache
/// refreshed on spawn/destroy/load instead of a per-frame LINQ over <c>MobileParty.All</c>; the
/// native position set is skipped when the smoothed position barely moved; the private
/// <c>MobileParty.Bearing</c> setter is resolved once through AccessTools and tolerated as absent
/// (log once, never throw); and escort release happens BEFORE the party is destroyed, because the
/// source module's destroy-first ordering cleared the hero's party link and stranded companions.</para>
/// </summary>
public sealed class SupplyCaravanService : ISupplyCaravanService
{
    private sealed class CaravanTracker
    {
        public SupplyOrder Order;
        public MobileParty Party;
        public List<Vec2> PathPoints;
        // Cumulative arc length per path point, rebuilt with PathPoints: the per-frame
        // point-at-fraction lookup reads these instead of re-walking every segment's sqrt.
        public readonly List<float> CumulativeLengths = new List<float>();
        // Route origin, resolved at repath cadence (or straight off the order's persisted
        // dispatch origin), never per frame.
        public Vec2 Origin;
        public bool OriginResolved;
        public Vec2 PathEndPlayerPos;
        public Vec2 SmoothPos;
        public bool SmoothInitialized;
        public Vec2 LastAppliedPos;
        public bool HasAppliedPos;
    }

    private const string CaravanIdPrefix = "taom_supply_caravan_";

    // Vanilla food model: 20 men on the map eat one food per day
    // (DefaultMobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood, verified on the
    // installed 1.4.8). The caravan is NOT IsCaravan (custom component), so DoesPartyConsumeFood
    // returns true for it and an escorted goods-less order would starve silently for the whole
    // transit (review round B); Spawn stocks provisions to cover the worst-case trip.
    private const int MenPerDailyFood = 20;

    // Longest possible transit is the force-deliver failsafe at 1.5x planned hours
    // (SupplyOrderEngine.ForceDeliverFraction; keep in sync).
    private const float WorstCaseTransitFactor = 1.5f;

    // An engine DEFAULT item (DefaultItems.RegisterAll creates "grain" in code with
    // isFood: true, verified in the 1.4.8 dump), so it exists in every campaign regardless of
    // module data; the null-guard in StockProvisions is belt only.
    private const string ProvisionItemId = "grain";

    // The path from source to player is recomputed only when the player has drifted this far from
    // the endpoint it was computed against (source module value).
    private const float RepathWhenPlayerMoved = 2.5f;

    // Exponential smoothing factor for the teleport position, so the icon glides instead of popping.
    private const float PositionSmoothingFactor = 0.2f;

    // A frame whose smoothed position moved less than this skips the native Position set entirely.
    private const float MinPositionDelta = 0.01f;

    // Past this travel fraction the caravan is closing on the player and the bearing is left alone
    // so the final approach does not visibly oscillate (source module value).
    private const float BearingSuppressedPastFraction = 0.88f;

    // Compiled open delegate over the non-public MobileParty.Bearing setter: this runs per
    // caravan per frame, and MethodInfo.Invoke would box the Vec2 into a fresh object[] each
    // call. Resolution still goes through AccessTools.PropertySetter (pinned by
    // SupplyCaravanBearingBindingTests); the delegate is compiled once from that MethodInfo.
    private static Action<MobileParty, Vec2> _bearingSetter;
    private static bool _bearingResolveAttempted;
    private static bool _bearingWarned;

    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly IModLogger _logger;

    // orderId -> live caravan state. Refreshed on Spawn, ReleaseEscortAndDestroy and RespawnMissing;
    // this is what keeps the per-frame tick free of MobileParty.All scans.
    private readonly Dictionary<string, CaravanTracker> _caravans = new Dictionary<string, CaravanTracker>();

    private bool _positionSetWarned;

    public SupplyCaravanService(ISupplyLinesSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public string Spawn(SupplyOrder order)
    {
        if (order == null)
            return null;

        MobileParty party = null;
        try
        {
            Settlement home;
            CampaignVec2 position;
            CultureObject culture;

            if (order.IsFromLord)
            {
                var lord = FindHero(order.SourceHeroId);
                var lordParty = lord?.PartyBelongedTo;
                if (order.HasDispatchOrigin)
                {
                    // Respawn (or any spawn after the origin was recorded): the caravan comes
                    // back at the DISPATCH position, not wherever the lord marched to since,
                    // and the lord losing his party no longer strands the order.
                    position = new CampaignVec2(
                        new Vec2(order.DispatchOriginX, order.DispatchOriginY), isOnLand: true);
                }
                else if (lordParty != null)
                {
                    position = lordParty.Position;
                }
                else
                {
                    _logger.LogWarning($"[SupplyLines] Spawn: lord '{order.SourceHeroId}' has no party and no dispatch origin, order {order.OrderId} not spawned");
                    return null;
                }
                culture = lord?.Culture ?? Hero.MainHero?.Culture;
                home = lord?.HomeSettlement ?? Hero.MainHero?.HomeSettlement;
            }
            else
            {
                var settlement = Settlement.Find(order.SourceSettlementId);
                if (settlement == null)
                {
                    _logger.LogWarning($"[SupplyLines] Spawn: settlement '{order.SourceSettlementId}' not found, order {order.OrderId} not spawned");
                    return null;
                }
                position = order.HasDispatchOrigin
                    ? new CampaignVec2(new Vec2(order.DispatchOriginX, order.DispatchOriginY), isOnLand: true)
                    : settlement.GatePosition;
                culture = settlement.Culture;
                home = settlement;
            }

            // First spawn records the origin; every later path build and respawn anchors here.
            if (!order.HasDispatchOrigin)
                order.SetDispatchOrigin(position.X, position.Y);

            var component = new SupplyCaravanComponent(order.OrderId, home);
            party = MobileParty.CreateParty(CaravanIdPrefix + order.OrderId, component);

            var template = PickCaravanTemplate(culture);
            if (template != null)
                party.InitializeMobilePartyAtPosition(template, position);
            else
                party.InitializeMobilePartyAtPosition(position);

            FillCargo(party, order);
            if (order.EscortEnum == SupplyEscortOption.Mercenaries)
                AddMercenaryGuards(party);
            else if (order.EscortEnum == SupplyEscortOption.Companion)
                AttachCompanionEscort(party, order);

            // With the roster final: record which aboard troops are NOT cargo (template guards,
            // mercenary escort) so delivery can tell them from purchased recruits sharing a
            // character id, and stock food for the worst-case transit so the escort does not
            // starve silently (the party is not IsCaravan, so vanilla feeds it nothing).
            RecordNonCargoManifest(party, order);
            StockProvisions(party, order);

            party.ActualClan = Clan.PlayerClan;
            party.Aggressiveness = 0f;
            party.SetMoveModeHold();
            PinAi(party);

            // CreateParty may uniquify the requested id, so the order records what it actually got.
            order.CaravanPartyId = party.StringId;
            order.StatusEnum = SupplyOrderStatus.InTransit;
            _caravans[order.OrderId] = new CaravanTracker { Order = order, Party = party };
            return party.StringId;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] Spawn failed for order {order.OrderId}: {ex}");
            // Escort FIRST, exactly like ReleaseEscortAndDestroy: if the throw landed after
            // AttachCompanionEscort, destroying the party would null the companion's party
            // binding and strand him. ReleaseCompanion tolerates every partial state.
            ReleaseCompanion(order, party);
            TryDestroyHalfSpawnedParty(party);
            return null;
        }
    }

    public bool CaravanExists(SupplyOrder order)
    {
        var party = TrackedParty(order);
        return party != null && party.IsActive;
    }

    public bool CaravanInMapEvent(SupplyOrder order)
    {
        // ANY map event, deliberately not IsRaid: a caravan attacked in the field sits in a
        // FieldBattle event, which the old IsRaid check could never see (round-A HIGH). While
        // this is true the order Continues; a defeat destroys the party and resolves as a loss
        // through CaravanExists.
        var party = TrackedParty(order);
        return party?.MapEvent != null;
    }

    public bool TryGetLiveCargo(
        SupplyOrder order,
        out IReadOnlyDictionary<string, int> goods,
        out IReadOnlyDictionary<string, int> troops)
    {
        goods = null;
        troops = null;
        var party = TrackedParty(order);
        if (party == null || !party.IsActive)
            return false;
        try
        {
            var liveGoods = new Dictionary<string, int>();
            var itemRoster = party.ItemRoster;
            if (itemRoster != null)
            {
                for (int i = 0; i < itemRoster.Count; i++)
                {
                    var element = itemRoster.GetElementCopyAtIndex(i);
                    var item = element.EquipmentElement.Item;
                    if (item?.StringId == null || element.Amount <= 0)
                        continue;
                    liveGoods.TryGetValue(item.StringId, out int existing);
                    liveGoods[item.StringId] = existing + element.Amount;
                }
            }

            var liveTroops = new Dictionary<string, int>();
            var memberRoster = party.MemberRoster;
            if (memberRoster != null)
            {
                for (int i = 0; i < memberRoster.Count; i++)
                {
                    var troop = memberRoster.GetCharacterAtIndex(i);
                    // Heroes are never cargo: the companion escort goes home through
                    // ReleaseEscortAndDestroy, not through delivery.
                    if (troop?.StringId == null || troop.IsHero)
                        continue;
                    int count = memberRoster.GetElementNumber(i);
                    if (count <= 0)
                        continue;
                    liveTroops.TryGetValue(troop.StringId, out int existing);
                    liveTroops[troop.StringId] = existing + count;
                }
            }

            goods = liveGoods;
            // Guards and template troops are not cargo even when they share a character id with
            // a purchased recruit: subtract the spawn-time manifest so a guard survivor is never
            // delivered as a recruit (Codex round 2 #6). Casualties bill against cargo first.
            troops = SubtractNonCargo(liveTroops, order.NonCargoTroops);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[SupplyLines] live cargo read failed for order {order.OrderId}: {ex.Message}");
            goods = null;
            troops = null;
            return false;
        }
    }

    public void ClearTrackers()
    {
        _caravans.Clear();
    }

    public float DistanceToPlayer(SupplyOrder order)
    {
        // Straight-line distance on purpose: this feeds the per-frame delivery-proximity check
        // against a range of a couple of map units, where nav-mesh distance buys nothing and a
        // pathfinding call per order per frame would be the exact cost this service avoids.
        var party = TrackedParty(order);
        var mainParty = MobileParty.MainParty;
        if (party == null || !party.IsActive || mainParty == null)
            return float.MaxValue;
        float distance = party.GetPosition2D.Distance(mainParty.GetPosition2D);
        return FiniteFloatValidator.IsFinite(distance) ? distance : float.MaxValue;
    }

    public void TickPositions()
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null || _caravans.Count == 0)
            return;
        Vec2 playerPos = mainParty.GetPosition2D;

        foreach (var tracker in _caravans.Values)
        {
            var order = tracker.Order;
            if (order == null || order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;
            var party = tracker.Party;
            if (party == null || !party.IsActive)
                continue;
            if (party.MapEvent != null)
                continue; // the engine owns the party while it fights

            // Origin resolution and path geometry happen only at repath cadence; the per-frame
            // work below reads tracker fields exclusively (no object lookups, no path re-walks).
            if (tracker.PathPoints == null
                || tracker.PathPoints.Count == 0
                || tracker.PathEndPlayerPos.Distance(playerPos) > RepathWhenPlayerMoved)
            {
                if (!TryGetOrigin(order, out Vec2 origin))
                    continue;
                tracker.Origin = origin;
                tracker.OriginResolved = true;
                tracker.PathPoints = ComputeNavPathPoints(origin, playerPos);
                tracker.PathEndPlayerPos = playerPos;
                ComputeCumulativeLengths(tracker.PathPoints, tracker.CumulativeLengths);
            }
            if (!tracker.OriginResolved)
                continue;

            float fraction = ClampFraction(order.ElapsedFraction());
            Vec2 target = PointAtFraction(tracker.PathPoints, tracker.CumulativeLengths, fraction);

            if (!tracker.SmoothInitialized)
            {
                tracker.SmoothPos = target;
                tracker.SmoothInitialized = true;
            }
            Vec2 previous = tracker.SmoothPos;
            tracker.SmoothPos = new Vec2(
                previous.x + (target.x - previous.x) * PositionSmoothingFactor,
                previous.y + (target.y - previous.y) * PositionSmoothingFactor);

            // Captured BEFORE the position write (which advances LastAppliedPos): one delta
            // gate shared by the position AND bearing setters. A frame whose smoothed position
            // barely moved cannot have meaningfully changed direction either, so the bearing
            // write is skipped with the position write instead of running per caravan per frame
            // (review round B).
            bool moved = !tracker.HasAppliedPos
                || tracker.SmoothPos.Distance(tracker.LastAppliedPos) >= MinPositionDelta;

            if (moved)
            {
                try
                {
                    party.Position = new CampaignVec2(tracker.SmoothPos, isOnLand: true);
                    tracker.LastAppliedPos = tracker.SmoothPos;
                    tracker.HasAppliedPos = true;
                }
                catch (Exception ex)
                {
                    if (!_positionSetWarned)
                    {
                        _positionSetWarned = true;
                        _logger.LogWarning($"[SupplyLines] caravan position set failed (logged once): {ex.Message}");
                    }
                }
            }

            if (moved && fraction < BearingSuppressedPastFraction)
                ApplyBearing(party, tracker, previous, tracker.Origin);
        }
    }

    public void ReleaseEscortAndDestroy(SupplyOrder order)
    {
        if (order == null)
            return;
        var party = TrackedParty(order);

        // Escort FIRST: destroying the party clears the hero's PartyBelongedTo, after which the
        // release check can never match and the companion is stranded (the source module's bug).
        ReleaseCompanion(order, party);

        if (party != null && party.IsActive)
        {
            try
            {
                DestroyPartyAction.Apply(null, party);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SupplyLines] destroy failed for caravan of order {order.OrderId}: {ex.Message}");
            }
        }
        _caravans.Remove(order.OrderId);
    }

    public void ForgetDestroyed(SupplyOrder order)
    {
        if (order == null)
            return;
        // The party is being destroyed BY THE ENGINE (MobilePartyDestroyed listener): calling
        // DestroyPartyAction again would double-destroy, and moving the companion mid-destroy
        // would fight the engine's own hero resolution (capture/release). Only the tracker is
        // dropped; the escort's fate is whatever the destroying battle decided.
        _caravans.Remove(order.OrderId);
    }

    public void RespawnMissing(IEnumerable<SupplyOrder> orders)
    {
        if (orders == null)
            return;

        Dictionary<string, MobileParty> partiesById = null;
        foreach (var order in orders)
        {
            if (order == null || order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;

            // Trackers were dropped when the book was loaded (LoadFrom → ClearTrackers), so
            // every order re-binds against the CURRENT campaign's party objects here. There is
            // deliberately no shortcut through a pre-existing tracker: a cached Party from the
            // previous session still reads IsActive == true and would freeze the real caravan
            // while a ghost travels (round-A HIGH).
            // One pass over MobileParty.All for the whole load, never per order.
            if (partiesById == null)
            {
                partiesById = new Dictionary<string, MobileParty>();
                foreach (var party in MobileParty.All)
                {
                    if (party?.StringId != null && !partiesById.ContainsKey(party.StringId))
                        partiesById[party.StringId] = party;
                }
            }

            if (!string.IsNullOrEmpty(order.CaravanPartyId)
                && partiesById.TryGetValue(order.CaravanPartyId, out var survivor)
                && survivor.IsActive)
            {
                // The save row must PROVE the party it names is this order's caravan before the
                // party is pinned, teleported and eventually destroyed on delivery: a damaged
                // or hostile row pointing at main_party, a refuge, or another order's caravan
                // would otherwise hand that party to the movement pass and to
                // DestroyPartyAction (Codex round 2 #3).
                if (survivor.PartyComponent is SupplyCaravanComponent component
                    && component.OrderId == order.OrderId)
                {
                    _caravans[order.OrderId] = new CaravanTracker { Order = order, Party = survivor };
                    PinAi(survivor); // AI pin does not survive a save round-trip
                    continue;
                }
                _logger.LogWarning(
                    $"[SupplyLines] order {order.OrderId} names party '{order.CaravanPartyId}', which is not its supply caravan; a fresh caravan will be spawned instead");
            }

            if (order.IsFromLord && !order.HasDispatchOrigin)
            {
                // A legacy lord order with no recorded origin has nowhere to respawn from; the
                // order service's next hourly tick sees the missing caravan and marks the order
                // Lost. Logged so the loss is attributable. Orders with a persisted origin fall
                // through to Spawn, which anchors at that origin.
                _logger.LogWarning($"[SupplyLines] lord-source caravan for order {order.OrderId} missing after load with no dispatch origin; order will be lost");
                continue;
            }

            if (Spawn(order) == null)
                _logger.LogWarning($"[SupplyLines] respawn failed for order {order.OrderId}; order will be lost");
        }
    }

    // --- spawn helpers ---

    private static PartyTemplateObject PickCaravanTemplate(CultureObject culture)
    {
        if (culture == null)
            return null;
        var templates = culture.CaravanPartyTemplates;
        if (templates != null && templates.Count > 0)
            return templates[0];
        return culture.DefaultPartyTemplate;
    }

    private void FillCargo(MobileParty party, SupplyOrder order)
    {
        foreach (var pair in order.Goods)
        {
            var item = MBObjectManager.Instance.GetObject<ItemObject>(pair.Key);
            if (item == null)
            {
                _logger.LogWarning($"[SupplyLines] Spawn: unknown item id '{pair.Key}' not loaded onto caravan");
                continue;
            }
            if (pair.Value > 0)
                party.ItemRoster.AddToCounts(item, pair.Value);
        }
        foreach (var pair in order.Recruits)
        {
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
            if (troop == null)
            {
                _logger.LogWarning($"[SupplyLines] Spawn: unknown troop id '{pair.Key}' not loaded onto caravan");
                continue;
            }
            if (pair.Value > 0)
                party.MemberRoster.AddToCounts(troop, pair.Value);
        }
    }

    private void AddMercenaryGuards(MobileParty party)
    {
        var guardTroop = Hero.MainHero?.Culture?.BasicTroop;
        int count = _settings.MercenaryGuardCount;
        if (guardTroop != null && count > 0)
            party.MemberRoster.AddToCounts(guardTroop, count);
    }

    /// <summary>
    /// Records the non-cargo troop counts on the order: everything aboard beyond the purchased
    /// recruits (template guards, mercenary escort), per character id. Recomputed on every
    /// spawn, respawns included, because the roster is rebuilt each time.
    /// </summary>
    private static void RecordNonCargoManifest(MobileParty party, SupplyOrder order)
    {
        var aboard = new Dictionary<string, int>();
        var roster = party?.MemberRoster;
        if (roster != null)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                var troop = roster.GetCharacterAtIndex(i);
                if (troop?.StringId == null || troop.IsHero)
                    continue;
                int count = roster.GetElementNumber(i);
                if (count <= 0)
                    continue;
                aboard.TryGetValue(troop.StringId, out int existing);
                aboard[troop.StringId] = existing + count;
            }
        }

        var manifest = new Dictionary<string, int>();
        foreach (var pair in aboard)
        {
            int ordered = 0;
            order.Recruits?.TryGetValue(pair.Key, out ordered);
            int nonCargo = pair.Value - ordered;
            if (nonCargo > 0)
                manifest[pair.Key] = nonCargo;
        }
        order.NonCargoTroops = manifest;
    }

    /// <summary>
    /// Removes the non-cargo counts from a live troop snapshot in place. A null manifest (an
    /// order saved before the field existed) leaves the snapshot untouched, which is the legacy
    /// guards-count-as-cargo behaviour for old saves only.
    /// </summary>
    internal static Dictionary<string, int> SubtractNonCargo(
        Dictionary<string, int> live, Dictionary<string, int> nonCargo)
    {
        if (live == null || nonCargo == null || nonCargo.Count == 0)
            return live;
        foreach (var pair in nonCargo)
        {
            if (!live.TryGetValue(pair.Key, out int aboard))
                continue;
            int remaining = aboard - pair.Value;
            if (remaining > 0)
                live[pair.Key] = remaining;
            else
                live.Remove(pair.Key);
        }
        return live;
    }

    private void StockProvisions(MobileParty party, SupplyOrder order)
    {
        int provisions = ComputeProvisionCount(party?.MemberRoster?.TotalManCount ?? 0, order.PlannedHours);
        if (provisions <= 0)
            return;
        var food = MBObjectManager.Instance.GetObject<ItemObject>(ProvisionItemId);
        if (food == null)
        {
            _logger.LogWarning(
                $"[SupplyLines] Spawn: provision item '{ProvisionItemId}' unknown; the caravan travels unprovisioned and its escort may starve");
            return;
        }
        party.ItemRoster.AddToCounts(food, provisions);
    }

    /// <summary>
    /// Food to load for the whole worst-case transit (force-deliver fires at 1.5x planned):
    /// vanilla feeds one food per 20 men per day, plus one spare for the fractional day. Pure
    /// and testable; non-finite or negative planned hours count as zero rather than poisoning
    /// the ceiling maths.
    /// </summary>
    internal static int ComputeProvisionCount(int memberCount, float plannedHours)
    {
        if (memberCount <= 0)
            return 0;
        if (!FiniteFloatValidator.IsFinite(plannedHours) || plannedHours < 0f)
            plannedHours = 0f;
        float worstCaseDays = plannedHours * WorstCaseTransitFactor / 24f;
        return (int)Math.Ceiling(worstCaseDays * memberCount / MenPerDailyFood) + 1;
    }

    private void AttachCompanionEscort(MobileParty party, SupplyOrder order)
    {
        if (string.IsNullOrEmpty(order.EscortHeroId))
            return;
        var hero = FindHero(order.EscortHeroId);
        if (hero == null || !hero.IsAlive)
        {
            _logger.LogWarning($"[SupplyLines] Spawn: escort hero '{order.EscortHeroId}' unavailable, caravan goes unescorted");
            return;
        }
        // Entity-state enumeration before mutating a hero on a load path: a captured, settled
        // or otherwise-employed escort stays where fate put him and the order continues
        // unescorted. Without these, a respawn after load yanked an imprisoned companion out of
        // his captor's prison roster (round-A MEDIUM, Entity State Matrix rule).
        if (hero.IsPrisoner)
        {
            _logger.LogWarning($"[SupplyLines] Spawn: escort hero '{order.EscortHeroId}' is a prisoner, caravan goes unescorted");
            return;
        }
        var currentParty = hero.PartyBelongedTo;
        if (currentParty != null && currentParty != MobileParty.MainParty)
        {
            _logger.LogWarning($"[SupplyLines] Spawn: escort hero '{order.EscortHeroId}' rides with another party, caravan goes unescorted");
            return;
        }
        if (currentParty == null && hero.CurrentSettlement != null)
        {
            _logger.LogWarning($"[SupplyLines] Spawn: escort hero '{order.EscortHeroId}' stays in a settlement, caravan goes unescorted");
            return;
        }
        AddHeroToPartyAction.Apply(hero, party, showNotification: false);
    }

    private static void PinAi(MobileParty party)
    {
        party?.Ai?.SetDoNotMakeNewDecisions(true);
    }

    // Heroes register with CampaignObjectManager only (Hero.cs:1467-1480, verified 1.4.8);
    // MBObjectManager.GetObject<Hero> reads XML type records and misses runtime/loaded heroes,
    // which is why every lord source silently resolved null (Codex round 2 #5). Item and
    // character lookups stay on MBObjectManager, which is correct for XML-defined objects.
    private static Hero FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);

    private void TryDestroyHalfSpawnedParty(MobileParty party)
    {
        if (party == null || !party.IsActive)
            return;
        try
        {
            DestroyPartyAction.Apply(null, party);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] cleanup of half-spawned caravan failed: {ex.Message}");
        }
    }

    // --- movement helpers ---

    private MobileParty TrackedParty(SupplyOrder order)
    {
        if (order == null)
            return null;
        return _caravans.TryGetValue(order.OrderId, out var tracker) ? tracker.Party : null;
    }

    private void ReleaseCompanion(SupplyOrder order, MobileParty caravanParty)
    {
        if (string.IsNullOrEmpty(order.EscortHeroId))
            return;
        var hero = FindHero(order.EscortHeroId);
        if (hero == null || !hero.IsAlive)
            return;
        if (caravanParty == null || hero.PartyBelongedTo != caravanParty)
            return; // captured, rescued or already elsewhere; moving them now would corrupt state
        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return;
        try
        {
            AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] escort release failed for order {order.OrderId}: {ex.Message}");
        }
    }

    private static bool TryGetOrigin(SupplyOrder order, out Vec2 origin)
    {
        // The persisted dispatch origin always wins: it is immutable, free to read (no object
        // lookups), and it keeps the route anchored where the caravan actually set out from
        // even after the source lord marched away (round-A / Codex P2). The lookups below are
        // the legacy fallback for orders recorded before the origin fields existed.
        if (order.HasDispatchOrigin)
        {
            origin = new Vec2(order.DispatchOriginX, order.DispatchOriginY);
            return true;
        }
        origin = default;
        if (order.IsFromLord)
        {
            var lordParty = FindHero(order.SourceHeroId)?.PartyBelongedTo;
            if (lordParty == null)
                return false;
            origin = lordParty.GetPosition2D;
            return true;
        }
        var settlement = Settlement.Find(order.SourceSettlementId);
        if (settlement == null)
            return false;
        origin = settlement.GetPosition2D;
        return true;
    }

    private static float ClampFraction(float fraction)
    {
        // A NaN fraction (corrupt save times) resolves to "arrived" rather than freezing the
        // caravan at its origin forever.
        if (!FiniteFloatValidator.IsFinite(fraction))
            return 1f;
        if (fraction < 0f)
            return 0f;
        return fraction > 1f ? 1f : fraction;
    }

    private void ApplyBearing(MobileParty party, CaravanTracker tracker, Vec2 previousSmoothPos, Vec2 origin)
    {
        // Face along the frame's actual movement; fall back to the overall route direction when the
        // frame barely moved, and never face backwards along the route (source module logic).
        var moveDir = new Vec2(
            tracker.SmoothPos.x - previousSmoothPos.x,
            tracker.SmoothPos.y - previousSmoothPos.y);
        var routeDir = new Vec2(
            tracker.PathEndPlayerPos.x - origin.x,
            tracker.PathEndPlayerPos.y - origin.y);
        Vec2 dir = moveDir.LengthSquared > 1E-06f ? moveDir : routeDir;
        if (routeDir.LengthSquared > 1E-06f && dir.DotProduct(routeDir) < 0f)
            dir = routeDir;
        if (dir.LengthSquared <= 1E-06f)
            return;
        SetBearing(party, dir.Normalized());
    }

    private void SetBearing(MobileParty party, Vec2 direction)
    {
        if (!_bearingResolveAttempted)
        {
            _bearingResolveAttempted = true;
            // Internal setter on MobileParty.Bearing; pinned by SupplyCaravanBearingBindingTests
            // so engine drift fails in CI instead of silently sliding caravans sideways here.
            // Compiled to an open delegate once: this runs per caravan per frame, and a
            // MethodInfo.Invoke would box the Vec2 into a fresh object[] every call.
            try
            {
                MethodInfo setter = AccessTools.PropertySetter(typeof(MobileParty), "Bearing");
                if (setter != null)
                    _bearingSetter = AccessTools.MethodDelegate<Action<MobileParty, Vec2>>(setter);
            }
            catch (Exception)
            {
                _bearingSetter = null;
            }
        }
        if (_bearingSetter == null)
        {
            if (!_bearingWarned)
            {
                _bearingWarned = true;
                _logger.LogWarning("[SupplyLines] MobileParty.Bearing setter did not resolve; caravan icons will not face their travel direction");
            }
            return;
        }
        try
        {
            _bearingSetter(party, direction);
        }
        catch (Exception ex)
        {
            _bearingSetter = null;
            if (!_bearingWarned)
            {
                _bearingWarned = true;
                _logger.LogWarning($"[SupplyLines] MobileParty.Bearing set threw, bearing disabled: {ex.Message}");
            }
        }
    }

    // --- nav path helpers (shared with SupplyRouteVisualService) ---

    internal static List<Vec2> ComputeNavPathPoints(Vec2 from, Vec2 to)
    {
        var points = new List<Vec2>();
        try
        {
            var mapScene = Campaign.Current?.MapSceneWrapper;
            if (mapScene != null)
            {
                var fromVec = new CampaignVec2(from, isOnLand: true);
                var toVec = new CampaignVec2(to, isOnLand: true);
                var path = new NavigationPath();
                if (mapScene.GetPathBetweenAIFaces(
                        fromVec.Face, toVec.Face, from, to, 0.1f, path, null, 1f, 100, 100)
                    && path.Size >= 2)
                {
                    for (int i = 0; i < path.Size; i++)
                        points.Add(path[i]);
                }
            }
        }
        catch (Exception)
        {
            points.Clear();
        }
        if (points.Count < 2)
        {
            points.Clear();
            points.Add(from);
            points.Add(to);
        }
        return points;
    }

    /// <summary>
    /// Cumulative arc length per path point (element i = distance from the start to point i),
    /// computed once per repath so the per-frame lookup never re-walks the path's square roots.
    /// </summary>
    internal static void ComputeCumulativeLengths(List<Vec2> path, List<float> cumulative)
    {
        cumulative.Clear();
        if (path == null || path.Count == 0)
            return;
        cumulative.Add(0f);
        float total = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            total += path[i].Distance(path[i + 1]);
            cumulative.Add(total);
        }
    }

    internal static Vec2 PointAtFraction(List<Vec2> path, List<float> cumulative, float fraction)
    {
        if (path == null || path.Count == 0)
            return new Vec2(0f, 0f);
        if (path.Count == 1 || fraction <= 0f)
            return path[0];
        if (fraction >= 1f || cumulative == null || cumulative.Count != path.Count)
            return path[path.Count - 1];

        float totalLength = cumulative[cumulative.Count - 1];
        if (totalLength <= 0.0001f)
            return path[path.Count - 1];

        float targetLength = fraction * totalLength;
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (cumulative[i + 1] >= targetLength)
            {
                float segment = cumulative[i + 1] - cumulative[i];
                float t = segment > 0.0001f ? (targetLength - cumulative[i]) / segment : 0f;
                return new Vec2(
                    path[i].x + (path[i + 1].x - path[i].x) * t,
                    path[i].y + (path[i + 1].y - path[i].y) * t);
            }
        }
        return path[path.Count - 1];
    }
}
