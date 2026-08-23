using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Decision-path coverage for <see cref="RefugeService"/>: the CanFound precedence matrix, the
/// limit formula, founding/upgrade/dismantle sequencing, the frame-tick build advancement with
/// the hold-nearby rule, persisted militia bookkeeping, raid gating, and the two-way load
/// reconcile. Campaign statics are overridden on a test subclass; those virtual bodies are the
/// honest untested boundary sliver, exercised only in game.
/// </summary>
[TestClass]
public class RefugeServiceTests
{
    private sealed class TestableRefugeService : RefugeService
    {
        public string MainParty = "main_party";
        public int Gold = 10_000;
        public int ClanTier;
        public bool CampReady = true;
        public readonly Dictionary<string, float> Progress = new Dictionary<string, float>();
        public double NowHours;
        public double Days;
        public float RandomRoll = 1f;
        public int RollCalls;
        public readonly Dictionary<string, float> DistancesFromMain = new Dictionary<string, float>();
        public float FortDistance = 100f;
        public float FortDistanceFromRefuge = 100f;
        public bool SpawnFails;
        public string SpawnIdOverride;
        public string SpawnedWardenId;
        public readonly List<string> Events = new List<string>();
        public readonly List<int> Charges = new List<int>();
        public int HoldCalls;
        public List<string> LiveParties = new List<string>();
        public readonly List<string> Pinned = new List<string>();
        public string MilitiaTroop = "militia_troop";
        public int Garrison;
        public readonly Dictionary<string, int> TroopPresent = new Dictionary<string, int>();
        public readonly List<string> TroopAdds = new List<string>();
        public readonly List<string> TroopRemovals = new List<string>();
        public readonly HashSet<string> PartiesInMapEvent = new HashSet<string>();
        public RaidThreat Threat;
        public int ThreatSearches;
        public readonly List<string> RaidsStarted = new List<string>();
        public readonly List<string> MergedParties = new List<string>();
        public readonly List<string> DestroyedParties = new List<string>();
        public readonly List<bool> Messages = new List<bool>();

        public TestableRefugeService(
            IRefugeSettingsProvider settings,
            IWardenService wardens,
            ICampService camps,
            IEnlistmentStateQuery enlistment,
            IRefugeVisualService visuals,
            IModLogger logger)
            : base(settings, wardens, camps, enlistment, visuals, logger)
        {
        }

        protected override string MainPartyId() => MainParty;
        protected override int PlayerGold => Gold;

        protected override void ChargePlayer(int amount)
        {
            Charges.Add(amount);
            Events.Add("charge");
        }

        protected override int PlayerClanTier() => ClanTier;
        protected override bool IsCampReady(CampState camp) => CampReady;

        protected override float BuildProgressOf(RefugeData data) =>
            Progress.TryGetValue(data.PartyId, out var progress) ? progress : 0f;

        protected override CampaignTime NowTime() => default;
        protected override double NowInHours() => NowHours;
        protected override double DaysSince(CampaignTime time) => Days;

        protected override float NextRandomFloat()
        {
            RollCalls++;
            return RandomRoll;
        }

        protected override float DistanceFromMainPartyTo(string partyId) =>
            DistancesFromMain.TryGetValue(partyId, out var distance) ? distance : float.MaxValue;

        protected override Vec2 PartyPosition(string partyId) => default;
        protected override float DistanceToNearestFortification() => FortDistance;
        protected override float DistanceToNearestFortificationFrom(string partyId) => FortDistanceFromRefuge;

        protected override string SpawnRefugeParty(string stringId, string wardenHeroId)
        {
            Events.Add("spawn:" + stringId);
            SpawnedWardenId = wardenHeroId;
            if (SpawnFails)
                return null;
            return SpawnIdOverride ?? stringId;
        }

        public bool AttachThrows;

        protected override void AttachWarden(string partyId, string wardenHeroId)
        {
            Events.Add("attach:" + wardenHeroId);
            if (AttachThrows)
                throw new System.InvalidOperationException("engine attach refusal");
        }

        public readonly List<int> Refunds = new List<int>();

        protected override void RefundPlayer(int amount)
        {
            Refunds.Add(amount);
            Events.Add("refund");
        }

        public bool WardenWithParty = true;

        protected override bool IsWardenWithParty(string partyId, string wardenHeroId) => WardenWithParty;

        public readonly List<string> DisbandCancels = new List<string>();

        protected override void CancelDisband(string partyId)
        {
            DisbandCancels.Add(partyId);
            Events.Add("canceldisband:" + partyId);
        }

        protected override void MergeRefugeIntoMainParty(string partyId)
        {
            MergedParties.Add(partyId);
            Events.Add("merge");
        }

        protected override void DestroyRefugeParty(string partyId)
        {
            DestroyedParties.Add(partyId);
            Events.Add("destroy");
        }

        protected override void HoldMainParty() => HoldCalls++;
        protected override IReadOnlyList<string> AllRefugePartyIds() => LiveParties;
        protected override void PinRefugePartyAi(string partyId) => Pinned.Add(partyId);
        protected override string ResolveMilitiaTroopId(string partyId) => MilitiaTroop;
        protected override int RefugeGarrisonCount(string partyId) => Garrison;

        protected override int GetTroopCountInRefuge(string partyId, string troopId) =>
            TroopPresent.TryGetValue(partyId + ":" + troopId, out var count) ? count : 0;

        protected override void AddTroopsToRefuge(string partyId, string troopId, int count)
        {
            TroopAdds.Add(partyId + ":" + troopId + ":" + count);
            Events.Add("rally:" + partyId);
        }

        protected override void RemoveTroopsFromRefuge(string partyId, string troopId, int count) =>
            TroopRemovals.Add(partyId + ":" + troopId + ":" + count);

        protected override bool IsPartyInMapEvent(string partyId) => PartiesInMapEvent.Contains(partyId);

        protected override RaidThreat FindNearestHostile(string refugePartyId, float range)
        {
            ThreatSearches++;
            return Threat;
        }

        protected override void StartRaid(RaidThreat threat, string refugePartyId)
        {
            RaidsStarted.Add(refugePartyId);
            Events.Add("raid:" + refugePartyId);
        }

        public readonly List<string> PeaceReleasedParties = new List<string>();

        protected override void ReleasePeacePrisoners(string partyId) =>
            PeaceReleasedParties.Add(partyId);

        protected override void ShowMessage(TextObject text, bool error) => Messages.Add(error);
    }

    private IRefugeSettingsProvider _settings;
    private IWardenService _wardens;
    private ICampService _camps;
    private IEnlistmentStateQuery _enlistment;
    private IRefugeVisualService _visuals;
    private IModLogger _logger;
    private TestableRefugeService _sut;
    private Dictionary<string, RefugeData> _book;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IRefugeSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.FoundCost.Returns(2000);
        _settings.StrongholdUpgradeCost.Returns(5000);
        _settings.BuildHours.Returns(6f);
        _settings.MaxRefugesCap.Returns(3);
        _settings.ManageRange.Returns(4f);
        _settings.MinTownDistance.Returns(16f);
        _settings.StrongholdMinTownDistance.Returns(26f);
        _settings.MilitiaBase.Returns(6);
        _settings.MilitiaMax.Returns(40);
        _settings.EnableRaids.Returns(false);
        _settings.RaidRange.Returns(6f);

        _wardens = Substitute.For<IWardenService>();
        _wardens.AnyAvailable().Returns(true);

        _camps = Substitute.For<ICampService>();
        _camps.PlayerCamp.Returns(new CampState { TypeEnum = CampType.Field });

        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _enlistment.IsEnlisted.Returns(false);

        _visuals = Substitute.For<IRefugeVisualService>();
        _visuals.Show(Arg.Any<string>(), Arg.Any<RefugeTier>(), Arg.Any<bool>(), Arg.Any<Vec2>())
            .Returns(true);

        _logger = Substitute.For<IModLogger>();

        _sut = new TestableRefugeService(_settings, _wardens, _camps, _enlistment, _visuals, _logger);
        _book = new Dictionary<string, RefugeData>();

        // Order-sensitive collaborators log into the same event stream as the subclass seams.
        _camps.When(c => c.BreakPlayerCamp()).Do(_ => _sut.Events.Add("break"));
        _wardens.When(w => w.ReleaseWarden(Arg.Any<string>(), Arg.Any<bool>()))
            .Do(_ => _sut.Events.Add("release"));
    }

    private RefugeData Seed(
        string id = "r1",
        bool ready = true,
        RefugeTier tier = RefugeTier.Refuge,
        bool building = false,
        bool upgrade = false)
    {
        var data = new RefugeData
        {
            PartyId = id,
            TierEnum = tier,
            Established = ready,
            Building = building,
            BuildingUpgrade = upgrade,
            WardenHeroId = "warden_1",
        };
        _book[id] = data;
        // LoadFrom keeps the reference, so repeated seeding into _book stays visible.
        _sut.LoadFrom(_book, 0);
        return data;
    }

    // --- RefugeLimit ---

    [TestMethod]
    public void RefugeLimit_ScalesWithClanTierUnderTheCap()
    {
        Assert.AreEqual(1, _sut.RefugeLimit(0));
        Assert.AreEqual(1, _sut.RefugeLimit(1));
        Assert.AreEqual(2, _sut.RefugeLimit(2));
        Assert.AreEqual(2, _sut.RefugeLimit(3));
        Assert.AreEqual(3, _sut.RefugeLimit(4));
        Assert.AreEqual(3, _sut.RefugeLimit(10), "the hard cap binds above tier 4");
    }

    [TestMethod]
    public void RefugeLimit_NegativeTier_FloorsAtOne()
    {
        Assert.AreEqual(1, _sut.RefugeLimit(-6));
    }

    [TestMethod]
    public void RefugeLimit_DegenerateCap_FloorsAtOne()
    {
        _settings.MaxRefugesCap.Returns(0);

        Assert.AreEqual(1, _sut.RefugeLimit(10));
    }

    // --- CanFound precedence ---

    [TestMethod]
    public void CanFound_FeatureDisabled_WinsOverEveryOtherBlock()
    {
        _settings.Enabled.Returns(false);
        _enlistment.IsEnlisted.Returns(true);
        _camps.PlayerCamp.Returns((CampState)null);
        _sut.Gold = 0;

        Assert.AreEqual(RefugeBlockReason.FeatureDisabled, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_NoMainParty_FeatureDisabled()
    {
        _sut.MainParty = null;

        Assert.AreEqual(RefugeBlockReason.FeatureDisabled, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_Enlisted_WinsOverRefugeAlreadyHere()
    {
        _enlistment.IsEnlisted.Returns(true);
        Seed("r1");
        _sut.DistancesFromMain["r1"] = 1f;

        Assert.AreEqual(RefugeBlockReason.Enlisted, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_RefugeAlreadyHere_WinsOverMissingCamp()
    {
        Seed("r1");
        _sut.DistancesFromMain["r1"] = 1f;
        _camps.PlayerCamp.Returns((CampState)null);

        Assert.AreEqual(RefugeBlockReason.RefugeAlreadyHere, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_BuildingRefugeNearby_AlsoBlocks()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 1f;

        Assert.AreEqual(RefugeBlockReason.RefugeAlreadyHere, _sut.CanFound(),
            "a raising refuge occupies the spot as much as a finished one");
    }

    [TestMethod]
    public void CanFound_NoCamp_NoReadyCampHere()
    {
        _camps.PlayerCamp.Returns((CampState)null);

        Assert.AreEqual(RefugeBlockReason.NoReadyCampHere, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_AmbushOrLookoutCamp_WrongCampType()
    {
        _camps.PlayerCamp.Returns(new CampState { TypeEnum = CampType.Ambush });
        Assert.AreEqual(RefugeBlockReason.WrongCampType, _sut.CanFound());

        _camps.PlayerCamp.Returns(new CampState { TypeEnum = CampType.Lookout });
        Assert.AreEqual(RefugeBlockReason.WrongCampType, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_CampStillRaising_NoReadyCampHere()
    {
        _sut.CampReady = false;

        Assert.AreEqual(RefugeBlockReason.NoReadyCampHere, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_AtRefugeLimit_Blocks()
    {
        // Tier 0 limit is 1; the standing refuge is far away, so the limit is the only block.
        Seed("r_far");

        Assert.AreEqual(RefugeBlockReason.AtRefugeLimit, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_TooCloseToTown_Blocks()
    {
        _sut.FortDistance = 5f;

        Assert.AreEqual(RefugeBlockReason.TooCloseToTown, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_MinTownDistanceZero_DisablesProximityCheck()
    {
        _settings.MinTownDistance.Returns(0f);
        _sut.FortDistance = 0.1f;

        Assert.AreEqual(RefugeBlockReason.None, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_NoWardenAvailable_Blocks()
    {
        _wardens.AnyAvailable().Returns(false);

        Assert.AreEqual(RefugeBlockReason.NoWardenAvailable, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_NotEnoughGold_Blocks()
    {
        _sut.Gold = 1999;

        Assert.AreEqual(RefugeBlockReason.NotEnoughGold, _sut.CanFound());
    }

    [TestMethod]
    public void CanFound_AllClear_ReturnsNone()
    {
        Assert.AreEqual(RefugeBlockReason.None, _sut.CanFound());
    }

    // --- Found ---

    [TestMethod]
    public void Found_AllClear_SpawnsAttachesChargesThenBreaksCamp()
    {
        var data = _sut.Found("warden_1", out var reason);

        Assert.AreEqual(RefugeBlockReason.None, reason);
        Assert.IsNotNull(data);
        CollectionAssert.AreEqual(
            new[] { "spawn:taom_refuge_0", "attach:warden_1", "charge", "break" },
            _sut.Events);
        CollectionAssert.AreEqual(new[] { 2000 }, _sut.Charges);
    }

    [TestMethod]
    public void Found_StartsTheRaiseUnestablished()
    {
        var data = _sut.Found("warden_1", out _);

        Assert.AreEqual("taom_refuge_0", data.PartyId);
        Assert.IsTrue(data.Building);
        Assert.IsFalse(data.BuildingUpgrade);
        Assert.IsFalse(data.Established);
        Assert.IsFalse(data.IsReady);
        Assert.AreEqual(6f, data.BuildTargetHours, 0.0001f);
        Assert.AreEqual("warden_1", data.WardenHeroId);
        Assert.AreEqual(RefugeTier.Refuge, data.TierEnum);
        Assert.AreEqual(1, _sut.AllRefuges.Count);
    }

    [TestMethod]
    public void Found_FromFieldCamp_NotFortified()
    {
        Assert.IsFalse(_sut.Found("warden_1", out _).Fortified);
    }

    [TestMethod]
    public void Found_FromFortifiedCamp_MarksFortified()
    {
        _camps.PlayerCamp.Returns(new CampState { TypeEnum = CampType.Fortified });

        Assert.IsTrue(_sut.Found("warden_1", out _).Fortified);
    }

    [TestMethod]
    public void Found_Blocked_ReturnsNullWithReasonAndNoSideEffects()
    {
        _sut.Gold = 0;

        var data = _sut.Found("warden_1", out var reason);

        Assert.IsNull(data);
        Assert.AreEqual(RefugeBlockReason.NotEnoughGold, reason);
        Assert.AreEqual(0, _sut.Events.Count);
    }

    [TestMethod]
    public void Found_NullWarden_FailsAsNoWardenWithoutSpawning()
    {
        var data = _sut.Found(null, out var reason);

        Assert.IsNull(data);
        Assert.AreEqual(RefugeBlockReason.NoWardenAvailable, reason);
        Assert.AreEqual(0, _sut.Events.Count);
    }

    [TestMethod]
    public void Found_SpawnRefused_AbortsBeforeChargeOrCampBreak()
    {
        _sut.SpawnFails = true;

        var data = _sut.Found("warden_1", out var reason);

        Assert.IsNull(data);
        Assert.AreEqual(RefugeBlockReason.None, reason, "engine refusal has no player-facing cause");
        CollectionAssert.AreEqual(new[] { "spawn:taom_refuge_0" }, _sut.Events,
            "no charge, no camp break: the player keeps camp and gold");
        Assert.AreEqual(0, _sut.AllRefuges.Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Found_BooksUnderTheEngineReturnedId()
    {
        _sut.SpawnIdOverride = "taom_refuge_0_1";

        var data = _sut.Found("warden_1", out _);

        Assert.AreEqual("taom_refuge_0_1", data.PartyId,
            "CreateParty may uniquify the requested id; the book must key on the real one");
        Assert.IsNotNull(_sut.GetByPartyId("taom_refuge_0_1"));
    }

    [TestMethod]
    public void Found_CounterAdvancesAcrossFoundings()
    {
        _sut.ClanTier = 10; // limit 3, so a second founding is legal

        _sut.Found("warden_1", out _);
        _sut.Found("warden_2", out var reason);

        Assert.AreEqual(RefugeBlockReason.None, reason);
        Assert.IsNotNull(_sut.GetByPartyId("taom_refuge_0"));
        Assert.IsNotNull(_sut.GetByPartyId("taom_refuge_1"));
    }

    // --- NearestManageable ---

    [TestMethod]
    public void NearestManageable_ReadyRefugeInRange_Returned()
    {
        var data = Seed("r1");
        _sut.DistancesFromMain["r1"] = 2f;

        Assert.AreSame(data, _sut.NearestManageable());
    }

    [TestMethod]
    public void NearestManageable_PicksTheCloserOfTwo()
    {
        Seed("r1");
        var closer = Seed("r2");
        _sut.DistancesFromMain["r1"] = 3f;
        _sut.DistancesFromMain["r2"] = 1f;

        Assert.AreSame(closer, _sut.NearestManageable());
    }

    [TestMethod]
    public void NearestManageable_BuildingRefuge_NotManageable()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 1f;

        Assert.IsNull(_sut.NearestManageable());
    }

    [TestMethod]
    public void NearestManageable_BeyondManageRange_Null()
    {
        Seed("r1");
        _sut.DistancesFromMain["r1"] = 5f;

        Assert.IsNull(_sut.NearestManageable());
    }

    // --- CanUpgrade / Upgrade ---

    [TestMethod]
    public void CanUpgrade_NullOrUnbookedRefuge_NoRefugeInReach()
    {
        Assert.AreEqual(RefugeBlockReason.NoRefugeInReach, _sut.CanUpgrade(null));
        Assert.AreEqual(
            RefugeBlockReason.NoRefugeInReach,
            _sut.CanUpgrade(new RefugeData { PartyId = "ghost", Established = true }));
    }

    [TestMethod]
    public void CanUpgrade_FeatureDisabled_Blocks()
    {
        var data = Seed("r1");
        _settings.Enabled.Returns(false);

        Assert.AreEqual(RefugeBlockReason.FeatureDisabled, _sut.CanUpgrade(data));
    }

    [TestMethod]
    public void CanUpgrade_StillRaising_StillBuilding()
    {
        var data = Seed("r1", ready: false, building: true);

        Assert.AreEqual(RefugeBlockReason.StillBuilding, _sut.CanUpgrade(data));
    }

    [TestMethod]
    public void CanUpgrade_AlreadyStronghold_AlreadyTopTier()
    {
        var data = Seed("r1", tier: RefugeTier.Stronghold);

        Assert.AreEqual(RefugeBlockReason.AlreadyTopTier, _sut.CanUpgrade(data));
    }

    [TestMethod]
    public void CanUpgrade_TooCloseForAStronghold_Blocks()
    {
        var data = Seed("r1");
        _sut.FortDistanceFromRefuge = 20f; // inside the 26 stronghold keep-out, outside founding's 16

        Assert.AreEqual(RefugeBlockReason.TooCloseToTown, _sut.CanUpgrade(data));
    }

    [TestMethod]
    public void CanUpgrade_NotEnoughGold_Blocks()
    {
        var data = Seed("r1");
        _sut.Gold = 4999;

        Assert.AreEqual(RefugeBlockReason.NotEnoughGold, _sut.CanUpgrade(data));
    }

    [TestMethod]
    public void Upgrade_AllClear_ChargesAndEntersRebuildPhase()
    {
        var data = Seed("r1");

        Assert.IsTrue(_sut.Upgrade(data));

        CollectionAssert.AreEqual(new[] { 5000 }, _sut.Charges);
        Assert.IsTrue(data.Building);
        Assert.IsTrue(data.BuildingUpgrade);
        Assert.AreEqual(6f, data.BuildTargetHours, 0.0001f);
        Assert.AreEqual(RefugeTier.Refuge, data.TierEnum, "the tier flips only when the rebuild finishes");
        Assert.IsFalse(data.IsReady, "the rebuild window suspends readiness by design");
    }

    [TestMethod]
    public void Upgrade_Blocked_ReturnsFalseWithoutCharging()
    {
        var data = Seed("r1");
        _sut.Gold = 0;

        Assert.IsFalse(_sut.Upgrade(data));
        Assert.AreEqual(0, _sut.Charges.Count);
        Assert.IsFalse(data.Building);
    }

    // --- Dismantle ---

    [TestMethod]
    public void Dismantle_ReleasesWardenBeforeMergingThenDestroys()
    {
        var data = Seed("r1");

        _sut.Dismantle(data);

        CollectionAssert.AreEqual(new[] { "release", "merge", "destroy" }, _sut.Events,
            "the companion must leave via a real action before his roster element is copied");
        _wardens.Received(1).ReleaseWarden("warden_1", false);
        CollectionAssert.AreEqual(new[] { "r1" }, _sut.MergedParties);
        CollectionAssert.AreEqual(new[] { "r1" }, _sut.DestroyedParties);
        Assert.AreEqual(0, _sut.AllRefuges.Count);
        _visuals.Received(1).Remove("r1");
        CollectionAssert.AreEqual(new[] { false }, _sut.Messages);
    }

    [TestMethod]
    public void Dismantle_PromotedWarden_PassesTheFlagThrough()
    {
        var data = Seed("r1");
        data.WardenPromoted = true;
        data.PromotedFromTroopId = "troop_1";

        _sut.Dismantle(data);

        _wardens.Received(1).ReleaseWarden("warden_1", true);
    }

    [TestMethod]
    public void Dismantle_UnbookedOrNull_SafeNoOp()
    {
        _sut.Dismantle(null);
        _sut.Dismantle(new RefugeData { PartyId = "ghost" });

        Assert.AreEqual(0, _sut.Events.Count);
        _wardens.DidNotReceive().ReleaseWarden(Arg.Any<string>(), Arg.Any<bool>());
    }

    // --- FrameTick: builds ---

    [TestMethod]
    public void FrameTick_RaiseComplete_EstablishesAndShowsVisual()
    {
        var data = Seed("r1", ready: false, building: true);
        _sut.Progress["r1"] = 1f;

        _sut.FrameTick();

        Assert.IsTrue(data.Established);
        Assert.IsFalse(data.Building);
        Assert.IsTrue(data.IsReady);
        CollectionAssert.AreEqual(new[] { false }, _sut.Messages, "the raise announces itself");
        _visuals.Received(1).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
    }

    [TestMethod]
    public void FrameTick_RaiseIncomplete_StaysBuilding()
    {
        var data = Seed("r1", ready: false, building: true);
        _sut.Progress["r1"] = 0.5f;

        _sut.FrameTick();

        Assert.IsTrue(data.Building);
        Assert.IsFalse(data.Established);
    }

    [TestMethod]
    public void FrameTick_UpgradeComplete_BecomesStrongholdAndRebuildsVisual()
    {
        var data = Seed("r1", ready: true, building: true, upgrade: true);
        _sut.Progress["r1"] = 1f;

        _sut.FrameTick();

        Assert.AreEqual(RefugeTier.Stronghold, data.TierEnum);
        Assert.IsFalse(data.Building);
        Assert.IsFalse(data.BuildingUpgrade);
        Assert.IsTrue(data.IsReady);
        _visuals.Received(1).Remove("r1");
        _visuals.Received(1).Show("r1", RefugeTier.Stronghold, false, Arg.Any<Vec2>());
    }

    // --- FrameTick: hold-nearby rule ---

    [TestMethod]
    public void FrameTick_BuildingRefugeNearby_HoldsPartyAndSaysWhyOnce()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 2f;

        _sut.FrameTick();
        _sut.NowHours = 0.02;
        _sut.FrameTick();

        Assert.AreEqual(2, _sut.HoldCalls, "the hold re-applies every work tick");
        Assert.AreEqual(1, _sut.Messages.Count,
            "FIX over the source's silent pin: one note per build, not a message storm");
    }

    [TestMethod]
    public void FrameTick_BuildingRefugeFarAway_DoesNotHold()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 10f;

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.HoldCalls);
    }

    [TestMethod]
    public void FrameTick_ReadyRefugeNearby_DoesNotHold()
    {
        Seed("r1");
        _sut.DistancesFromMain["r1"] = 1f;

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.HoldCalls);
    }

    [TestMethod]
    public void FrameTick_UpgradeRebuild_GetsItsOwnHoldNote()
    {
        var data = Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 2f;
        _sut.Progress["r1"] = 0.5f;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.Messages.Count, "arrange: founding build note shown");

        // Finish the raise, then start the stronghold rebuild.
        _sut.Progress["r1"] = 1f;
        _sut.NowHours = 0.02;
        _sut.FrameTick();
        _sut.Gold = 10_000;
        Assert.IsTrue(_sut.Upgrade(data), "arrange: upgrade accepted");
        _sut.Progress["r1"] = 0.1f;
        _sut.NowHours = 0.04;

        _sut.FrameTick();

        // founding note + raise-complete + rebuild note = 3 info messages
        Assert.AreEqual(3, _sut.Messages.Count, "the rebuild is a new build phase with its own note");
    }

    // --- FrameTick: throttle + visual retries ---

    [TestMethod]
    public void FrameTick_SameGameTime_RunsWorkOnlyOnce()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 2f;

        _sut.FrameTick();
        _sut.FrameTick();
        _sut.FrameTick();

        Assert.AreEqual(1, _sut.HoldCalls, "game time did not advance, so the work is throttled");
    }

    [TestMethod]
    public void FrameTick_VisualRetry_UntilSceneReady()
    {
        _visuals.Show(Arg.Any<string>(), Arg.Any<RefugeTier>(), Arg.Any<bool>(), Arg.Any<Vec2>())
            .Returns(false);
        Seed("r1");

        _sut.FrameTick();
        _sut.NowHours = 0.02;
        _sut.FrameTick();
        _visuals.Received(2).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());

        _visuals.Show(Arg.Any<string>(), Arg.Any<RefugeTier>(), Arg.Any<bool>(), Arg.Any<Vec2>())
            .Returns(true);
        _sut.NowHours = 0.04;
        _sut.FrameTick();
        _sut.NowHours = 0.06;
        _sut.FrameTick();

        _visuals.Received(3).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
    }

    [TestMethod]
    public void FrameTick_FortifiedRefuge_ShowsFortifiedLayout()
    {
        var data = Seed("r1");
        data.Fortified = true;

        _sut.FrameTick();

        _visuals.Received(1).Show("r1", RefugeTier.Refuge, true, Arg.Any<Vec2>());
    }

    // --- Militia rally ---

    [TestMethod]
    public void OnMapEventStarted_ReadyRefuge_AddsClampedMilitiaAndRecordsIt()
    {
        var data = Seed("r1");
        _sut.Days = 3;
        _sut.Garrison = 25;
        _sut.TroopPresent["r1:militia_troop"] = 4; // the player garrisoned 4 of the same type

        _sut.OnMapEventStarted("r1");

        // base 6 + stronghold 0 + age 3 + garrison 25/10=2 -> 11
        CollectionAssert.AreEqual(new[] { "r1:militia_troop:11" }, _sut.TroopAdds);
        Assert.AreEqual(11, data.MilitiaAdded);
        Assert.AreEqual("militia_troop", data.MilitiaTroopId);
        Assert.AreEqual(4, data.MilitiaPreRallyCount,
            "the pre-rally baseline is what stand-down protects from removal");
    }

    [TestMethod]
    public void OnMapEventStarted_StrongholdBonusAndAgeCap()
    {
        Seed("r1", tier: RefugeTier.Stronghold);
        _sut.Days = 40; // capped at 15

        _sut.OnMapEventStarted("r1");

        // base 6 + stronghold 10 + age 15 + garrison 0 -> 31
        CollectionAssert.AreEqual(new[] { "r1:militia_troop:31" }, _sut.TroopAdds);
    }

    [TestMethod]
    public void OnMapEventStarted_TotalAboveMax_ClampsToMilitiaMax()
    {
        Seed("r1", tier: RefugeTier.Stronghold);
        _sut.Days = 15;
        _sut.Garrison = 200;

        _sut.OnMapEventStarted("r1");

        // 6 + 10 + 15 + 20 = 51 -> clamped to 40
        CollectionAssert.AreEqual(new[] { "r1:militia_troop:40" }, _sut.TroopAdds);
    }

    [TestMethod]
    public void OnMapEventStarted_AlreadyBoosted_SkipsSecondRally()
    {
        var data = Seed("r1");
        data.MilitiaAdded = 5;
        data.MilitiaTroopId = "militia_troop";

        _sut.OnMapEventStarted("r1");

        Assert.AreEqual(0, _sut.TroopAdds.Count,
            "persisted bookkeeping: a mid-battle save must not double-rally on load");
        Assert.AreEqual(5, data.MilitiaAdded);
    }

    [TestMethod]
    public void OnMapEventStarted_RefugeStillRaising_NoMilitia()
    {
        Seed("r1", ready: false, building: true);

        _sut.OnMapEventStarted("r1");

        Assert.AreEqual(0, _sut.TroopAdds.Count);
    }

    [TestMethod]
    public void OnMapEventStarted_UnknownParty_NoMilitia()
    {
        _sut.OnMapEventStarted("some_lord_party");
        _sut.OnMapEventStarted(null);

        Assert.AreEqual(0, _sut.TroopAdds.Count);
    }

    [TestMethod]
    public void OnMapEventStarted_NoMilitiaTroopDefined_NoRally()
    {
        var data = Seed("r1");
        _sut.MilitiaTroop = null;

        _sut.OnMapEventStarted("r1");

        Assert.AreEqual(0, _sut.TroopAdds.Count);
        Assert.AreEqual(0, data.MilitiaAdded);
    }

    [TestMethod]
    public void OnMapEventStarted_ZeroCount_NothingRecorded()
    {
        _settings.MilitiaBase.Returns(0);
        var data = Seed("r1");
        _sut.Days = 0;
        _sut.Garrison = 0;

        _sut.OnMapEventStarted("r1");

        Assert.AreEqual(0, _sut.TroopAdds.Count);
        Assert.AreEqual(0, data.MilitiaAdded);
        Assert.IsNull(data.MilitiaTroopId);
    }

    [TestMethod]
    public void OnMapEventEnded_LossesCase_RemovesOnlyTheMilitiaSurvivors()
    {
        var data = Seed("r1");
        data.MilitiaAdded = 11;
        data.MilitiaTroopId = "militia_troop";
        data.MilitiaPreRallyCount = 0; // no garrison of this type before the rally
        _sut.TroopPresent["r1:militia_troop"] = 7; // battle losses ate 4

        _sut.OnMapEventEnded("r1");

        CollectionAssert.AreEqual(new[] { "r1:militia_troop:7" }, _sut.TroopRemovals);
        Assert.AreEqual(0, data.MilitiaAdded);
        Assert.IsNull(data.MilitiaTroopId);
        Assert.AreEqual(0, data.MilitiaPreRallyCount, "bookkeeping fully reset for the next battle");
    }

    [TestMethod]
    public void OnMapEventEnded_GarrisonedSameTypeWithCasualties_NeverRemovesTheGarrisonStack()
    {
        var data = Seed("r1");
        data.MilitiaAdded = 11;
        data.MilitiaTroopId = "militia_troop";
        data.MilitiaPreRallyCount = 9; // the player's own 9 stood in the stack before the rally
        _sut.TroopPresent["r1:militia_troop"] = 13; // 20 fought, 7 died

        _sut.OnMapEventEnded("r1");

        // Casualties attribute to militia first: survivors above the baseline = 13 - 9 = 4.
        // The old min(recorded, present) removed 11 here, deleting 7 of the player's 9.
        CollectionAssert.AreEqual(new[] { "r1:militia_troop:4" }, _sut.TroopRemovals,
            "only militia survivors above the pre-rally baseline stand down");
    }

    [TestMethod]
    public void OnMapEventEnded_GarrisonedSameTypeNoCasualties_RemovesExactlyTheMilitia()
    {
        var data = Seed("r1");
        data.MilitiaAdded = 11;
        data.MilitiaTroopId = "militia_troop";
        data.MilitiaPreRallyCount = 9;
        _sut.TroopPresent["r1:militia_troop"] = 20; // nobody died

        _sut.OnMapEventEnded("r1");

        CollectionAssert.AreEqual(new[] { "r1:militia_troop:11" }, _sut.TroopRemovals,
            "a bloodless battle stands down the full militia and leaves the garrison's 9");
    }

    [TestMethod]
    public void OnMapEventEnded_EverybodyDied_RemovesNothing()
    {
        var data = Seed("r1");
        data.MilitiaAdded = 11;
        data.MilitiaTroopId = "militia_troop";
        data.MilitiaPreRallyCount = 9;
        _sut.TroopPresent["r1:militia_troop"] = 0;

        _sut.OnMapEventEnded("r1");

        Assert.AreEqual(0, _sut.TroopRemovals.Count);
        Assert.AreEqual(0, data.MilitiaAdded);
    }

    [TestMethod]
    public void OnMapEventEnded_NothingRecorded_NoRemoval()
    {
        Seed("r1");

        _sut.OnMapEventEnded("r1");

        Assert.AreEqual(0, _sut.TroopRemovals.Count);
    }

    [TestMethod]
    public void MilitiaBookkeeping_SurvivesASaveRoundTrip()
    {
        var data = Seed("r1");
        _sut.OnMapEventStarted("r1");
        Assert.IsTrue(data.MilitiaAdded > 0, "arrange: rally recorded");

        // A save/load between battle start and end: the same dictionary rides SyncData.
        _sut.SaveInto(out var savedBook, out var savedCounter);
        _sut.LoadFrom(savedBook, savedCounter);
        _sut.TroopPresent["r1:militia_troop"] = 3;

        _sut.OnMapEventEnded("r1");

        CollectionAssert.AreEqual(new[] { "r1:militia_troop:3" }, _sut.TroopRemovals);
        Assert.AreEqual(0, savedBook["r1"].MilitiaAdded);
    }

    // --- Raids ---

    [TestMethod]
    public void HourlyTick_RaidsOffByDefault_NothingRollsOrSearches()
    {
        Seed("r1");
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.RollCalls);
        Assert.AreEqual(0, _sut.ThreatSearches);
        Assert.AreEqual(0, _sut.RaidsStarted.Count);
    }

    [TestMethod]
    public void HourlyTick_RollAtBoundary_Raids()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0.05f; // the source's boundary: RandomFloat > 0.05 skips, so 0.05 raids
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        CollectionAssert.AreEqual(new[] { "r1" }, _sut.RaidsStarted);
        CollectionAssert.AreEqual(new[] { true }, _sut.Messages, "the attack warning is an error-red line");
    }

    [TestMethod]
    public void HourlyTick_RollAboveChance_NoRaid()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0.051f;
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        Assert.AreEqual(1, _sut.RollCalls);
        Assert.AreEqual(0, _sut.ThreatSearches, "a failed roll must not pay for the party scan");
        Assert.AreEqual(0, _sut.RaidsStarted.Count);
    }

    [TestMethod]
    public void HourlyTick_NaNRoll_SkipsViaPositiveGate()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = float.NaN;
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.RaidsStarted.Count);
    }

    [TestMethod]
    public void HourlyTick_ReCancelsDisbandForEveryBookedRefuge_EvenWithRaidsAndToggleOff()
    {
        // MbEvent dispatches LIFO, so the OnPartyDisbandStarted cancel runs BEFORE vanilla's
        // queue-add and strikes an empty queue; this hourly idempotent re-cancel is what actually
        // empties vanilla's 1-day disband queue in a continuous session (round-C HIGH). It is
        // state-protecting: raids and the master toggle being off must not stop it.
        _settings.Enabled.Returns(false);
        _settings.EnableRaids.Returns(false);
        Seed("r1");
        Seed("r2");

        _sut.HourlyTick();

        CollectionAssert.AreEquivalent(
            new[] { "r1", "r2" }, _sut.DisbandCancels,
            "every booked refuge gets the idempotent re-cancel each hour");
    }

    [TestMethod]
    public void LoadFrom_FutureBuildStart_ResetsSoTheBuildCanFinish()
    {
        // A future BuildStartTime keeps the elapsed numerator negative for the whole offset:
        // never ready, not dismantlable, a cap slot gone - the mirror of the NaN-target
        // absorbing state the load repair exists to kill.
        _sut.Days = -41.0; // DaysSince(start) negative = the start is ahead of campaign time
        // CampaignTime.Never is the only campaign-independent nonzero CampaignTime a test can
        // construct: every Hours/Days factory multiplies by internal statics a live campaign
        // initializes, so they all yield default (0 ticks) here.
        var future = CampaignTime.Never;
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData
            {
                PartyId = "r1",
                Established = true,
                Building = true,
                BuildStartTime = future,
                BuildTargetHours = 6f,
            },
        };

        _sut.LoadFrom(book, 1);

        var row = _sut.GetByPartyId("r1");
        // No CampaignTime.ToString in asserts: it needs a live Campaign. The harness pins
        // NowTime() to default, so the repair must have landed exactly there.
        Assert.IsFalse(row.BuildStartTime.Equals(future), "future start must be reset");
        Assert.IsTrue(row.BuildStartTime.Equals(default(CampaignTime)), "reset lands on NowTime()");
    }

    [TestMethod]
    public void HourlyTick_RefugeAlreadyFighting_NoSecondBattle()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0f;
        _sut.PartiesInMapEvent.Add("r1");
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.RaidsStarted.Count);
    }

    [TestMethod]
    public void HourlyTick_RefugeStillRaising_NotRaidable()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1", ready: false, building: true);
        _sut.RandomRoll = 0f;
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        Assert.AreEqual(0, _sut.RollCalls, "an unraised refuge is not on the raid table at all");
        Assert.AreEqual(0, _sut.RaidsStarted.Count);
    }

    [TestMethod]
    public void HourlyTick_NoHostileInRange_NoRaidNoMessage()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0f;
        _sut.Threat = null;

        _sut.HourlyTick();

        Assert.AreEqual(1, _sut.ThreatSearches);
        Assert.AreEqual(0, _sut.RaidsStarted.Count);
        Assert.AreEqual(0, _sut.Messages.Count);
    }

    [TestMethod]
    public void HourlyTick_RalliesMilitiaBeforeStartingTheRaid()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0f;
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };

        _sut.HourlyTick();

        // The leading canceldisband is the hourly state-protecting re-cancel pass (round C);
        // the assertion's point is rally BEFORE raid, which must stay in this order.
        CollectionAssert.AreEqual(new[] { "canceldisband:r1", "rally:r1", "raid:r1" }, _sut.Events,
            "MapEventParty captures NumberOfHealthyMembers at construction, so militia added "
            + "after StartBattleAction never reaches an auto-resolve's participant count");
    }

    [TestMethod]
    public void OnMapEventStarted_AfterRaidPathRallied_DoesNotDoubleRally()
    {
        _settings.EnableRaids.Returns(true);
        Seed("r1");
        _sut.RandomRoll = 0f;
        _sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };
        _sut.HourlyTick();
        Assert.AreEqual(1, _sut.TroopAdds.Count, "arrange: the raid path rallied");

        _sut.OnMapEventStarted("r1");

        Assert.AreEqual(1, _sut.TroopAdds.Count,
            "the MapEventStarted callback for the raid battle must see MilitiaAdded != 0 and skip");
    }

    // --- Map-event gating (manage/dismantle/enter) ---

    [TestMethod]
    public void NearestManageable_RefugeInMapEvent_Excluded()
    {
        Seed("r1");
        _sut.DistancesFromMain["r1"] = 1f;
        Assert.IsNotNull(_sut.NearestManageable(), "arrange: manageable when idle");

        _sut.PartiesInMapEvent.Add("r1");

        Assert.IsNull(_sut.NearestManageable(),
            "a refuge mid-battle is a live participant; its rosters belong to the event");
    }

    [TestMethod]
    public void Dismantle_RefugeInMapEvent_RefusedNoOp()
    {
        var data = Seed("r1");
        _sut.PartiesInMapEvent.Add("r1");

        _sut.Dismantle(data);

        Assert.AreEqual(0, _sut.Events.Count, "no release, no merge, no destroy mid-battle");
        Assert.IsNotNull(_sut.GetByPartyId("r1"));
    }

    // --- NearestDismantlable (the orphan exit) ---

    [TestMethod]
    public void NearestDismantlable_IncludesOrphanAdoptedRows()
    {
        var orphan = Seed("r_orphan", ready: false, building: false);
        _sut.DistancesFromMain["r_orphan"] = 1f;

        Assert.IsNull(_sut.NearestManageable(), "an orphan is never manageable");
        Assert.AreSame(orphan, _sut.NearestDismantlable(),
            "dismantle is the orphan's only exit; without it the row eats a cap slot forever");
    }

    [TestMethod]
    public void NearestDismantlable_BuildingRefuge_StillExcluded()
    {
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 1f;

        Assert.IsNull(_sut.NearestDismantlable(),
            "a raising refuge is neither ready nor orphaned; it finishes or it is not touched");
    }

    [TestMethod]
    public void NearestDismantlable_InMapEvent_Excluded()
    {
        Seed("r_orphan", ready: false, building: false);
        _sut.DistancesFromMain["r_orphan"] = 1f;
        _sut.PartiesInMapEvent.Add("r_orphan");

        Assert.IsNull(_sut.NearestDismantlable());
    }

    [TestMethod]
    public void Dismantle_OrphanAdoptedRow_Works()
    {
        var orphan = Seed("r_orphan", ready: false, building: false);

        _sut.Dismantle(orphan);

        Assert.IsNull(_sut.GetByPartyId("r_orphan"));
        CollectionAssert.AreEqual(new[] { "r_orphan" }, _sut.DestroyedParties);
    }

    // --- OnPartyDestroyed (engine destroyed the refuge party mid-session) ---

    [TestMethod]
    public void OnPartyDestroyed_DropsRowAndVisualsAndTellsThePlayer()
    {
        Seed("r1");
        Seed("r2");

        _sut.OnPartyDestroyed("r1");

        Assert.IsNull(_sut.GetByPartyId("r1"), "the row must not count against the cap all session");
        Assert.IsNotNull(_sut.GetByPartyId("r2"));
        _visuals.Received(1).Remove("r1");
        CollectionAssert.AreEqual(new[] { true }, _sut.Messages, "the fall is an error-red line");
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("warden_1")));
    }

    [TestMethod]
    public void OnPartyDestroyed_FreesTheCapSlot()
    {
        _settings.MaxRefugesCap.Returns(1);
        Seed("r1");
        Assert.AreEqual(RefugeBlockReason.AtRefugeLimit, _sut.CanFound(), "arrange: at cap");

        _sut.OnPartyDestroyed("r1");

        Assert.AreEqual(RefugeBlockReason.None, _sut.CanFound(),
            "a fallen refuge must not block founding its replacement until a reload");
    }

    [TestMethod]
    public void OnPartyDestroyed_UnknownOrNullParty_NoOp()
    {
        Seed("r1");

        _sut.OnPartyDestroyed("some_lord_party");
        _sut.OnPartyDestroyed(null);

        Assert.IsNotNull(_sut.GetByPartyId("r1"));
        Assert.AreEqual(0, _sut.Messages.Count);
    }

    // --- OnPeaceMade (refuge-held hero prisoners; vanilla never enumerates custom components) ---

    [TestMethod]
    public void OnPeaceMade_ReleasesForEveryRefuge()
    {
        Seed("r1");
        Seed("r2");

        _sut.OnPeaceMade();

        CollectionAssert.AreEquivalent(new[] { "r1", "r2" }, _sut.PeaceReleasedParties);
    }

    // --- ResetForNewSession (the singleton-leak fix) ---

    [TestMethod]
    public void ResetForNewSession_ClearsBookCounterAndVisuals()
    {
        _sut.ClanTier = 10; // limit = cap = 3, so seeding + founding both fit
        Seed("r1");
        var founded = _sut.Found("warden_1", out _); // advances the counter to 1
        Assert.IsNotNull(founded, "arrange: campaign A founded taom_refuge_0");

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.AllRefuges.Count, "campaign B must not inherit campaign A's book");
        _visuals.Received(1).ClearAll();
        Assert.IsNotNull(_sut.Found("warden_2", out _));
        Assert.IsNotNull(_sut.GetByPartyId("taom_refuge_0"), "the counter restarted at 0");
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheHoldNoteFlagTransient()
    {
        var data = Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 1f;
        _sut.FrameTick();
        Assert.AreEqual(1, _sut.Messages.Count, "arrange: the hold note was shown once");

        _sut.ResetForNewSession();
        // Re-add on the LIVE dictionary reference (no LoadFrom, which clears the latch itself):
        // only ResetForNewSession can have cleared the hold-note latch here.
        _book["r1"] = data;
        _sut.NowHours = 1.0;
        _sut.FrameTick();

        Assert.AreEqual(2, _sut.Messages.Count,
            "a stale hold-note latch would silently pin the new campaign's party");
    }

    // --- OnGameLoaded reconcile ---

    [TestMethod]
    public void OnGameLoaded_RowWithoutParty_DroppedWithWarning()
    {
        Seed("r1");
        Seed("r_ghost");
        _sut.LiveParties = new List<string> { "r1" };

        _sut.OnGameLoaded();

        Assert.IsNotNull(_sut.GetByPartyId("r1"));
        Assert.IsNull(_sut.GetByPartyId("r_ghost"));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("r_ghost")));
    }

    [TestMethod]
    public void OnGameLoaded_PartyWithoutRow_AdoptedUnestablishedWithWarning()
    {
        Seed("r1");
        _sut.LiveParties = new List<string> { "r1", "r_orphan" };

        _sut.OnGameLoaded();

        var adopted = _sut.GetByPartyId("r_orphan");
        Assert.IsNotNull(adopted);
        Assert.IsFalse(adopted.Established, "an orphan is adopted un-established, never granted readiness");
        Assert.AreEqual(RefugeTier.Refuge, adopted.TierEnum);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("r_orphan")));
    }

    [TestMethod]
    public void OnGameLoaded_RepinsAiOnEveryRefugeParty()
    {
        Seed("r1");
        Seed("r2");
        _sut.LiveParties = new List<string> { "r1", "r2" };

        _sut.OnGameLoaded();

        CollectionAssert.AreEquivalent(new[] { "r1", "r2" }, _sut.Pinned,
            "the source only pinned at spawn, so loaded refuges wandered");
    }

    [TestMethod]
    public void OnGameLoaded_ClearsVisualStateSoLayoutsRebuild()
    {
        Seed("r1");
        _sut.FrameTick();
        _visuals.Received(1).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
        _sut.LiveParties = new List<string> { "r1" };

        _sut.OnGameLoaded();
        _sut.NowHours = 0.02;
        _sut.FrameTick();

        _visuals.Received(2).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
    }

    // --- persistence plumbing + the book seam ---

    [TestMethod]
    public void LoadFrom_Null_ResetsToEmptyBookAndCounter()
    {
        Seed("r1");

        _sut.LoadFrom(null, -5);

        Assert.AreEqual(0, _sut.AllRefuges.Count);
        _sut.Found("warden_1", out _);
        Assert.IsNotNull(_sut.GetByPartyId("taom_refuge_0"), "a negative counter resets to 0");
    }

    [TestMethod]
    public void LoadFrom_NullRows_ScrubbedWithOneWarning()
    {
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData { PartyId = "r1", Established = true },
            ["r_null"] = null,
            ["r_null2"] = null,
        };

        _sut.LoadFrom(book, 2);

        Assert.AreEqual(1, _sut.AllRefuges.Count,
            "a hostile or half-recovered save's null rows must not reach FrameTick's dereference");
        Assert.IsNotNull(_sut.GetByPartyId("r1"));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("2")));
    }

    [TestMethod]
    public void SaveInto_HandsBackTheLiveBookAndCounter()
    {
        _sut.Found("warden_1", out _);

        _sut.SaveInto(out var book, out var counter);

        Assert.AreEqual(1, counter);
        Assert.IsTrue(book.ContainsKey("taom_refuge_0"));
    }

    [TestMethod]
    public void GetByPartyId_KnownUnknownAndNull()
    {
        var data = Seed("r1");

        Assert.AreSame(data, _sut.GetByPartyId("r1"));
        Assert.IsNull(_sut.GetByPartyId("nope"));
        Assert.IsNull(_sut.GetByPartyId(null));
    }

    // --- Master toggle off: state-protecting frame work runs, gameplay effects stop ---

    [TestMethod]
    public void FrameTick_Disabled_StillFinishesTheBuild()
    {
        _settings.Enabled.Returns(false);
        var data = Seed("r1", ready: false, building: true);
        _sut.Progress["r1"] = 1f;

        _sut.FrameTick();

        Assert.IsTrue(data.IsReady,
            "a mid-build toggle-off froze the refuge forever (round B MED); builds must finish regardless");
        _visuals.Received(1).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
    }

    [TestMethod]
    public void FrameTick_Disabled_StillRetriesVisualsAndTicksWind()
    {
        _settings.Enabled.Returns(false);
        Seed("r1");

        _sut.FrameTick();

        _visuals.Received(1).TickWind();
        _visuals.Received(1).Show("r1", RefugeTier.Refuge, false, Arg.Any<Vec2>());
    }

    [TestMethod]
    public void FrameTick_Disabled_DoesNotHoldThePartyOrNoteIt()
    {
        _settings.Enabled.Returns(false);
        Seed("r1", ready: false, building: true);
        _sut.DistancesFromMain["r1"] = 2f;
        _sut.Progress["r1"] = 0.5f;

        _sut.FrameTick();

        Assert.AreEqual(0, _sut.HoldCalls, "the hold-nearby pin is a gameplay effect and gates on Enabled");
        Assert.AreEqual(0, _sut.Messages.Count);
    }

    // --- LoadFrom row repair (hostile or damaged saves) ---

    [TestMethod]
    public void LoadFrom_DivergentInnerPartyId_CanonicalizedToTheDictionaryKey()
    {
        // Swapped inner ids would make Dismantle destroy a DIFFERENT party than the one the
        // player stands at; the dictionary key is the identity every lookup resolves.
        var data = new RefugeData { PartyId = "r2", Established = true };
        _book["r1"] = data;

        _sut.LoadFrom(_book, 0);

        Assert.AreEqual("r1", data.PartyId);
    }

    [TestMethod]
    public void LoadFrom_NaNBuildTarget_FlooredSoTheBuildCanFinish()
    {
        // A NaN target fails every comparison, so the row could never reach BuildProgress >= 1:
        // a permanent absorbing state consuming a cap slot (Codex round 2 #4).
        var data = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = float.NaN };
        _book["r1"] = data;

        _sut.LoadFrom(_book, 0);

        Assert.AreEqual(0.1f, data.BuildTargetHours);
    }

    [TestMethod]
    public void LoadFrom_InfiniteBuildTarget_FlooredSoTheBuildCanFinish()
    {
        var data = new RefugeData
        {
            PartyId = "r1", Building = true, BuildTargetHours = float.PositiveInfinity,
        };
        _book["r1"] = data;

        _sut.LoadFrom(_book, 0);

        Assert.AreEqual(0.1f, data.BuildTargetHours);
    }

    [TestMethod]
    public void LoadFrom_UnknownTierAndNegativeMilitia_Repaired()
    {
        var data = new RefugeData
        {
            PartyId = "r1", Tier = 99, MilitiaAdded = -5, MilitiaPreRallyCount = -3,
        };
        _book["r1"] = data;

        _sut.LoadFrom(_book, 0);

        Assert.AreEqual(RefugeTier.Refuge, data.TierEnum);
        Assert.AreEqual(0, data.MilitiaAdded);
        Assert.AreEqual(0, data.MilitiaPreRallyCount);
    }

    // --- Found is transactional after the party exists (Codex round 2 #12) ---

    [TestMethod]
    public void Found_ThrowAfterSpawn_RollsBackWardenGoldAndParty()
    {
        _sut.AttachThrows = true;

        var refuge = _sut.Found("hero_1", out var reason);

        Assert.IsNull(refuge);
        Assert.AreEqual(RefugeBlockReason.None, reason);
        Assert.AreEqual(0, _sut.Charges.Count, "the throw came before the charge; nothing to refund");
        Assert.AreEqual(0, _sut.Refunds.Count);
        _wardens.Received(1).ReleaseWarden("hero_1", false);
        CollectionAssert.AreEqual(new[] { "taom_refuge_0" }, _sut.DestroyedParties);
        Assert.AreEqual(0, _sut.AllRefuges.Count, "no ghost row may survive a failed founding");
    }

    [TestMethod]
    public void Found_ThrowInCampBreak_RefundsTheChargeToo()
    {
        bool breakThrew = false;
        _camps.When(c => c.BreakPlayerCamp()).Do(_ =>
        {
            breakThrew = true;
            throw new System.InvalidOperationException("camp break refusal");
        });

        var refuge = _sut.Found("hero_1", out _);

        Assert.IsNull(refuge);
        Assert.IsTrue(breakThrew);
        CollectionAssert.AreEqual(new[] { 2000 }, _sut.Charges);
        CollectionAssert.AreEqual(new[] { 2000 }, _sut.Refunds, "the charge completed, so it must unwind");
        _wardens.Received(1).ReleaseWarden("hero_1", false);
        CollectionAssert.AreEqual(new[] { "taom_refuge_0" }, _sut.DestroyedParties);
        Assert.AreEqual(0, _sut.AllRefuges.Count);
    }

    // --- Warden death: the engine's disband flow is cancelled (Codex round 2 #10) ---

    [TestMethod]
    public void OnPartyDisbandStarted_WardenStillWithParty_CancelsAndKeepsTheRow()
    {
        var data = Seed("r1");
        _sut.WardenWithParty = true;

        _sut.OnPartyDisbandStarted("r1");

        CollectionAssert.AreEqual(new[] { "r1" }, _sut.DisbandCancels,
            "a refuge never disbands; dismantle is its only exit");
        Assert.IsTrue(data.IsReady, "the row is untouched while the warden stands");
        Assert.AreEqual("warden_1", data.WardenHeroId);
        Assert.AreEqual(0, _sut.Messages.Count);
    }

    [TestMethod]
    public void OnPartyDisbandStarted_WardenGone_OrphanAdoptsTheLeaderlessRow()
    {
        // Vanilla starts this flow itself when the warden dies with no other hero in the roster
        // (KillCharacterAction.MakeDead -> RemovePartyLeader -> DisbandPartyAction.StartDisband).
        var data = Seed("r1");
        _sut.WardenWithParty = false;

        _sut.OnPartyDisbandStarted("r1");

        CollectionAssert.AreEqual(new[] { "r1" }, _sut.DisbandCancels);
        Assert.IsNull(data.WardenHeroId);
        Assert.IsTrue(data.IsOrphanAdopted, "leaderless = dismantle-only, never ready again");
        Assert.IsFalse(data.IsReady);
        CollectionAssert.AreEqual(new[] { true }, _sut.Messages, "the player is told, as an alert");
    }

    [TestMethod]
    public void OnPartyDisbandStarted_MidBuildWardenGone_BuildIsAbandonedIntoOrphanState()
    {
        var data = Seed("r1", ready: false, building: true);
        _sut.WardenWithParty = false;

        _sut.OnPartyDisbandStarted("r1");

        Assert.IsTrue(data.IsOrphanAdopted);
        Assert.IsFalse(data.Building);
    }

    [TestMethod]
    public void OnPartyDisbandStarted_UnknownOrNullParty_NoOp()
    {
        _sut.OnPartyDisbandStarted("nope");
        _sut.OnPartyDisbandStarted(null);

        Assert.AreEqual(0, _sut.DisbandCancels.Count);
    }

    [TestMethod]
    public void OnGameLoaded_CancelsAnyPersistedDisbandOnEveryRefugeParty()
    {
        // A save written inside the engine's 1-day disband-wait window would flip IsDisbanding an
        // hour after load; the belt clears the queue entry (no-op for parties never queued).
        Seed("r1");
        Seed("r2");
        _sut.LiveParties = new List<string> { "r1", "r2" };

        _sut.OnGameLoaded();

        CollectionAssert.AreEquivalent(new[] { "r1", "r2" }, _sut.DisbandCancels);
    }

    // --- The dismantle hero-merge contract (no hero row ever rides a raw roster copy) ---

    /// <summary>Overrides ONLY the small merge seams, so <c>MergeRefugeIntoMainParty</c> runs the
    /// REAL orchestration body. TroopRoster.Add + Clear fires the engine's OnHeroRemoved, which
    /// nulls Hero.PartyBelongedTo unconditionally; the ordering pinned here (every hero and hero
    /// prisoner moved by engine action BEFORE the bulk copy touches anything) is the fix.</summary>
    private sealed class HeroMergeProbeService : RefugeService
    {
        public readonly List<string> MergeEvents = new List<string>();
        public List<string> Heroes = new List<string>();
        public List<string> HeroPrisoners = new List<string>();

        public HeroMergeProbeService(
            IRefugeSettingsProvider settings,
            IWardenService wardens,
            ICampService camps,
            IEnlistmentStateQuery enlistment,
            IRefugeVisualService visuals,
            IModLogger logger)
            : base(settings, wardens, camps, enlistment, visuals, logger)
        {
        }

        protected override IReadOnlyList<string> HeroesInRefugeRoster(string partyId) => Heroes;

        protected override void MoveRefugeHeroToMainParty(string heroId) =>
            MergeEvents.Add("hero:" + heroId);

        protected override IReadOnlyList<string> HeroPrisonersInRefuge(string partyId) => HeroPrisoners;

        protected override void TransferHeroPrisonerToMainParty(string partyId, string heroId) =>
            MergeEvents.Add("prisoner:" + heroId);

        protected override void BulkMergeRegularsIntoMainParty(string partyId) =>
            MergeEvents.Add("bulk:" + partyId);

        protected override void DestroyRefugeParty(string partyId) =>
            MergeEvents.Add("destroy:" + partyId);

        protected override bool IsPartyInMapEvent(string partyId) => false;

        protected override void ShowMessage(TextObject text, bool error)
        {
        }
    }

    [TestMethod]
    public void Dismantle_MovesEveryHeroAndHeroPrisonerByActionBeforeTheBulkCopy()
    {
        var probe = new HeroMergeProbeService(
            _settings, _wardens, _camps, _enlistment, _visuals, _logger);
        probe.Heroes = new List<string> { "warden_promoted", "companion_deposited" };
        probe.HeroPrisoners = new List<string> { "captive_lord" };
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData { PartyId = "r1", Established = true, WardenHeroId = "warden_promoted" },
        };
        probe.LoadFrom(book, 0);

        probe.Dismantle(book["r1"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "hero:warden_promoted",
                "hero:companion_deposited",
                "prisoner:captive_lord",
                "bulk:r1",
                "destroy:r1",
            },
            probe.MergeEvents,
            "a hero row surviving into the bulk copy+clear gets PartyBelongedTo nulled by the "
            + "engine's roster callbacks and the desync persists into the save");
    }

    [TestMethod]
    public void Dismantle_NoHeroesInRefuge_StillBulkMerges()
    {
        var probe = new HeroMergeProbeService(
            _settings, _wardens, _camps, _enlistment, _visuals, _logger);
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData { PartyId = "r1", Established = true },
        };
        probe.LoadFrom(book, 0);

        probe.Dismantle(book["r1"]);

        CollectionAssert.AreEqual(new[] { "bulk:r1", "destroy:r1" }, probe.MergeEvents);
    }
}
