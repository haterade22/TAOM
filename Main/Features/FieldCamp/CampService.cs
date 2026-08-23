using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.SupplyLines;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// One hostile party the ambush scan is weighing. Built at the campaign boundary; the decision
/// logic reads only the plain fields, and <see cref="EngineParty"/> rides along as an opaque
/// handle so the boundary can act on the chosen target without a second lookup.
/// </summary>
public sealed class AmbushCandidate
{
    public string PartyId;
    public string Name;
    public float StraightLineDistance;
    public bool IsBandit;

    /// <summary>The engine <c>MobileParty</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineParty;
}

/// <summary>What kind of move order the player had issued when the move guard interrupted it.</summary>
public enum CapturedMoveKind
{
    /// <summary>A plain point click (or any order this capture does not model): resume as GoToPoint.</summary>
    Point = 0,
    /// <summary>A settlement click: resume as GoToSettlement so the party actually enters.</summary>
    Settlement = 1,
    /// <summary>An engage order on a hostile party: resume as EngageParty so the attack happens.</summary>
    EngageParty = 2,
    /// <summary>An escort/follow order on a friendly party.</summary>
    EscortParty = 3,
}

/// <summary>
/// Full snapshot of the player's interrupted move order, captured BEFORE the move guard holds the
/// party. Resuming only the point (the old shape) downgraded settlement/party clicks to a stale
/// walk-to-position: the party stopped at the gate without entering, or marched to where the enemy
/// used to be. The engine targets ride along as opaque handles (null in tests).
/// </summary>
public sealed class CapturedMoveOrder
{
    public CapturedMoveKind Kind;

    /// <summary>The engine <c>Settlement</c> for <see cref="CapturedMoveKind.Settlement"/>.</summary>
    public object TargetSettlement;

    /// <summary>The engine <c>MobileParty</c> for engage/escort kinds.</summary>
    public object TargetParty;

    /// <summary>Fallback point (always captured; the resume path degrades to it when the live
    /// target has since died or despawned).</summary>
    public CampaignVec2 Point;

    public bool TargetingPort;

    public MobileParty.NavigationType NavigationType = MobileParty.NavigationType.Default;
}

/// <summary>
/// The camp book and its state machine (port of the FieldCamp module's CampManager +
/// campaign-behavior guts). Owns the persisted dictionary; the campaign behavior hands it through
/// LoadFrom/SaveInto at SyncData time and pumps <see cref="HourlyTick"/> / <see cref="FrameTick"/>.
///
/// <para>Deliberate changes from the source: fortifying no longer wipes the forage tally (it does
/// restart the raise, a real second build at double the setup hours, matching the source's
/// re-establish); the ambush scan runs on a half-hour game-time cadence with a straight-line
/// prefilter instead of pathfinding every hostile every frame. The ambush payoff is the source's:
/// trigger breaks the camp, a two-button inquiry offers the strike, and confirming starts a real
/// battle (morale halved + disorganized only on a successful roll).</para>
///
/// <para>Campaign statics (party, terrain, gold, tracker, inquiries) are isolated behind protected
/// virtual members, the SupplyOrderService precedent, so every decision path is unit-testable; the
/// virtual bodies are the honest untested boundary sliver.</para>
/// </summary>
public class CampService : ICampService
{
    private const float AmbushLookoutBuildFactor = 0.25f;
    private const float FortifiedBuildFactor = 2f;
    private const float FortifiedMoraleFactor = 2f;
    private const float AmbushScanIntervalHours = 0.5f;
    private const float BanditPriorityBias = -0.5f;

    private readonly ICampSettingsProvider _settings;
    private readonly ICampTerrainService _terrain;
    private readonly ICampAmbushService _ambush;
    private readonly ICampVisualService _visuals;
    private readonly ISupplyOrderService _supplyOrders;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IEnumerable<ICampOverlayContributor> _overlayContributors;
    private readonly IModLogger _logger;

    private Dictionary<string, CampState> _camps = new Dictionary<string, CampState>();

    /// <summary>Reentry latch: while the break-camp inquiry is on screen the move guard must not
    /// stack a second one every frame. Transient; LoadFrom and ResetForNewSession clear it, because
    /// a stale true (quit-to-menu with the inquiry showing) would permanently disable the guard.</summary>
    private bool _movePromptOpen;

    /// <summary>Latch for the ambush strike-now inquiry (the source's <c>_inquiryPending</c>):
    /// no new scan may run while the player is deciding. Transient, cleared like the move latch.</summary>
    private bool _ambushPromptOpen;

    /// <summary>Whether the standing player camp's completion has been announced. Transient:
    /// LoadFrom re-derives it from readiness so a loaded ready camp never re-announces.</summary>
    private bool _establishAnnounced = true;

    /// <summary>Next game-time (in hours) the ambush scan may run. MinValue = scan on the first
    /// eligible frame, then every <see cref="AmbushScanIntervalHours"/>. Transient: LoadFrom resets
    /// it, otherwise loading a save whose game time is behind the last scan stalls every ambush
    /// camp until the clock catches up.</summary>
    private double _nextAmbushScanHours = double.MinValue;

    public CampService(
        ICampSettingsProvider settings,
        ICampTerrainService terrain,
        ICampAmbushService ambush,
        ICampVisualService visuals,
        ISupplyOrderService supplyOrders,
        IEnlistmentStateQuery enlistment,
        IEnumerable<ICampOverlayContributor> overlayContributors,
        IModLogger logger)
    {
        _settings = settings;
        _terrain = terrain;
        _ambush = ambush;
        _visuals = visuals;
        _supplyOrders = supplyOrders;
        _enlistment = enlistment;
        _overlayContributors = overlayContributors;
        _logger = logger;
    }

    public CampState PlayerCamp
    {
        get
        {
            var id = MainPartyId();
            return id != null && _camps.TryGetValue(id, out var camp) ? camp : null;
        }
    }

    public CampBlockReason CanEstablish(CampType type)
    {
        if (!_settings.Enabled)
            return CampBlockReason.FeatureDisabled;
        // No main party means no campaign to act in; the feature cannot operate at all.
        if (MainPartyId() == null)
            return CampBlockReason.FeatureDisabled;
        // An enlisted soldier's movement belongs to his commander; pitching a private camp would
        // fight the enlistment attachment loop for control of the party.
        if (_enlistment.IsEnlisted)
            return CampBlockReason.Enlisted;
        if (PlayerCamp != null)
            return CampBlockReason.AlreadyCamped;
        if (IsMainPartyInSettlement())
            return CampBlockReason.InSettlement;
        if (IsMainPartyMoving())
            return CampBlockReason.Moving;

        // First contributor with an opinion wins (Refuge raising nearby, etc.).
        if (FirstContributorBlockReason() != null)
            return CampBlockReason.External;

        // Ambush and lookout camps stay hidden and small, so only the visible camp types keep
        // their distance from garrisoned walls.
        if (type == CampType.Field || type == CampType.Fortified)
        {
            float minDistance = _settings.MinTownDistance;
            if (FiniteFloatValidator.IsFinite(minDistance) && minDistance > 0f
                && DistanceToNearestFortification() < minDistance)
            {
                return CampBlockReason.TooCloseToTown;
            }
        }

        if (type == CampType.Ambush && !_terrain.AllowsAmbush(CurrentTerrain()))
            return CampBlockReason.TerrainUnsuitable;
        if (type == CampType.Lookout && !_terrain.AllowsLookout(CurrentTerrain()))
            return CampBlockReason.TerrainUnsuitable;

        return CampBlockReason.None;
    }

    public bool Establish(CampType type)
    {
        if (CanEstablish(type) != CampBlockReason.None)
            return false;

        var id = MainPartyId();
        var camp = new CampState
        {
            TypeEnum = type,
            EstablishedAt = CampaignTimeNow(),
            BuildHours = BuildHoursFor(type),
        };
        _camps[id] = camp;

        // The completion of THIS raise has not been announced yet (FrameTick announces once the
        // camp turns ready).
        _establishAnnounced = false;

        HoldMainParty();
        TrackMainPartyOnMap();
        // The raising layout shows immediately (matching the port's staged-reveal choice);
        // FrameTick retries while the map scene is not ready yet.
        TryShowVisual(id, camp);
        return true;
    }

    public void BreakPlayerCamp()
    {
        var id = MainPartyId();
        if (id == null || !_camps.Remove(id))
            return;

        UntrackMainPartyOnMap();
        _visuals.Remove(id);
        // Only convoys ordered FROM this camp lose their delivery point; town-placed orders
        // deliver to the party wherever it is and keep advancing untouched.
        _supplyOrders.CancelCampOrders();
        _movePromptOpen = false;
        _establishAnnounced = true;
        ShowMessage(new TextObject("{=taom_fc_broken}Camp broken."), error: false);
    }

    public bool Fortify()
    {
        var camp = PlayerCamp;
        if (camp == null || camp.TypeEnum != CampType.Field || !IsCampReady(camp))
            return false;

        int cost = _settings.FortifiedUpgradeCost;
        if (PlayerGold < cost)
            return false;
        ChargePlayer(cost);

        // Source behaviour: fortifying re-establishes the camp, a REAL second raise at double the
        // setup hours (the palisade takes time to build; morale and forage pause until it stands).
        // The one fix kept over the source: Foraging, ForageAccumulator and ForagedTotal survive
        // the upgrade instead of being silently wiped, so foraging resumes when the raise ends.
        camp.EstablishedAt = CampaignTimeNow();
        camp.BuildHours = BuildHoursFor(CampType.Fortified);
        camp.TypeEnum = CampType.Fortified;
        _establishAnnounced = false;

        // The palisade layout replaces the tent layout: drop and re-show under the new type.
        var id = MainPartyId();
        _visuals.Remove(id);
        camp.VisualShown = false;
        TryShowVisual(id, camp);
        return true;
    }

    public bool ToggleForaging()
    {
        var camp = PlayerCamp;
        if (camp == null)
            return false;
        if (camp.TypeEnum != CampType.Field && camp.TypeEnum != CampType.Fortified)
            return false;
        if (!IsCampReady(camp))
            return false;
        camp.Foraging = !camp.Foraging;
        return true;
    }

    public void HourlyTick()
    {
        var camp = PlayerCamp;
        if (camp == null)
            return;

        // State-protecting folds run even while the master toggle is off: a disabled feature must
        // still be able to clean up a standing camp, never trap the player with dead state.
        // Capture breaks the camp cleanly (visuals removed, camp-placed orders cancelled) instead
        // of letting it tick along at the captor's position.
        if (IsMainPartyCaptive())
        {
            BreakPlayerCamp();
            return;
        }

        // Entering a settlement folds the tents (the source's ResetIfMoving guard).
        if (IsMainPartyInSettlement())
        {
            BreakPlayerCamp();
            return;
        }

        // Feature off = no gameplay effects; the camp itself stays so a toggle never drops state.
        if (!_settings.Enabled)
            return;

        if (!IsCampReady(camp))
            return;

        if (camp.TypeEnum == CampType.Field || camp.TypeEnum == CampType.Fortified)
        {
            float moralePerHour = _settings.CampMoralePerHour;
            if (FiniteFloatValidator.IsFinite(moralePerHour) && moralePerHour != 0f)
            {
                AddMoraleToMainParty(moralePerHour
                    * (camp.TypeEnum == CampType.Fortified ? FortifiedMoraleFactor : 1f));
            }

            if (camp.Foraging)
                ForageHour(camp);
        }
    }

    public void FrameTick()
    {
        var camp = PlayerCamp;
        if (camp == null)
        {
            _movePromptOpen = false;
            return;
        }

        if (IsMainPartyCaptive())
        {
            BreakPlayerCamp();
            return;
        }

        // The move guard PROTECTS state (it is the only break path left while the toggle is off,
        // since the overlay button hides), so it runs regardless of the master toggle. The ambush
        // scan is a gameplay effect and stops when the feature is off.
        MoveGuardTick();
        if (_settings.Enabled)
            AmbushScanTick(camp);

        // The scan may have sprung the trap and broken the camp this very frame; never retry a
        // visual for a camp that no longer exists.
        camp = PlayerCamp;
        if (camp == null)
            return;

        if (_settings.Enabled)
            AnnounceReadyTick(camp);

        // The map scene may not have existed when the visual was first requested (save load);
        // retry until the visual service reports it standing. Once it stands, the IsShown poll is
        // the steady-state driver for the banner-cloth wind ticker (CampVisualService throttles).
        if (!camp.VisualShown)
            TryShowVisual(MainPartyId(), camp);
        else
            _visuals.IsShown(MainPartyId());
    }

    public void LoadFrom(Dictionary<string, CampState> camps)
    {
        _camps = ScrubNullRows(camps);
        // Stale transients must never survive into a loaded save: a forward-set ambush clock
        // stalls every scan, a stuck move latch kills the break guard, and a pending inquiry
        // latch from a dead session would block the first scan forever.
        ResetTransientState();
        // A loaded ready camp must not re-announce; a loaded mid-raise camp announces on
        // completion exactly once.
        var playerCamp = PlayerCamp;
        _establishAnnounced = playerCamp == null || IsCampReady(playerCamp);
    }

    public void ResetForNewSession()
    {
        _camps = new Dictionary<string, CampState>();
        ResetTransientState();
        _establishAnnounced = true;
        // Entity handles from the previous session's map scene must not leak into this one.
        _visuals.ClearAll();
    }

    private void ResetTransientState()
    {
        _movePromptOpen = false;
        _ambushPromptOpen = false;
        _nextAmbushScanHours = double.MinValue;
    }

    /// <summary>Drops null rows a hostile or partially recovered save may carry; every consumer
    /// dereferences the CampState unguarded, so one null row would NRE every tick.</summary>
    private Dictionary<string, CampState> ScrubNullRows(Dictionary<string, CampState> camps)
    {
        if (camps == null)
            return new Dictionary<string, CampState>();

        int dropped = 0;
        foreach (var key in new List<string>(camps.Keys))
        {
            if (camps[key] == null)
            {
                camps.Remove(key);
                dropped++;
            }
        }
        if (dropped > 0)
            _logger.LogWarning($"[FieldCamp] dropped {dropped} null camp record(s) from the loaded book");
        return camps;
    }

    public void SaveInto(out Dictionary<string, CampState> camps)
    {
        camps = _camps;
    }

    public void OnGameLoaded()
    {
        // Camp entities never survive a save load even though VisualShown was persisted; clear the
        // flags so the FrameTick retry loop rebuilds every layout once the scene is live.
        foreach (var camp in _camps.Values)
            camp.VisualShown = false;

        var playerCamp = PlayerCamp;
        if (playerCamp != null)
            TryShowVisual(MainPartyId(), playerCamp);
    }

    // --- internals ---

    private float BuildHoursFor(CampType type)
    {
        float setupHours = _settings.CampSetupHours;
        // Belt over the provider's own validation: a poisoned setup time becomes 0, which
        // CampState treats as instantly ready rather than never ready (NaN progress).
        if (!FiniteFloatValidator.IsFinite(setupHours) || setupHours < 0f)
            setupHours = 0f;
        switch (type)
        {
            case CampType.Ambush:
            case CampType.Lookout:
                return setupHours * AmbushLookoutBuildFactor;
            case CampType.Fortified:
                return setupHours * FortifiedBuildFactor;
            default:
                return setupHours;
        }
    }

    private string FirstContributorBlockReason()
    {
        foreach (var contributor in _overlayContributors)
        {
            var reason = contributor?.CreationBlockedReason();
            if (!string.IsNullOrEmpty(reason))
                return reason;
        }
        return null;
    }

    private void ForageHour(CampState camp)
    {
        float gathered = _terrain.HourlyForage(
            CurrentTerrain(), MainPartyTroopCount(), MainPartyScoutingSkill(), _settings.ForagePerTroopFactor);
        if (!FiniteFloatValidator.IsFinite(gathered) || !(gathered > 0f))
            return;

        // A NaN accumulator from an old save would poison the cast below forever; reset it.
        if (!FiniteFloatValidator.IsFinite(camp.ForageAccumulator))
            camp.ForageAccumulator = 0f;

        camp.ForageAccumulator += gathered;
        int wholeGrain = (int)camp.ForageAccumulator;
        if (wholeGrain < 1)
            return;

        camp.ForageAccumulator -= wholeGrain;
        camp.ForagedTotal += wholeGrain;
        AddGrainToMainParty(wholeGrain);
        ShowMessage(
            new TextObject("{=taom_fc_foraged}Foraged +{N} grain (camp total: {TOTAL}).")
                .SetTextVariable("N", wholeGrain)
                .SetTextVariable("TOTAL", camp.ForagedTotal),
            error: false);
    }

    /// <summary>Announces the raise completing, once per raise (the source's per-type "Camp
    /// established." feedback, folded into one key).</summary>
    private void AnnounceReadyTick(CampState camp)
    {
        if (_establishAnnounced || !IsCampReady(camp))
            return;
        _establishAnnounced = true;
        ShowMessage(
            new TextObject("{=taom_fc_established}{CAMP} established.")
                .SetTextVariable("CAMP", UI.FieldCampTexts.TypeLabel(camp.TypeEnum)),
            error: false);
    }

    private void MoveGuardTick()
    {
        if (_movePromptOpen || _ambushPromptOpen || !IsMainPartyMoving())
            return;

        // Capture the FULL click order BEFORE holding, so "break camp and move" resumes exactly
        // what was ordered: a settlement click still enters, an engage click still attacks. A
        // bare point snapshot would strand the party at the gate or at a stale enemy position.
        var order = CaptureMoveOrder();
        HoldMainParty();
        _movePromptOpen = true;
        ShowBreakCampInquiry(
            breakAndMove: () =>
            {
                _movePromptOpen = false;
                BreakPlayerCamp();
                ResumeMove(order);
            },
            stayCamped: () =>
            {
                _movePromptOpen = false;
                HoldMainParty();
            });
    }

    private void AmbushScanTick(CampState camp)
    {
        if (camp.TypeEnum != CampType.Ambush || !IsCampReady(camp))
            return;
        // No new scan while the strike-now inquiry is on screen (the source's _inquiryPending).
        if (_ambushPromptOpen)
            return;
        if (IsMainPartyInEncounter())
            return;

        // Game-time throttle: the source pathfound every hostile lord and bandit party every
        // frame; half an in-game hour keeps the trap responsive at a fraction of the cost.
        double now = NowInHours();
        if (now < _nextAmbushScanHours)
            return;
        _nextAmbushScanHours = now + AmbushScanIntervalHours;

        TrySpringAmbush();
    }

    private void TrySpringAmbush()
    {
        float maxRange = _settings.MaxAmbushRange;
        float spotting = MainPartySpottingRange();
        if (!FiniteFloatValidator.IsFinite(maxRange) || !(maxRange > 0f))
            return;
        if (!FiniteFloatValidator.IsFinite(spotting) || !(spotting > 0f))
            return;

        // A target must be inside BOTH the party's sight and the trap's reach.
        float reach = Math.Min(spotting, maxRange);

        AmbushCandidate best = null;
        float bestScore = float.MaxValue;
        foreach (var candidate in EnumerateHostileCandidates())
        {
            if (candidate == null)
                continue;
            // Straight-line prefilter: the navigation distance can only be longer, so anything
            // beyond reach as the crow flies can never qualify and skips pathfinding entirely.
            if (!FiniteFloatValidator.IsFinite(candidate.StraightLineDistance)
                || candidate.StraightLineDistance > reach)
            {
                continue;
            }

            float navDistance = NavigationDistanceTo(candidate);
            if (!FiniteFloatValidator.IsFinite(navDistance) || navDistance > reach)
                continue;

            // Bandits are the preferred prey at equal distance (source bias).
            float score = navDistance + (candidate.IsBandit ? BanditPriorityBias : 0f);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
            return;

        // Source formula: the chance rides on the PLAYER's spotting range (a wider sight radius
        // erodes the concealment term), not on the candidate's distance.
        float chance = _ambush.TriggerChance(
            spotting, maxRange, _settings.BaseAmbushChance, MainPartyScoutingSkill());
        bool success = NextRandomFloat() <= chance;

        // The trap is spent either way: sprung or spotted, the camp is done (source behaviour).
        BreakPlayerCamp();

        var enemyName = new TextObject("{=taom_fc_ambush_enemy}the enemy");
        string name = string.IsNullOrEmpty(best.Name) ? enemyName.ToString() : best.Name;

        // Source payoff: a two-button inquiry, and confirming starts a real battle. On a
        // successful roll the target is softened first (morale halved, disorganized); on a failed
        // roll the player may still attack, without the edge.
        _ambushPromptOpen = true;
        ShowAmbushInquiry(
            name,
            success,
            attack: () =>
            {
                _ambushPromptOpen = false;
                LaunchAmbushBattle(best, success);
            },
            holdBack: () => _ambushPromptOpen = false);
    }

    private void LaunchAmbushBattle(AmbushCandidate candidate, bool success)
    {
        // Source guard: no battle unless BOTH parties are free of a map event; the world kept
        // moving while the inquiry was up.
        if (IsMainPartyInEncounter() || IsCandidateInMapEvent(candidate))
            return;
        if (success)
            ApplyAmbushPenalties(candidate, _ambush.AmbushedMoraleFactor);
        StartBattleWith(candidate);
    }

    private void TryShowVisual(string partyId, CampState camp)
    {
        if (partyId == null || camp.VisualShown)
            return;
        if (_visuals.Show(partyId, camp.TypeEnum, MainPartyPosition()))
            camp.VisualShown = true;
    }

    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

    protected virtual string MainPartyId() => MobileParty.MainParty?.StringId;

    protected virtual bool IsMainPartyMoving() => MobileParty.MainParty?.IsMoving ?? false;

    protected virtual bool IsMainPartyInSettlement() => MobileParty.MainParty?.CurrentSettlement != null;

    protected virtual bool IsMainPartyInEncounter() => MobileParty.MainParty?.MapEvent != null;

    protected virtual Vec2 MainPartyPosition() => MobileParty.MainParty?.GetPosition2D ?? default;

    protected virtual void HoldMainParty() => MobileParty.MainParty?.SetMoveModeHold();

    protected virtual bool IsMainPartyCaptive() =>
        Campaign.Current != null && PlayerCaptivity.IsCaptive;

    protected virtual CapturedMoveOrder CaptureMoveOrder()
    {
        var main = MobileParty.MainParty;
        var order = new CapturedMoveOrder();
        if (main == null)
            return order;

        order.Point = main.MoveTargetPoint;
        // Hold resets DesiredAiNavigationType to None, which is why the capture happens BEFORE
        // the hold; None still maps to Default defensively.
        if (main.DesiredAiNavigationType != MobileParty.NavigationType.None)
            order.NavigationType = main.DesiredAiNavigationType;

        switch (main.DefaultBehavior)
        {
            case AiBehavior.GoToSettlement when main.TargetSettlement != null:
                order.Kind = CapturedMoveKind.Settlement;
                order.TargetSettlement = main.TargetSettlement;
                order.TargetingPort = main.IsTargetingPort;
                break;
            case AiBehavior.EngageParty when main.TargetParty != null:
                order.Kind = CapturedMoveKind.EngageParty;
                order.TargetParty = main.TargetParty;
                break;
            case AiBehavior.EscortParty when main.TargetParty != null:
                order.Kind = CapturedMoveKind.EscortParty;
                order.TargetParty = main.TargetParty;
                order.TargetingPort = main.IsTargetingPort;
                break;
            default:
                order.Kind = CapturedMoveKind.Point;
                break;
        }
        return order;
    }

    protected virtual void ResumeMove(CapturedMoveOrder order)
    {
        var main = MobileParty.MainParty;
        if (main == null || order == null)
            return;

        // A target that died or despawned while the inquiry was up degrades to the point walk.
        switch (order.Kind)
        {
            case CapturedMoveKind.Settlement when order.TargetSettlement is Settlement settlement:
                main.SetMoveGoToSettlement(settlement, order.NavigationType, order.TargetingPort);
                return;
            case CapturedMoveKind.EngageParty
                when order.TargetParty is MobileParty engageTarget && engageTarget.IsActive:
                main.SetMoveEngageParty(engageTarget, order.NavigationType);
                return;
            case CapturedMoveKind.EscortParty
                when order.TargetParty is MobileParty escortTarget && escortTarget.IsActive:
                main.SetMoveEscortParty(escortTarget, order.NavigationType, order.TargetingPort);
                return;
        }
        main.SetMoveGoToPoint(order.Point, order.NavigationType);
    }

    protected virtual TerrainType CurrentTerrain()
    {
        var mapScene = Campaign.Current?.MapSceneWrapper;
        var party = MobileParty.MainParty;
        if (mapScene == null || party == null)
            return TerrainType.Plain;
        return mapScene.GetFaceTerrainType(party.CurrentNavigationFace);
    }

    protected virtual float DistanceToNearestFortification()
    {
        var party = MobileParty.MainParty;
        if (party == null)
            return float.MaxValue;
        var position = party.GetPosition2D;
        float nearest = float.MaxValue;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                continue;
            float distance = position.Distance(settlement.GetPosition2D);
            if (distance < nearest)
                nearest = distance;
        }
        return nearest;
    }

    protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;

    protected virtual void ChargePlayer(int amount) =>
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);

    protected virtual void AddMoraleToMainParty(float delta)
    {
        var party = MobileParty.MainParty;
        if (party != null)
            party.RecentEventsMorale += delta;
    }

    protected virtual int MainPartyTroopCount() =>
        MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;

    protected virtual float MainPartyScoutingSkill() =>
        MobileParty.MainParty?.LeaderHero?.GetSkillValue(DefaultSkills.Scouting) ?? 0f;

    protected virtual void AddGrainToMainParty(int amount) =>
        MobileParty.MainParty?.ItemRoster?.AddToCounts(DefaultItems.Grain, amount);

    protected virtual float MainPartySpottingRange()
    {
        var party = MobileParty.MainParty;
        var model = Campaign.Current?.Models?.MapVisibilityModel;
        if (party == null || model == null)
            return 0f;
        return model.GetPartySpottingRange(party).ResultNumber;
    }

    /// <summary>Every lord and bandit party at war with the player, with a cheap straight-line
    /// distance; the expensive pathfinding happens only for prefilter survivors.</summary>
    protected virtual IReadOnlyList<AmbushCandidate> EnumerateHostileCandidates()
    {
        var result = new List<AmbushCandidate>();
        var main = MobileParty.MainParty;
        var campaign = Campaign.Current;
        var mainFaction = main?.MapFaction;
        if (main == null || campaign == null || mainFaction == null)
            return result;

        var position = main.GetPosition2D;
        CollectHostiles(campaign.LordParties, mainFaction, position, result);
        CollectHostiles(campaign.BanditParties, mainFaction, position, result);
        return result;
    }

    private static void CollectHostiles(
        MBReadOnlyList<MobileParty> parties, IFaction mainFaction, Vec2 mainPosition,
        List<AmbushCandidate> result)
    {
        if (parties == null)
            return;
        foreach (var party in parties)
        {
            if (party == null || party.IsMainParty || !party.IsVisible || party.ShouldBeIgnored)
                continue;
            var faction = party.MapFaction;
            if (faction == null || !faction.IsAtWarWith(mainFaction))
                continue;
            result.Add(new AmbushCandidate
            {
                PartyId = party.StringId,
                Name = party.Name?.ToString(),
                StraightLineDistance = mainPosition.Distance(party.GetPosition2D),
                IsBandit = party.IsBandit,
                EngineParty = party,
            });
        }
    }

    protected virtual float NavigationDistanceTo(AmbushCandidate candidate)
    {
        var main = MobileParty.MainParty;
        var model = Campaign.Current?.Models?.MapDistanceModel;
        if (main == null || model == null || !(candidate.EngineParty is MobileParty target))
            return float.MaxValue;
        return model.GetDistance(target, main, MobileParty.NavigationType.Default, out _);
    }

    protected virtual void ApplyAmbushPenalties(AmbushCandidate candidate, float moraleFactor)
    {
        if (!(candidate.EngineParty is MobileParty target))
            return;
        target.RecentEventsMorale *= moraleFactor;
        target.SetDisorganized(true);
    }

    protected virtual bool IsCandidateInMapEvent(AmbushCandidate candidate) =>
        candidate.EngineParty is MobileParty target && target.MapEvent != null;

    protected virtual void StartBattleWith(AmbushCandidate candidate)
    {
        var main = MobileParty.MainParty;
        if (main == null || !(candidate.EngineParty is MobileParty target))
            return;
        StartBattleAction.ApplyStartBattle(main, target);
    }

    protected virtual float NextRandomFloat() => MBRandom.RandomFloat;

    protected virtual CampaignTime CampaignTimeNow() => CampaignTime.Now;

    protected virtual double NowInHours() => CampaignTime.Now.ToHours;

    // Readiness routes through the service so tests can pin it; CampState.IsReady dereferences
    // Campaign.Current for the elapsed clock.
    protected virtual bool IsCampReady(CampState camp) => camp.IsReady;

    protected virtual void TrackMainPartyOnMap()
    {
        try
        {
            var tracker = Campaign.Current?.VisualTrackerManager;
            var party = MobileParty.MainParty;
            if (tracker == null || party == null)
                return;
            if (!tracker.CheckTracked(party))
                tracker.RegisterObject(party);
        }
        catch (Exception ex)
        {
            // A missing tracker only loses the map marker; never let it kill the camp itself.
            _logger.LogWarning($"[FieldCamp] map tracker register failed: {ex.Message}");
        }
    }

    protected virtual void UntrackMainPartyOnMap()
    {
        try
        {
            var tracker = Campaign.Current?.VisualTrackerManager;
            var party = MobileParty.MainParty;
            if (tracker == null || party == null)
                return;
            tracker.RemoveTrackedObject(party, forceRemove: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FieldCamp] map tracker remove failed: {ex.Message}");
        }
    }

    protected virtual void ShowAmbushInquiry(string enemyName, bool success, Action attack, Action holdBack)
    {
        var body = success
            ? new TextObject("{=taom_fc_ambush_ok}Your ambush is set against {ENEMY} - strike now?")
                .SetTextVariable("ENEMY", enemyName)
            : new TextObject("{=taom_fc_ambush_fail}You were spotted by {ENEMY} before you could strike - attack anyway?")
                .SetTextVariable("ENEMY", enemyName);
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_fc_ambush_title}Ambush").ToString(),
            body.ToString(),
            true,
            true,
            GameTexts.FindText("str_ok").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            attack,
            holdBack));
    }

    protected virtual void ShowBreakCampInquiry(Action breakAndMove, Action stayCamped)
    {
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_fc_move_title}Break camp?").ToString(),
            new TextObject("{=taom_fc_move_text}Moving will break your camp. Break camp and move on?").ToString(),
            true,
            true,
            new TextObject("{=taom_fc_move_yes}Break camp and move").ToString(),
            new TextObject("{=taom_fc_move_no}Stay camped").ToString(),
            breakAndMove,
            stayCamped));
    }

    protected virtual void ShowMessage(TextObject text, bool error)
    {
        InformationManager.DisplayMessage(
            new InformationMessage(text.ToString(), error ? Colors.Red : Colors.Green));
    }
}
