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
        public TerrainType Terrain = TerrainType.Plain;
        public float NearestFortDistance = 100f;
        public int Gold = 10_000;
        public int TroopCount = 50;
        public float Scouting;
        public float SpottingRange = 5f;
        public double NowHours;
        public float ElapsedHours = 10f;
        public bool CampReady = true;
        public float RandomRoll;
        public List<AmbushCandidate> Candidates = new List<AmbushCandidate>();
        public Dictionary<string, float> NavDistances = new Dictionary<string, float>();

        public int HoldCalls;
        public int TrackCalls;
        public int UntrackCalls;
        public int ScanEnumerations;
        public int NavDistanceCalls;
        public int InquiryCount;
        public int ResumeMoveCalls;
        public readonly List<int> Charges = new List<int>();
        public readonly List<float> MoraleAdded = new List<float>();
        public readonly List<int> GrainAdded = new List<int>();
        public readonly List<string> Messages = new List<string>();
        public readonly List<string> Penalties = new List<string>();
        public Action PendingBreakAndMove;
        public Action PendingStayCamped;

        public TestableCampService(
            ICampSettingsProvider settings,
            ICampTerrainService terrain,
            ICampAmbushService ambush,
            ICampVisualService visuals,
            ISupplyOrderService supplyOrders,
            IEnlistmentStateQuery enlistment,
            IEnumerable<ICampOverlayContributor> overlayContributors,
            IModLogger logger)
            : base(settings, terrain, ambush, visuals, supplyOrders, enlistment, overlayContributors, logger)
        {
        }

        protected override string MainPartyId() => PartyId;
        protected override bool IsMainPartyMoving() => Moving;
        protected override bool IsMainPartyInSettlement() => InSettlement;
        protected override bool IsMainPartyInEncounter() => InEncounter;
        protected override Vec2 MainPartyPosition() => default;
        protected override void HoldMainParty() => HoldCalls++;
        protected override CampaignVec2 CaptureMoveTarget() => default;
        protected override void ResumeMoveTo(CampaignVec2 target) => ResumeMoveCalls++;
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
        protected override float ElapsedHoursSince(CampaignTime time) => ElapsedHours;
        protected override bool IsCampReady(CampState camp) => CampReady;
        protected override void TrackMainPartyOnMap() => TrackCalls++;
        protected override void UntrackMainPartyOnMap() => UntrackCalls++;

        protected override IReadOnlyList<AmbushCandidate> EnumerateHostileCandidates()
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

        protected override void ShowMessage(TextObject text, bool error) =>
            Messages.Add(error ? "error" : "info");
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
            new[] { _contributor }, _logger);
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
    public void Fortify_LongStandingCamp_UsesDoubleSetupBuildHours()
    {
        EstablishCamp();
        _sut.ElapsedHours = 20f;

        Assert.IsTrue(_sut.Fortify());

        Assert.AreEqual(8f, _sut.PlayerCamp.BuildHours, 0.0001f);
    }

    [TestMethod]
    public void Fortify_FreshlyReadyCamp_ClampsBuildHoursToElapsedSoReadinessSurvives()
    {
        EstablishCamp();
        _sut.ElapsedHours = 5f;

        Assert.IsTrue(_sut.Fortify());

        // elapsed (5h) < double setup (8h): build hours clamp to elapsed so progress stays >= 1
        // and the paid-for camp never regresses to "still being set up".
        Assert.AreEqual(5f, _sut.PlayerCamp.BuildHours, 0.0001f);
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
    public void BreakPlayerCamp_CancelsSuppliesRemovesVisualAndUntracks()
    {
        EstablishCamp();

        _sut.BreakPlayerCamp();

        Assert.IsNull(_sut.PlayerCamp);
        _supply.Received(1).CancelAll();
        _visuals.Received(1).Remove("main_party");
        Assert.AreEqual(1, _sut.UntrackCalls);
    }

    [TestMethod]
    public void BreakPlayerCamp_NoCamp_SafeAndCancelsNothing()
    {
        _sut.BreakPlayerCamp();

        _supply.DidNotReceive().CancelAll();
        Assert.AreEqual(0, _sut.UntrackCalls);
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
        _supply.Received(1).CancelAll();
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
        _supply.DidNotReceive().CancelAll();
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
    public void FrameTick_ConfirmBreak_BreaksCampAndResumesMove()
    {
        EstablishCamp();
        _sut.Moving = true;
        _sut.FrameTick();

        _sut.PendingBreakAndMove();

        Assert.IsNull(_sut.PlayerCamp);
        Assert.AreEqual(1, _sut.ResumeMoveCalls);
        _supply.Received(1).CancelAll();
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
    public void FrameTick_AmbushSprings_BreaksCampAndPenalizesTarget()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate("bandit_1", straightLine: 3f, navDistance: 4f);
        _sut.RandomRoll = 0f;

        _sut.FrameTick();

        Assert.IsNull(_sut.PlayerCamp, "a sprung trap is spent");
        CollectionAssert.AreEqual(new[] { "bandit_1:0.5" }, _sut.Penalties);
        CollectionAssert.AreEqual(new[] { "info" }, _sut.Messages);
        _supply.Received(1).CancelAll();
    }

    [TestMethod]
    public void FrameTick_AmbushSpotted_BreaksCampWithoutPenalty()
    {
        _ambushMath.TriggerChance(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(0.2f);
        EstablishCamp(CampType.Ambush);
        AddCandidate("lord_1", isBandit: false);
        _sut.RandomRoll = 0.9f;

        _sut.FrameTick();

        Assert.IsNull(_sut.PlayerCamp, "a spotted trap is spent too");
        Assert.AreEqual(0, _sut.Penalties.Count);
        CollectionAssert.AreEqual(new[] { "error" }, _sut.Messages);
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
    public void FrameTick_FeatureDisabled_NoWork()
    {
        EstablishCamp(CampType.Ambush);
        AddCandidate();
        _settings.Enabled.Returns(false);
        _sut.Moving = true;

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.ScanEnumerations);
        Assert.AreEqual(0, _sut.InquiryCount);
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
