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
        public Vec2 PathEndPlayerPos;
        public Vec2 SmoothPos;
        public bool SmoothInitialized;
        public Vec2 LastAppliedPos;
        public bool HasAppliedPos;
    }

    private const string CaravanIdPrefix = "taom_supply_caravan_";

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

    private static MethodInfo _bearingSetter;
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
                var lord = MBObjectManager.Instance.GetObject<Hero>(order.SourceHeroId);
                var lordParty = lord?.PartyBelongedTo;
                if (lordParty == null)
                {
                    _logger.LogWarning($"[SupplyLines] Spawn: lord '{order.SourceHeroId}' has no party, order {order.OrderId} not spawned");
                    return null;
                }
                position = lordParty.Position;
                culture = lord.Culture ?? Hero.MainHero?.Culture;
                home = lord.HomeSettlement ?? Hero.MainHero?.HomeSettlement;
            }
            else
            {
                var settlement = Settlement.Find(order.SourceSettlementId);
                if (settlement == null)
                {
                    _logger.LogWarning($"[SupplyLines] Spawn: settlement '{order.SourceSettlementId}' not found, order {order.OrderId} not spawned");
                    return null;
                }
                position = settlement.GatePosition;
                culture = settlement.Culture;
                home = settlement;
            }

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
            TryDestroyHalfSpawnedParty(party);
            return null;
        }
    }

    public bool CaravanExists(SupplyOrder order)
    {
        var party = TrackedParty(order);
        return party != null && party.IsActive;
    }

    public bool CaravanInRaid(SupplyOrder order)
    {
        var party = TrackedParty(order);
        return party?.MapEvent?.IsRaid == true;
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

            if (!TryGetOrigin(order, out Vec2 origin))
                continue;

            if (tracker.PathPoints == null
                || tracker.PathPoints.Count == 0
                || tracker.PathEndPlayerPos.Distance(playerPos) > RepathWhenPlayerMoved)
            {
                tracker.PathPoints = ComputeNavPathPoints(origin, playerPos);
                tracker.PathEndPlayerPos = playerPos;
            }

            float fraction = ClampFraction(order.ElapsedFraction());
            Vec2 target = PointAtFraction(tracker.PathPoints, fraction);

            if (!tracker.SmoothInitialized)
            {
                tracker.SmoothPos = target;
                tracker.SmoothInitialized = true;
            }
            Vec2 previous = tracker.SmoothPos;
            tracker.SmoothPos = new Vec2(
                previous.x + (target.x - previous.x) * PositionSmoothingFactor,
                previous.y + (target.y - previous.y) * PositionSmoothingFactor);

            if (!tracker.HasAppliedPos || tracker.SmoothPos.Distance(tracker.LastAppliedPos) >= MinPositionDelta)
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

            if (fraction < BearingSuppressedPastFraction)
                ApplyBearing(party, tracker, previous, origin);
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

    public void RespawnMissing(IEnumerable<SupplyOrder> orders)
    {
        if (orders == null)
            return;

        Dictionary<string, MobileParty> partiesById = null;
        foreach (var order in orders)
        {
            if (order == null || order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;

            if (_caravans.TryGetValue(order.OrderId, out var tracker)
                && tracker.Party != null && tracker.Party.IsActive)
            {
                PinAi(tracker.Party); // AI pin does not survive a save round-trip
                continue;
            }

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
                _caravans[order.OrderId] = new CaravanTracker { Order = order, Party = survivor };
                PinAi(survivor);
                continue;
            }

            if (order.IsFromLord)
            {
                // No settlement to respawn from; the order service's next hourly tick sees the
                // missing caravan and marks the order Lost. Logged so the loss is attributable.
                _logger.LogWarning($"[SupplyLines] lord-source caravan for order {order.OrderId} missing after load; order will be lost");
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

    private void AttachCompanionEscort(MobileParty party, SupplyOrder order)
    {
        if (string.IsNullOrEmpty(order.EscortHeroId))
            return;
        var hero = MBObjectManager.Instance.GetObject<Hero>(order.EscortHeroId);
        if (hero == null || !hero.IsAlive)
        {
            _logger.LogWarning($"[SupplyLines] Spawn: escort hero '{order.EscortHeroId}' unavailable, caravan goes unescorted");
            return;
        }
        AddHeroToPartyAction.Apply(hero, party, showNotification: false);
    }

    private static void PinAi(MobileParty party)
    {
        party?.Ai?.SetDoNotMakeNewDecisions(true);
    }

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
        var hero = MBObjectManager.Instance.GetObject<Hero>(order.EscortHeroId);
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
        origin = default;
        if (order.IsFromLord)
        {
            var lordParty = MBObjectManager.Instance.GetObject<Hero>(order.SourceHeroId)?.PartyBelongedTo;
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
            _bearingSetter = AccessTools.PropertySetter(typeof(MobileParty), "Bearing");
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
            _bearingSetter.Invoke(party, new object[] { direction });
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

    internal static Vec2 PointAtFraction(List<Vec2> path, float fraction)
    {
        if (path == null || path.Count == 0)
            return new Vec2(0f, 0f);
        if (path.Count == 1 || fraction <= 0f)
            return path[0];
        if (fraction >= 1f)
            return path[path.Count - 1];

        float totalLength = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            totalLength += path[i].Distance(path[i + 1]);
        if (totalLength <= 0.0001f)
            return path[path.Count - 1];

        float targetLength = fraction * totalLength;
        float walked = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            float segment = path[i].Distance(path[i + 1]);
            if (walked + segment >= targetLength)
            {
                float t = segment > 0.0001f ? (targetLength - walked) / segment : 0f;
                return new Vec2(
                    path[i].x + (path[i + 1].x - path[i].x) * t,
                    path[i].y + (path[i + 1].y - path[i].y) * t);
            }
            walked += segment;
        }
        return path[path.Count - 1];
    }
}
