using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.SupplyLines;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Decision-path coverage for <see cref="CampService"/>: the CanEstablish block ordering, the
/// establish/fortify/break lifecycle, hourly morale + forage carry, the frame-tick move guard and
/// the throttled ambush scan. Campaign statics are overridden on a test subclass; those virtual
/// bodies are the honest untested boundary sliver, exercised only in game.
/// </summary>
[TestClass]
public class CampServiceTests
{
    private sealed class TestableCampService : CampService
    {
        public string PartyId = "main_party";
        public bool Moving;
        public bool InSettlement;
        public bool InEncounter;
        public bool Captive;
        public bool CandidateInEvent;
        public TerrainType Terrain = TerrainType.Plain;
        public float NearestFortDistance = 100f;
        public int Gold = 10_000;
        public int TroopCount = 50;
        public float Scouting;
        public float SpottingRange = 5f;
        public double NowHours;
        public bool CampReady = true;
        public float RandomRoll;
        public CapturedMoveOrder MoveOrderToCapture = new CapturedMoveOrder();
        public List<AmbushCandidate> Candidates = new List<AmbushCandidate>();
        public Dictionary<string, float> NavDistances = new Dictionary<string, float>();

        public int HoldCalls;
        public int TrackCalls;
        public int UntrackCalls;
        public int ScanEnumerations;
        public int NavDistanceCalls;
        public int InquiryCount;
        public readonly List<CapturedMoveOrder> ResumedMoves = new List<CapturedMoveOrder>();
        public readonly List<int> Charges = new List<int>();
        public readonly List<float> MoraleAdded = new List<float>();
        public readonly List<int> GrainAdded = new List<int>();
        public readonly List<string> Messages = new List<string>();
        public readonly List<string> MessageTexts = new List<string>();
        public readonly List<string> Penalties = new List<string>();
        public readonly List<string> BattleTargets = new List<string>();
        public Action PendingBreakAndMove;
        public Action PendingStayCamped;

        public int AmbushInquiryCount;
        public bool? LastAmbushSuccess;
        public string LastAmbushEnemy;
        public Action PendingAmbushAttack;
        public Action PendingAmbushHold;

        public TestableCampService(
            ICampSettingsProvider settings,
            ICampTerrainService terrain,
            ICampAmbushService ambush,
            ICampVisualService visuals,
            ISupplyOrderService supplyOrders,
            IEnlistmentStateQuery enlistment,
            IEnumerable<Lazy<ICampOverlayContributor>> overlayContributors,
            IModLogger logger)
            : base(settings, terrain, ambush, visuals, supplyOrders, enlistment, overlayContributors, logger)
        {
        }

        protected override string MainPartyId() => PartyId;
        protected override bool IsMainPartyMoving() => Moving;
        protected override bool IsMainPartyInSettlement() => InSettlement;
        protected override bool IsMainPartyInEncounter() => InEncounter;
        protected override bool IsMainPartyCaptive() => Captive;
        protected override Vec2 MainPartyPosition() => default;
        protected override void HoldMainParty() => HoldCalls++;
        protected override CapturedMoveOrder CaptureMoveOrder() => MoveOrderToCapture;
        protected override void ResumeMove(CapturedMoveOrder order) => ResumedMoves.Add(order);
        protected override TerrainType CurrentTerrain() => Terrain;
        protected override float DistanceToNearestFortification() => NearestFortDistance;
        protected override int PlayerGold => Gold;
        protected override void ChargePlayer(int amount) => Charges.Add(amount);
        protected override void AddMoraleToMainParty(float delta) => MoraleAdded.Add(delta);
        protected override int MainPartyTroopCount() => TroopCount;
        protected override float MainPartyScoutingSkill() => Scouting;
        protected override void AddGrainToMainParty(int amount) => GrainAdded.Add(amount);
        protected override float MainPartySpottingRange() => SpottingRange;
        protected override float NextRandomFloat() => RandomRoll;
        protected override CampaignTime CampaignTimeNow() => default;
        protected override double NowInHours() => NowHours;
        protected override bool IsCampReady(CampState camp) => CampReady;
        protected override bool IsCandidateInMapEvent(AmbushCandidate candidate) => CandidateInEvent;
        protected override void StartBattleWith(AmbushCandidate candidate) => BattleTargets.Add(candidate.PartyId);
        protected override void TrackMainPartyOnMap() => TrackCalls++;
        protected override void UntrackMainPartyOnMap() => UntrackCalls++;

        protected override IReadOnlyList<AmbushCandidate> EnumerateHostileCandidates(float reach)
        {
            ScanEnumerations++;
            return Candidates;
        }

        protected override float NavigationDistanceTo(AmbushCandidate candidate)
        {
            NavDistanceCalls++;
            return NavDistances.TryGetValue(candidate.PartyId, out var distance)
                ? distance
                : float.MaxValue;
        }

        protected override void ApplyAmbushPenalties(AmbushCandidate candidate, float moraleFactor) =>
            // Invariant formatting: a comma-decimal OS culture must not change the assertion key.
            Penalties.Add(candidate.PartyId + ":" + moraleFactor.ToString(System.Globalization.CultureInfo.InvariantCulture));

        protected override void ShowBreakCampInquiry(Action breakAndMove, Action stayCamped)
        {
            InquiryCount++;
            PendingBreakAndMove = breakAndMove;
            PendingStayCamped = stayCamped;
        }

        protected override void ShowAmbushInquiry(string enemyName, bool success, Action attack, Action holdBack)
        {
            AmbushInquiryCount++;
            LastAmbushEnemy = enemyName;
            LastAmbushSuccess = success;
            PendingAmbushAttack = attack;
            PendingAmbushHold = holdBack;
        }

        protected override void ShowMessage(TextObject text, bool error)
        {
            Messages.Add(error ? "error" : "info");
            MessageTexts.Add(text.ToString());
        }
    }

    private ICampSettingsProvider _settings;
    private ICampTerrainService _terrain;
    private ICampAmbushService _ambushMath;
    private ICampVisualService _visuals;
    private ISupplyOrderService _supply;
    private IEnlistmentStateQuery _enlistment;
    private ICampOverlayContributor _contributor;
    private IModLogger _logger;
    private TestableCampService _sut;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICampSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.CampSetupHours.Returns(4f);
        _settings.CampMoralePerHour.Returns(1f);
        _settings.ForagePerTroopFactor.Returns(0.1f);
        _settings.MaxAmbushRange.Returns(10f);
        _settings.BaseAmbushChance.Returns(0.5f);
        _settings.MinTownDistance.Returns(10f);
        _settings.FortifiedUpgradeCost.Returns(500);

        _terrain = Substitute.For<ICampTerrainService>();
        _terrain.AllowsAmbush(Arg.Any<TerrainType>()).Returns(true);
        _terrain.AllowsLookout(Arg.Any<TerrainType>()).Returns(true);

        _ambushMath = Substitute.For<ICampAmbushService>();
        _ambushMath.TriggerChance(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(1f);
        _ambushMath.AmbushedMoraleFactor.Returns(0.5f);

        _visuals = Substitute.For<ICampVisualService>();
        _visuals.Show(Arg.Any<string>(), Arg.Any<CampType>(), Arg.Any<Vec2>()).Returns(true);

        _supply = Substitute.For<ISupplyOrderService>();
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _enlistment.IsEnlisted.Returns(false);
        _contributor = Substitute.For<ICampOverlayContributor>();
        _contributor.CreationBlockedReason().Returns((string)null);
        _logger = Substitute.For<IModLogger>();

        _sut = new TestableCampService(
            _settings, _terrain, _ambushMath, _visuals, _supply, _enlistment,
            new[] { new Lazy<ICampOverlayContributor>(() => _contributor) }, _logger);
    }

    private CampState EstablishCamp(CampType type = CampType.Field)
    {
        Assert.IsTrue(_sut.Establish(type), $"arrange: establishing {type} camp");
        return _sut.PlayerCamp;
    }

    private AmbushCandidate AddCandidate(
        string id = "bandit_1", float straightLine = 3f, float navDistance = 4f, bool isBandit = true)
    {
        var candidate = new AmbushCandidate
        {
            PartyId = id,
            Name = id,
            StraightLineDistance = straightLine,
            IsBandit = isBandit,
        };
        _sut.Candidates.Add(candidate);
        _sut.NavDistances[id] = navDistance;
        return candidate;
    }

    // --- CanEstablish ordering ---

    [TestMethod]
    public void CanEstablish_FeatureDisabled_WinsOverEveryOtherBlock()
    {
        _settings.Enabled.Returns(false);
        _enlistment.IsEnlisted.Returns(true);
        _sut.InSettlement = true;

        Assert.AreEqual(CampBlockReason.FeatureDisabled, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_Enlisted_WinsOverSettlementAndMoving()
    {
        _enlistment.IsEnlisted.Returns(true);
        _sut.InSettlement = true;
        _sut.Moving = true;

        Assert.AreEqual(CampBlockReason.Enlisted, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_AlreadyCamped_WinsOverSettlement()
    {
        EstablishCamp();
        _sut.InSettlement = true;

        Assert.AreEqual(CampBlockReason.AlreadyCamped, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_InSettlement_WinsOverMoving()
    {
        _sut.InSettlement = true;
        _sut.Moving = true;

        Assert.AreEqual(CampBlockReason.InSettlement, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_Moving_ReturnsMoving()
    {
        _sut.Moving = true;

        Assert.AreEqual(CampBlockReason.Moving, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_ContributorBlocks_WinsOverTownProximity()
    {
        _contributor.CreationBlockedReason().Returns("a refuge is raising nearby");
        _sut.NearestFortDistance = 1f;

        Assert.AreEqual(CampBlockReason.External, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_ContributorThrows_IsContainedAndSkipped()
    {
        // CanEstablish feeds a GameMenuOption condition whose engine dispatch is unguarded; a
        // faulty contributor must be skipped, never propagated (round-B seam-containment finding,
        // same contract as the VM and menu-controller consumers).
        _contributor.CreationBlockedReason().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_LazyContributorFactoryThrows_IsContainedAndSkipped()
    {
        // Contributors arrive as Lazy<T> (the DryIoc cycle fix); a factory that blows up on
        // first materialization is the same hazard as a throwing contributor body.
        var sut = new TestableCampService(
            _settings, _terrain, _ambushMath, _visuals, _supply, _enlistment,
            new[] { new Lazy<ICampOverlayContributor>(() => throw new InvalidOperationException("boom")) },
            _logger);

        Assert.AreEqual(CampBlockReason.None, sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_FieldTooCloseToTown_ReturnsTooCloseToTown()
    {
        _sut.NearestFortDistance = 5f;

        Assert.AreEqual(CampBlockReason.TooCloseToTown, _sut.CanEstablish(CampType.Field));
        Assert.AreEqual(CampBlockReason.TooCloseToTown, _sut.CanEstablish(CampType.Fortified));
    }

    [TestMethod]
    public void CanEstablish_AmbushAndLookoutExemptFromTownProximity()
    {
        _sut.NearestFortDistance = 5f;

        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Ambush));
        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Lookout));
    }

    [TestMethod]
    public void CanEstablish_MinTownDistanceZero_DisablesProximityCheck()
    {
        _settings.MinTownDistance.Returns(0f);
        _sut.NearestFortDistance = 0.1f;

        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_AmbushOnOpenGround_ReturnsTerrainUnsuitable()
    {
        _terrain.AllowsAmbush(Arg.Any<TerrainType>()).Returns(false);

        Assert.AreEqual(CampBlockReason.TerrainUnsuitable, _sut.CanEstablish(CampType.Ambush));
    }

    [TestMethod]
    public void CanEstablish_LookoutWithoutVantage_ReturnsTerrainUnsuitable()
    {
        _terrain.AllowsLookout(Arg.Any<TerrainType>()).Returns(false);

        Assert.AreEqual(CampBlockReason.TerrainUnsuitable, _sut.CanEstablish(CampType.Lookout));
    }

    [TestMethod]
    public void CanEstablish_FieldCampIgnoresTerrainSets()
    {
        _terrain.AllowsAmbush(Arg.Any<TerrainType>()).Returns(false);
        _terrain.AllowsLookout(Arg.Any<TerrainType>()).Returns(false);

        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Field));
    }

    [TestMethod]
    public void CanEstablish_AllClear_ReturnsNone()
    {
        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Field));
    }

    // --- Establish ---

    [TestMethod]
    public void Establish_BuildHoursScalePerCampType()
    {
        EstablishCamp(CampType.Field);
        Assert.AreEqual(4f, _sut.PlayerCamp.BuildHours, 0.0001f);
        _sut.BreakPlayerCamp();

        EstablishCamp(CampType.Ambush);
        Assert.AreEqual(1f, _sut.PlayerCamp.BuildHours, 0.0001f, "ambush = quarter setup");
        _sut.BreakPlayerCamp();

        EstablishCamp(CampType.Lookout);
        Assert.AreEqual(1f, _sut.PlayerCamp.BuildHours, 0.0001f, "lookout = quarter setup");
        _sut.BreakPlayerCamp();

        EstablishCamp(CampType.Fortified);
        Assert.AreEqual(8f, _sut.PlayerCamp.BuildHours, 0.0001f, "fortified = double setup");
    }

    [TestMethod]
    public void Establish_HoldsPartyTracksAndShowsVisual()
    {
        var camp = EstablishCamp();

        Assert.AreEqual(1, _sut.HoldCalls);
        Assert.AreEqual(1, _sut.TrackCalls);
        _visuals.Received(1).Show("main_party", CampType.Field, Arg.Any<Vec2>());
        Assert.IsTrue(camp.VisualShown);
    }

    [TestMethod]
    public void Establish_Blocked_ReturnsFalseWithoutCamp()
    {
        _sut.Moving = true;

        Assert.IsFalse(_sut.Establish(CampType.Field));
        Assert.IsNull(_sut.PlayerCamp);
        Assert.AreEqual(0, _sut.HoldCalls);
    }

    [TestMethod]
    public void Establish_SceneNotReady_LeavesVisualShownFalseForRetry()
    {
        _visuals.Show(Arg.Any<string>(), Arg.Any<CampType>(), Arg.Any<Vec2>()).Returns(false);

        var camp = EstablishCamp();

        Assert.IsFalse(camp.VisualShown);
    }

    // --- Fortify ---

    [TestMethod]
    public void Fortify_NoCamp_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Fortify());
        Assert.AreEqual(0, _sut.Charges.Count);
    }

    [TestMethod]
    public void Fortify_AmbushCamp_ReturnsFalse()
    {
        EstablishCamp(CampType.Ambush);

        Assert.IsFalse(_sut.Fortify());
    }

    [TestMethod]
    public void Fortify_CampNotReady_ReturnsFalse()
    {
        EstablishCamp();
        _sut.CampReady = false;

        Assert.IsFalse(_sut.Fortify());
        Assert.AreEqual(0, _sut.Charges.Count);
    }

    [TestMethod]
    public void Fortify_InsufficientGold_ReturnsFalseWithoutCharge()
    {
        EstablishCamp();
        _sut.Gold = 100;

        Assert.IsFalse(_sut.Fortify());
        Assert.AreEqual(0, _sut.Charges.Count);
        Assert.AreEqual(CampType.Field, _sut.PlayerCamp.TypeEnum);
    }

    [TestMethod]
    public void Fortify_ReadyFieldCamp_ChargesAndUpgrades()
    {
        EstablishCamp();

        Assert.IsTrue(_sut.Fortify());
        CollectionAssert.AreEqual(new[] { 500 }, _sut.Charges);
        Assert.AreEqual(CampType.Fortified, _sut.PlayerCamp.TypeEnum);
    }

    [TestMethod]
    public void Fortify_PreservesForagingStateAndTally()
    {
        var camp = EstablishCamp();
        camp.Foraging = true;
        camp.ForageAccumulator = 0.4f;
        camp.ForagedTotal = 7;

        Assert.IsTrue(_sut.Fortify());

        Assert.IsTrue(camp.Foraging, "the source wiped Foraging by re-establishing; ours must not");
        Assert.AreEqual(0.4f, camp.ForageAccumulator, 0.0001f);
        Assert.AreEqual(7, camp.ForagedTotal);
    }

    [TestMethod]
    public void Fortify_RestartsTheRaiseAtDoubleSetupHours()
    {
        EstablishCamp();

        Assert.IsTrue(_sut.Fortify());

        // Source behaviour: fortifying re-establishes the camp, a real second raise at 2x the
        // setup hours. The camp regresses to "raising" and its effects pause until it stands.
        Assert.AreEqual(8f, _sut.PlayerCamp.BuildHours, 0.0001f);
    }

    [TestMethod]
    public void Fortify_RaiseCompletion_AnnouncesAgain()
    {
        EstablishCamp();
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.MessageTexts.Count, "arrange: first raise announced");

        Assert.IsTrue(_sut.Fortify());
        _sut.FrameTick();

        Assert.AreEqual(2, _sut.MessageTexts.Count, "the fortified raise announces its own completion");
        StringAssert.Contains(_sut.MessageTexts[1], "established");
    }

    [TestMethod]
    public void Fortify_ReplacesVisualUnderNewType()
    {
        EstablishCamp();
        _visuals.ClearReceivedCalls();

        Assert.IsTrue(_sut.Fortify());

        _visuals.Received(1).Remove("main_party");
        _visuals.Received(1).Show("main_party", CampType.Fortified, Arg.Any<Vec2>());
    }

    // --- ToggleForaging ---

    [TestMethod]
    public void ToggleForaging_ReadyFieldCamp_TogglesBothWays()
    {
        var camp = EstablishCamp();

        Assert.IsTrue(_sut.ToggleForaging());
        Assert.IsTrue(camp.Foraging);
        Assert.IsTrue(_sut.ToggleForaging());
        Assert.IsFalse(camp.Foraging);
    }

    [TestMethod]
    public void ToggleForaging_AmbushCamp_ReturnsFalse()
    {
        EstablishCamp(CampType.Ambush);

        Assert.IsFalse(_sut.ToggleForaging());
    }

    [TestMethod]
    public void ToggleForaging_CampNotReady_ReturnsFalse()
    {
        EstablishCamp();
        _sut.CampReady = false;

        Assert.IsFalse(_sut.ToggleForaging());
    }

    [TestMethod]
    public void ToggleForaging_NoCamp_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ToggleForaging());
    }

    // --- BreakPlayerCamp ---

    [TestMethod]
    public void BreakPlayerCamp_CancelsCampOrdersRemovesVisualAndUntracks()
    {
        EstablishCamp();

        _sut.BreakPlayerCamp();

        Assert.IsNull(_sut.PlayerCamp);
        // Camp-scoped cancellation ONLY: a town-placed order has nothing to do with the camp and
        // its gold/goods must never be forfeited by a routine camp break. (The old blanket
        // CancelAll was deleted from ISupplyOrderService in review round B: it had no
        // production caller and existed only as a footgun that looked symmetrical to this.)
        _supply.Received(1).CancelCampOrders();
        _visuals.Received(1).Remove("main_party");
        Assert.AreEqual(1, _sut.UntrackCalls);
    }

    [TestMethod]
    public void BreakPlayerCamp_AnnouncesTheBreak()
    {
        EstablishCamp();

        _sut.BreakPlayerCamp();

        Assert.AreEqual(1, _sut.MessageTexts.Count);
        StringAssert.Contains(_sut.MessageTexts[0], "Camp broken");
    }

    [TestMethod]
    public void BreakPlayerCamp_NoCamp_SafeAndCancelsNothing()
    {
        _sut.BreakPlayerCamp();

        _supply.DidNotReceive().CancelCampOrders();
        Assert.AreEqual(0, _sut.UntrackCalls);
        Assert.AreEqual(0, _sut.Messages.Count);
    }

    // --- HourlyTick ---

    [TestMethod]
    public void HourlyTick_ReadyFieldCamp_AddsMoralePerHour()
    {
        EstablishCamp();

        _sut.HourlyTick();

        CollectionAssert.AreEqual(new[] { 1f }, _sut.MoraleAdded);
    }

    [TestMethod]
    public void HourlyTick_FortifiedCamp_DoublesMorale()
    {
        EstablishCamp(CampType.Fortified);

        _sut.HourlyTick();

        CollectionAssert.AreEqual(new[] { 2f }, _sut.MoraleAdded);
    }

    [TestMethod]
    public void HourlyTick_CampStillRaising_NoMorale()
    {
        EstablishCamp();
        _sut.CampReady = false;

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.MoraleAdded.Count);
    }

    [TestMethod]
    public void HourlyTick_AmbushAndLookoutCamps_NoMorale()
    {
        EstablishCamp(CampType.Ambush);
        _sut.HourlyTick();
        _sut.BreakPlayerCamp();

        EstablishCamp(CampType.Lookout);
        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.MoraleAdded.Count);
    }

    [TestMethod]
    public void HourlyTick_FeatureDisabled_NoTickButCampKept()
    {
        EstablishCamp();
        _settings.Enabled.Returns(false);

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.MoraleAdded.Count);
        Assert.IsNotNull(_sut.PlayerCamp, "a toggle must never silently drop camp state");
    }

    [TestMethod]
    public void HourlyTick_PartyEnteredSettlement_BreaksCamp()
    {
        EstablishCamp();
        _sut.InSettlement = true;

        _sut.HourlyTick();

        Assert.IsNull(_sut.PlayerCamp);
        _supply.Received(1).CancelCampOrders();
        Assert.AreEqual(0, _sut.MoraleAdded.Count);
    }

    [TestMethod]
    public void HourlyTick_ForageAccumulatorCarriesFractionsAcrossHours()
    {
        _terrain.HourlyForage(Arg.Any<TerrainType>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(0.6f);
        var camp = EstablishCamp();
        camp.Foraging = true;

        _sut.HourlyTick();
        Assert.AreEqual(0, _sut.GrainAdded.Count, "0.6 accumulated, below one grain");
        Assert.AreEqual(0.6f, camp.ForageAccumulator, 0.0001f);

        _sut.HourlyTick();
        CollectionAssert.AreEqual(new[] { 1 }, _sut.GrainAdded);
        Assert.AreEqual(0.2f, camp.ForageAccumulator, 0.0001f);
        Assert.AreEqual(1, camp.ForagedTotal);
        CollectionAssert.AreEqual(new[] { "info" }, _sut.Messages);
    }

    [TestMethod]
    public void HourlyTick_ForagingOff_NeverQueriesTerrainForage()
    {
        EstablishCamp();

        _sut.HourlyTick();

        _terrain.DidNotReceive().HourlyForage(
            Arg.Any<TerrainType>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void HourlyTick_NoCamp_NoEffect()
    {
        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.MoraleAdded.Count);
        _supply.DidNotReceive().CancelCampOrders();
    }

    // --- FrameTick: move guard ---

    [TestMethod]
    public void FrameTick_MovingWhileCamped_HoldsAndAsksOnce()
    {
        EstablishCamp();
        _sut.HoldCalls = 0;
        _sut.Moving = true;

        _sut.FrameTick();

        Assert.AreEqual(1, _sut.HoldCalls);
        Assert.AreEqual(1, _sut.InquiryCount);
    }

    [TestMethod]
    public void FrameTick_PromptAlreadyOpen_DoesNotStackInquiries()
    {
        EstablishCamp();
        _sut.Moving = true;

        _sut.FrameTick();
        _sut.FrameTick();

        Assert.AreEqual(1, _sut.InquiryCount, "reentry latch must hold while the inquiry is open");
    }

    [TestMethod]
    public void FrameTick_ConfirmBreak_BreaksCampAndResumesTheCapturedOrder()
    {
        EstablishCamp();
        _sut.MoveOrderToCapture = new CapturedMoveOrder { Kind = CapturedMoveKind.Settlement };
        _sut.Moving = true;
        _sut.FrameTick();

        _sut.PendingBreakAndMove();

        Assert.IsNull(_sut.PlayerCamp);
        // The FULL captured order object reaches the resume path: settlement/party targets are
        // preserved, never downgraded to a stale point snapshot.
        Assert.AreEqual(1, _sut.ResumedMoves.Count);
        Assert.AreSame(_sut.MoveOrderToCapture, _sut.ResumedMoves[0]);
        Assert.AreEqual(CapturedMoveKind.Settlement, _sut.ResumedMoves[0].Kind);
        _supply.Received(1).CancelCampOrders();
    }

    [TestMethod]
    public void FrameTick_StayCamped_KeepsCampAndReleasesLatch()
    {
        EstablishCamp();
        _sut.Moving = true;
        _sut.FrameTick();
        int holdsBefore = _sut.HoldCalls;

        _sut.PendingStayCamped();

        Assert.IsNotNull(_sut.PlayerCamp);
        Assert.AreEqual(holdsBefore + 1, _sut.HoldCalls, "staying re-holds the party");

        // Latch released: a fresh move attempt prompts again.
        _sut.FrameTick();
        Assert.AreEqual(2, _sut.InquiryCount);
    }

    // --- FrameTick: ambush scan ---

    [TestMethod]
    public void FrameTick_AmbushScan_RunsOnHalfHourGameTimeCadence()
    {
        EstablishCamp(CampType.Ambush);

        _sut.NowHours = 0.0;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.ScanEnumerations, "first eligible frame scans");

        _sut.NowHours = 0.4;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.ScanEnumerations, "0.4h since epoch is inside the throttle window");

        _sut.NowHours = 0.6;
        _sut.FrameTick();
        Assert.AreEqual(2, _sut.ScanEnumerations, "past the half-hour mark scans again");
    }

    [TestMethod]
    public void FrameTick_AmbushTriggers_BreaksCampAndShowsStrikeInquiry()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1", straightLine: 3f, navDistance: 4f);
        _sut.RandomRoll = 0f;

        _sut.FrameTick();

        Assert.IsNull(_sut.PlayerCamp, "the trap is spent the moment it triggers (source)");
        Assert.AreEqual(1, _sut.AmbushInquiryCount);
        Assert.AreEqual(true, _sut.LastAmbushSuccess);
        Assert.AreEqual("bandit_1", _sut.LastAmbushEnemy);
        Assert.AreEqual(0, _sut.Penalties.Count, "penalties wait for the player's strike confirm");
        Assert.AreEqual(0, _sut.BattleTargets.Count);
        _supply.Received(1).CancelCampOrders();
    }

    [TestMethod]
    public void AmbushAttackConfirmed_Success_PenalizesTargetThenStartsBattle()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1", straightLine: 3f, navDistance: 4f);
        _sut.RandomRoll = 0f;
        _sut.FrameTick();

        _sut.PendingAmbushAttack();

        CollectionAssert.AreEqual(new[] { "bandit_1:0.5" }, _sut.Penalties);
        CollectionAssert.AreEqual(new[] { "bandit_1" }, _sut.BattleTargets);
    }

    [TestMethod]
    public void FrameTick_AmbushSpotted_OffersAttackAnywayWithoutPenalty()
    {
        _ambushMath.TriggerChance(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(0.2f);
        EstablishCamp(CampType.Ambush);
        AddCandidate("lord_1", isBandit: false);
        _sut.RandomRoll = 0.9f;

        _sut.FrameTick();

        Assert.IsNull(_sut.PlayerCamp, "a spotted trap is spent too");
        Assert.AreEqual(false, _sut.LastAmbushSuccess);

        _sut.PendingAmbushAttack();

        Assert.AreEqual(0, _sut.Penalties.Count, "a spotted ambush attacks without the softening edge");
        CollectionAssert.AreEqual(new[] { "lord_1" }, _sut.BattleTargets);
    }

    [TestMethod]
    public void AmbushDeclined_NoBattleNoPenalty()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1");
        _sut.RandomRoll = 0f;
        _sut.FrameTick();

        _sut.PendingAmbushHold();

        Assert.AreEqual(0, _sut.Penalties.Count);
        Assert.AreEqual(0, _sut.BattleTargets.Count);
    }

    [TestMethod]
    public void AmbushAttackConfirmed_TargetAlreadyFighting_NoBattle()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1");
        _sut.RandomRoll = 0f;
        _sut.FrameTick();
        _sut.CandidateInEvent = true;

        _sut.PendingAmbushAttack();

        Assert.AreEqual(0, _sut.BattleTargets.Count, "source guard: both parties must be free of a map event");
        Assert.AreEqual(0, _sut.Penalties.Count);
    }

    [TestMethod]
    public void AmbushAttackConfirmed_PlayerAlreadyInEncounter_NoBattle()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1");
        _sut.RandomRoll = 0f;
        _sut.FrameTick();
        _sut.InEncounter = true;

        _sut.PendingAmbushAttack();

        Assert.AreEqual(0, _sut.BattleTargets.Count);
    }

    [TestMethod]
    public void AmbushInquiryOpen_SuppressesNewScans()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1");
        _sut.RandomRoll = 0f;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.AmbushInquiryCount, "arrange: inquiry is up, camp broken");

        // The player re-arms a fresh ambush while the inquiry is still open; no second scan (or
        // stacked inquiry) may run until the first is answered (the source's _inquiryPending).
        EstablishCamp(CampType.Ambush);
        _sut.NowHours = 10.0;
        _sut.FrameTick();

        Assert.AreEqual(1, _sut.AmbushInquiryCount);

        _sut.PendingAmbushHold();
        _sut.NowHours = 20.0;
        _sut.FrameTick();
        Assert.AreEqual(2, _sut.AmbushInquiryCount, "answered inquiry releases the scan latch");
    }

    [TestMethod]
    public void FrameTick_AmbushChance_ReceivesThePlayerSpottingRange()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1", straightLine: 3f, navDistance: 4f);
        _sut.SpottingRange = 5f;
        _sut.Scouting = 30f;

        _sut.FrameTick();

        // Source formula: the first argument is the PLAYER party's spotting range (candidate
        // distance never enters the odds); pinned so the seam cannot silently regress either way.
        _ambushMath.Received(1).TriggerChance(5f, 10f, 0.5f, 30f);
    }

    [TestMethod]
    public void FrameTick_CandidateWithoutName_InquiryFallsBackToTheEnemy()
    {
        // The boundary no longer renders a name per scanned party (round-B cost finding); the
        // winner's name resolves lazily from EngineParty, and a null handle (or a nameless
        // party) must land on the generic fallback, never a null in the inquiry text.
        EstablishCamp(CampType.Ambush);
        var candidate = AddCandidate("bandit_1", straightLine: 3f, navDistance: 4f);
        candidate.Name = null;
        _sut.RandomRoll = 0f;

        _sut.FrameTick();

        Assert.AreEqual(1, _sut.AmbushInquiryCount);
        StringAssert.Contains(_sut.LastAmbushEnemy, "the enemy");
    }

    [TestMethod]
    public void FrameTick_StraightLineBeyondReach_SkipsPathfinding()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("far_party", straightLine: 50f, navDistance: 4f);

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.NavDistanceCalls, "prefilter must spare the pathfinder");
        Assert.IsNotNull(_sut.PlayerCamp);
    }

    [TestMethod]
    public void FrameTick_NavDistanceBeyondReach_NoTrigger()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("detour_party", straightLine: 3f, navDistance: 50f);

        _sut.FrameTick();

        Assert.AreEqual(1, _sut.NavDistanceCalls);
        Assert.IsNotNull(_sut.PlayerCamp);
    }

    [TestMethod]
    public void FrameTick_BanditBiasWinsAtSimilarDistance()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("lord_1", straightLine: 3f, navDistance: 4.0f, isBandit: false);
        AddCandidate("bandit_1", straightLine: 3f, navDistance: 4.4f, isBandit: true);
        _sut.RandomRoll = 0f;

        _sut.FrameTick();

        Assert.AreEqual("bandit_1", _sut.LastAmbushEnemy, "the bandit bias picks the prey");
        _sut.PendingAmbushAttack();
        CollectionAssert.AreEqual(new[] { "bandit_1:0.5" }, _sut.Penalties);
    }

    [TestMethod]
    public void FrameTick_FieldCamp_NeverScans()
    {
        EstablishCamp(CampType.Field);
        AddCandidate();

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.ScanEnumerations);
    }

    [TestMethod]
    public void FrameTick_AmbushStillRaising_NoScan()
    {
        EstablishCamp(CampType.Ambush);
        _sut.CampReady = false;
        AddCandidate();

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.ScanEnumerations);
    }

    [TestMethod]
    public void FrameTick_PlayerInEncounter_NoScan()
    {
        EstablishCamp(CampType.Ambush);
        _sut.InEncounter = true;
        AddCandidate();

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.ScanEnumerations);
    }

    [TestMethod]
    public void FrameTick_FeatureDisabled_MoveGuardStillProtects_ScanStops()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate();
        _settings.Enabled.Returns(false);
        _sut.Moving = true;

        _sut.FrameTick();

        // The overlay button hides while the toggle is off, so the move guard is the only break
        // path left; it must keep working. The ambush scan is a gameplay effect and stops.
        Assert.AreEqual(0, _sut.ScanEnumerations);
        Assert.AreEqual(1, _sut.InquiryCount, "the break-camp guard protects state even while off");
    }

    [TestMethod]
    public void HourlyTick_FeatureDisabled_SettlementEntryStillFoldsTheCamp()
    {
        EstablishCamp();
        _settings.Enabled.Returns(false);
        _sut.InSettlement = true;

        _sut.HourlyTick();

        Assert.IsNull(_sut.PlayerCamp, "state-protecting folds run regardless of the toggle");
    }

    // --- captivity ---

    [TestMethod]
    public void HourlyTick_PlayerCaptured_BreaksCampCleanly()
    {
        EstablishCamp();
        _sut.Captive = true;

        _sut.HourlyTick();

        Assert.IsNull(_sut.PlayerCamp);
        _visuals.Received(1).Remove("main_party");
        _supply.Received(1).CancelCampOrders();
        Assert.AreEqual(0, _sut.MoraleAdded.Count, "no camp effects tick through captivity");
    }

    [TestMethod]
    public void FrameTick_PlayerCaptured_BreaksCampBeforeAnyGuardOrScan()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate();
        _sut.Captive = true;
        _sut.Moving = true;

        _sut.FrameTick();

        Assert.IsNull(_sut.PlayerCamp);
        Assert.AreEqual(0, _sut.ScanEnumerations);
        Assert.AreEqual(0, _sut.InquiryCount, "no move-guard inquiry while captive");
    }

    // --- session reset + transient hygiene ---

    [TestMethod]
    public void ResetForNewSession_ClearsBookAndVisuals()
    {
        EstablishCamp();

        _sut.ResetForNewSession();

        Assert.IsNull(_sut.PlayerCamp, "a new session must not inherit the previous campaign's camp");
        _visuals.Received(1).ClearAll();
        _supply.DidNotReceive().CancelCampOrders();
    }

    [TestMethod]
    public void LoadFrom_ResetsTheAmbushScanClock()
    {
        EstablishCamp(CampType.Ambush);
        _sut.NowHours = 1000.0;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.ScanEnumerations, "arrange: scan ran, deadline now 1000.5");

        // Load a save whose game time is far behind the deadline; a stale clock would stall
        // every scan for 500 in-game hours.
        _sut.SaveInto(out var book);
        _sut.LoadFrom(book);
        _sut.NowHours = 500.0;
        _sut.FrameTick();

        Assert.AreEqual(2, _sut.ScanEnumerations);
    }

    [TestMethod]
    public void LoadFrom_ResetsTheMoveGuardLatch()
    {
        EstablishCamp();
        _sut.Moving = true;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.InquiryCount, "arrange: latch is up");

        // Quit-to-menu with the inquiry showing, then load a camped save: a stale latch would
        // permanently kill the guard.
        _sut.SaveInto(out var book);
        _sut.LoadFrom(book);
        _sut.FrameTick();

        Assert.AreEqual(2, _sut.InquiryCount);
    }

    [TestMethod]
    public void LoadFrom_NullRow_DroppedAndLoggedNotInstalled()
    {
        var book = new Dictionary<string, CampState>
        {
            ["main_party"] = null,
            ["other_party"] = new CampState { TypeEnum = CampType.Field },
        };

        _sut.LoadFrom(book);

        Assert.IsNull(_sut.PlayerCamp, "the null row is dropped, not dereferenced later");
        _logger.Received(1).LogWarning(Arg.Any<string>());

        // The surviving row still loads; ticks run over the scrubbed book without throwing.
        _sut.FrameTick();
        _sut.HourlyTick();
    }

    // --- establish/ready announcements ---

    [TestMethod]
    public void FrameTick_RaiseCompletes_AnnouncesOnce()
    {
        _sut.CampReady = false;
        EstablishCamp();
        _sut.FrameTick();
        Assert.AreEqual(0, _sut.Messages.Count, "nothing announced while raising");

        _sut.CampReady = true;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.MessageTexts.Count);
        StringAssert.Contains(_sut.MessageTexts[0], "established");

        _sut.FrameTick();
        Assert.AreEqual(1, _sut.Messages.Count, "announced exactly once per raise");
    }

    [TestMethod]
    public void LoadFrom_ReadyCamp_DoesNotReannounce()
    {
        EstablishCamp();
        _sut.SaveInto(out var book);
        _sut.LoadFrom(book);

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.Messages.Count, "a loaded ready camp was announced in its own session");
    }

    [TestMethod]
    public void LoadFrom_MidRaiseCamp_AnnouncesOnCompletion()
    {
        EstablishCamp();
        _sut.SaveInto(out var book);
        _sut.CampReady = false;
        _sut.LoadFrom(book);
        _sut.FrameTick();
        Assert.AreEqual(0, _sut.Messages.Count);

        _sut.CampReady = true;
        _sut.FrameTick();

        Assert.AreEqual(1, _sut.Messages.Count, "a save mid-raise still gets its completion message");
    }

    // --- wind ticker driver ---

    [TestMethod]
    public void FrameTick_VisualStanding_PollsIsShownAsTheWindDriver()
    {
        EstablishCamp();

        _sut.FrameTick();

        // Once the layout stands the IsShown poll is the steady-state driver for the banner-cloth
        // wind ticker; without it the flags hang limp shortly after placement.
        _visuals.Received(1).IsShown("main_party");
    }

    [TestMethod]
    public void FrameTick_VisualRetry_UntilSceneReady()
    {
        _visuals.Show(Arg.Any<string>(), Arg.Any<CampType>(), Arg.Any<Vec2>()).Returns(false);
        var camp = EstablishCamp();

        _sut.FrameTick();
        Assert.IsFalse(camp.VisualShown);
        _visuals.Received(2).Show("main_party", CampType.Field, Arg.Any<Vec2>());

        _visuals.Show(Arg.Any<string>(), Arg.Any<CampType>(), Arg.Any<Vec2>()).Returns(true);
        _sut.FrameTick();
        Assert.IsTrue(camp.VisualShown);

        _sut.FrameTick();
        _visuals.Received(3).Show("main_party", CampType.Field, Arg.Any<Vec2>());
    }

    // --- persistence plumbing ---

    [TestMethod]
    public void SaveInto_RoundTripsTheCampBook()
    {
        EstablishCamp();

        _sut.SaveInto(out var camps);

        Assert.IsTrue(camps.ContainsKey("main_party"));
    }

    [TestMethod]
    public void LoadFrom_Null_ResetsToEmptyBook()
    {
        EstablishCamp();

        _sut.LoadFrom(null);

        Assert.IsNull(_sut.PlayerCamp);
    }

    [TestMethod]
    public void OnGameLoaded_ResetsVisualFlagsAndReshows()
    {
        var camp = new CampState { TypeEnum = CampType.Field, VisualShown = true };
        _sut.LoadFrom(new Dictionary<string, CampState> { ["main_party"] = camp });

        _sut.OnGameLoaded();

        Assert.IsTrue(camp.VisualShown, "re-shown once the visual service reported success");
        _visuals.Received(1).Show("main_party", CampType.Field, Arg.Any<Vec2>());
    }

    [TestMethod]
    public void OnGameLoaded_SceneNotReady_LeavesFlagClearForFrameRetry()
    {
        _visuals.Show(Arg.Any<string>(), Arg.Any<CampType>(), Arg.Any<Vec2>()).Returns(false);
        var camp = new CampState { TypeEnum = CampType.Field, VisualShown = true };
        _sut.LoadFrom(new Dictionary<string, CampState> { ["main_party"] = camp });

        _sut.OnGameLoaded();

        Assert.IsFalse(camp.VisualShown, "persisted flag must not suppress the post-load rebuild");
    }
}
