using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;

using TAOM.Features.Enlistment.Presentation;
namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The live status board. The value equality IS the refresh throttle, so a field missing from
/// Equals is a field whose changes never reach the screen — that is what the per-field DataRows
/// below exist to catch.
/// </summary>
[TestClass]
public class ServiceStatusServiceTests
{
    private EnlistmentStore _store;
    private EnlistmentContentStore _content;
    private ICommanderLordAdapter _commander;
    private IServiceStatusTextWriter _writer;
    private IEnlistmentContentConfigProvider _config;
    private IHeroSkillXpAdapter _skillXp;
    private PromotionService _promotion;
    private ServiceStatusService _sut;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _content = new EnlistmentContentStore(logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _writer = Substitute.For<IServiceStatusTextWriter>();
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();

        // A REAL PromotionService on purpose. Substituting it here would stub out the exact seam
        // this batch adds — the board reading the same ladder that promotes the player — and leave
        // the numbers on screen free to drift from the numbers that grant the rank.
        _promotion = new PromotionService(_content, _config, _skillXp, _store, logger);

        Commander();
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";

        _sut = new ServiceStatusService(_store, _content, _commander, _writer, _promotion, _config);
    }

    private void Commander(
        bool inMapEvent = false, bool besieging = false,
        string settlementId = null, string settlementName = null, bool alive = true)
    {
        _commander.GetSnapshot("lord_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: alive, partyId: "lord_party", partyIsActive: true,
            partyIsInMapEvent: inMapEvent, partyIsBesieging: besieging,
            partyIsInSettlement: !string.IsNullOrEmpty(settlementId),
            settlementId: settlementId, settlementName: settlementName,
            name: "Boromir"));
    }

    // ---- Build ------------------------------------------------------------------------------

    [TestMethod]
    public void Build_NotEnlisted_Null()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;
        Assert.IsNull(_sut.Build());
    }

    [TestMethod]
    public void Build_Marching_CarriesCommanderAndProgress()
    {
        _content.Record.Rank = ServiceRank.Veteran;
        _content.Record.DaysServed = 12;
        _content.Record.Trust = 9;

        var status = _sut.Build();

        Assert.AreEqual(CommanderActivity.Marching, status.Activity);
        Assert.AreEqual("Boromir", status.CommanderName);
        Assert.AreEqual(ServiceRank.Veteran, status.Rank);
        Assert.AreEqual(12, status.DaysServed);
        Assert.AreEqual(9, status.Trust);
    }

    [TestMethod]
    public void Build_InSettlement_CarriesTheSettlementName()
    {
        // The proof line for this batch: the board must be able to say WHERE, by name.
        Commander(settlementId: "town_ES1", settlementName: "Minas Tirith");

        var status = _sut.Build();

        Assert.AreEqual(CommanderActivity.InSettlement, status.Activity);
        Assert.AreEqual("Minas Tirith", status.SettlementName);
    }

    [TestMethod]
    public void Build_BattleOutranksSiegeAndSettlement()
    {
        // A commander assaulting a town is in all three states at once; "in battle" is the one
        // the player needs.
        Commander(inMapEvent: true, besieging: true, settlementId: "town_ES1", settlementName: "Minas Tirith");
        Assert.AreEqual(CommanderActivity.InBattle, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_SiegeOutranksSettlement()
    {
        Commander(besieging: true, settlementId: "town_ES1", settlementName: "Minas Tirith");
        Assert.AreEqual(CommanderActivity.Besieging, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_DeadCommander_Unavailable()
    {
        Commander(alive: false);
        Assert.AreEqual(CommanderActivity.Unavailable, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_NullSnapshot_DoesNotThrow()
    {
        _commander.GetSnapshot("lord_1").Returns((CommanderSnapshot)null);
        Assert.AreEqual(CommanderActivity.Unavailable, _sut.Build().Activity);
    }

    // ---- the three things the board knew and hid ---------------------------------------------

    [TestMethod]
    public void Build_Always_CarriesServiceXpAndTodaysWage()
    {
        // Service XP is the promotion ladder's main currency and had no player-visible surface at
        // all; the wage is read from the same rank table ServiceRewardService pays from.
        _content.Record.ServiceXp = 42;

        var status = _sut.Build();

        Assert.AreEqual(42, status.ServiceXp);
        Assert.AreEqual(5, status.DailyWage, "recruit daily wage, default table [5,8,14,22]");
    }

    [TestMethod]
    public void Build_HigherRank_WageFollowsTheRankTable()
    {
        _content.Record.Rank = ServiceRank.Veteran;

        Assert.AreEqual(14, _sut.Build().DailyWage);
    }

    [TestMethod]
    public void Build_FreshRecruit_NamesTheNextRankAndItsBindingRequirement()
    {
        // Nothing served yet: days (0 of 7) and XP (0 of 100) are both a 100% shortfall, so the
        // documented tie-break — evaluator declaration order — has to pick days.
        var status = _sut.Build();

        Assert.AreEqual(ServiceRank.Soldier, status.NextRank);
        Assert.AreEqual("days", status.NextRequirementKey);
        Assert.AreEqual(7, status.NextRequirementTarget);
    }

    [TestMethod]
    public void Build_SeveralGapsOpen_PicksTheLargestRelativeShortfall()
    {
        // Soldier -> Veteran needs 25 days / 350 XP / Leadership 20 / 2 duties / trust 0.
        // A raw difference would name XP for the wrong reason; the point is that 250-of-350 XP
        // (71%) really does outrank 1-of-25 days (4%) and 2-of-20 Leadership (10%).
        _content.Record.Rank = ServiceRank.Soldier;
        _content.Record.DaysServed = 24;
        _content.Record.ServiceXp = 100;
        _content.Record.DutySuccesses = 2;
        _content.Record.Trust = 0;
        _skillXp.GetSkillValue("main_hero", "Leadership").Returns(18);

        var status = _sut.Build();

        Assert.AreEqual(ServiceRank.Veteran, status.NextRank);
        Assert.AreEqual("xp", status.NextRequirementKey);
        Assert.AreEqual(350, status.NextRequirementTarget);
    }

    [TestMethod]
    public void Build_AtTopRank_NoLadderToShow()
    {
        _content.Record.Rank = ServiceRank.Sergeant;

        var status = _sut.Build();

        Assert.IsNull(status.NextRank);
        Assert.IsNull(status.NextRequirementKey);
        Assert.AreEqual(0, status.NextRequirementTarget);
    }

    [TestMethod]
    public void Build_EveryRequirementMet_NoLadderToShow()
    {
        // The promotion lands on the next daily tick. A "you still need..." line here would be a
        // lie, so the board says nothing rather than naming an already-cleared gate.
        _content.Record.DaysServed = 8;
        _content.Record.ServiceXp = 120;

        var status = _sut.Build();

        Assert.IsNull(status.NextRank);
        Assert.IsNull(status.NextRequirementKey);
    }

    [TestMethod]
    public void Build_PromotionIsDue_DoesNotPromote()
    {
        // Build() runs on the render pump. If it could promote, rank would advance from a draw
        // call — Peek() exists precisely so a status read can never mutate the ladder.
        _content.Record.DaysServed = 8;
        _content.Record.ServiceXp = 120;

        _sut.Build();

        Assert.AreEqual(ServiceRank.Recruit, _content.Record.Rank);
    }

    // ---- the throttle -----------------------------------------------------------------------

    [TestMethod]
    public void RefreshIfChanged_UnmetRequirementsUnchanged_DoesNotWriteAgain()
    {
        // THE regression test for the shape of the new fields. PromotionEvaluation exposes its
        // gaps as a List<string> and builds a fresh one per evaluation; carrying that list into
        // the model would make every rebuild differ by reference and re-push the menu text on
        // every pass, defeating the throttle silently. The model stores a single string instead.
        _content.Record.Rank = ServiceRank.Soldier;
        _content.Record.DaysServed = 24;
        _content.Record.ServiceXp = 100;

        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();

        Assert.IsFalse(_sut.RefreshIfChanged());
        _writer.DidNotReceiveWithAnyArgs().Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_ServiceXpMoved_WritesAgain()
    {
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();
        _content.Record.ServiceXp += 10;

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_BindingRequirementMoved_WritesAgain()
    {
        // Days is the binding gate for a fresh recruit; clearing it hands the board over to XP.
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();
        _content.Record.DaysServed = 7;

        Assert.IsTrue(_sut.RefreshIfChanged());
        Assert.AreEqual("xp", _sut.Build().NextRequirementKey);
    }


    [TestMethod]
    public void RefreshIfChanged_FirstCall_Writes()
    {
        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_NothingChanged_DoesNotWriteAgain()
    {
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();

        Assert.IsFalse(_sut.RefreshIfChanged());
        _writer.DidNotReceiveWithAnyArgs().Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_ColumnEntersASettlement_WritesAgain()
    {
        // The felt change: the text must move when the column stops somewhere.
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();
        Commander(settlementId: "town_ES1", settlementName: "Minas Tirith");

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_NotEnlisted_DoesNothing()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        Assert.IsFalse(_sut.RefreshIfChanged());
        _writer.DidNotReceiveWithAnyArgs().Write(default);
    }

    [TestMethod]
    public void Invalidate_ForcesTheNextRefreshToWrite()
    {
        // Menu init relies on this: the cached model is almost always identical to what is on
        // screen, so without invalidation the init refresh would no-op and leave the previous
        // menu's text in place.
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();

        _sut.Invalidate();

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }
}

/// <summary>
/// One DataRow per field. The model's Equals is the refresh gate, so an omitted field is a
/// permanently invisible status line — a silent failure with no error anywhere.
/// </summary>
[TestClass]
public class ServiceStatusModelEqualityTests
{
    private static ServiceStatusModel Base() => new ServiceStatusModel(
        commanderName: "Boromir", activity: CommanderActivity.Marching, settlementName: null,
        rank: ServiceRank.Soldier, assignment: ServiceAssignment.Infantry,
        daysServed: 5, trust: 3, deferredWages: 0, activeDutyId: null,
        serviceXp: 120, dailyWage: 8, nextRank: ServiceRank.Veteran,
        nextRequirementKey: "xp", nextRequirementTarget: 350);

    [TestMethod]
    public void Equals_IdenticalModels_True() => Assert.IsTrue(Base().Equals(Base()));

    [TestMethod]
    public void Equals_Null_False() => Assert.IsFalse(Base().Equals(null));

    [DataTestMethod]
    [DataRow("commanderName")]
    [DataRow("activity")]
    [DataRow("settlementName")]
    [DataRow("rank")]
    [DataRow("assignment")]
    [DataRow("daysServed")]
    [DataRow("trust")]
    [DataRow("deferredWages")]
    [DataRow("activeDutyId")]
    [DataRow("serviceXp")]
    [DataRow("dailyWage")]
    [DataRow("nextRank")]
    [DataRow("nextRequirementKey")]
    [DataRow("nextRequirementTarget")]
    public void Equals_IsFalse_WhenAnySingleFieldDiffers(string field)
    {
        var other = new ServiceStatusModel(
            commanderName: field == "commanderName" ? "Faramir" : "Boromir",
            activity: field == "activity" ? CommanderActivity.InBattle : CommanderActivity.Marching,
            settlementName: field == "settlementName" ? "Minas Tirith" : null,
            rank: field == "rank" ? ServiceRank.Veteran : ServiceRank.Soldier,
            assignment: field == "assignment" ? ServiceAssignment.Cavalry : ServiceAssignment.Infantry,
            daysServed: field == "daysServed" ? 6 : 5,
            trust: field == "trust" ? 4 : 3,
            deferredWages: field == "deferredWages" ? 50 : 0,
            activeDutyId: field == "activeDutyId" ? "recon_sweep" : null,
            serviceXp: field == "serviceXp" ? 121 : 120,
            dailyWage: field == "dailyWage" ? 14 : 8,
            nextRank: field == "nextRank" ? ServiceRank.Sergeant : ServiceRank.Veteran,
            nextRequirementKey: field == "nextRequirementKey" ? "leadership" : "xp",
            nextRequirementTarget: field == "nextRequirementTarget" ? 800 : 350);

        Assert.IsFalse(Base().Equals(other), $"'{field}' is missing from Equals — its changes would never reach the screen");
    }

    /// <summary>
    /// GetHashCode has to move with Equals or the two disagree the moment anything buckets these.
    /// Not a uniqueness claim — just that each new field reaches the hash at all.
    /// </summary>
    [DataTestMethod]
    [DataRow("serviceXp")]
    [DataRow("dailyWage")]
    [DataRow("nextRank")]
    [DataRow("nextRequirementKey")]
    [DataRow("nextRequirementTarget")]
    public void GetHashCode_Differs_WhenANewFieldDiffers(string field)
    {
        var other = new ServiceStatusModel(
            commanderName: "Boromir", activity: CommanderActivity.Marching, settlementName: null,
            rank: ServiceRank.Soldier, assignment: ServiceAssignment.Infantry,
            daysServed: 5, trust: 3, deferredWages: 0, activeDutyId: null,
            serviceXp: field == "serviceXp" ? 121 : 120,
            dailyWage: field == "dailyWage" ? 14 : 8,
            nextRank: field == "nextRank" ? ServiceRank.Sergeant : ServiceRank.Veteran,
            nextRequirementKey: field == "nextRequirementKey" ? "leadership" : "xp",
            nextRequirementTarget: field == "nextRequirementTarget" ? 800 : 350);

        Assert.AreNotEqual(Base().GetHashCode(), other.GetHashCode(), $"'{field}' is missing from GetHashCode");
    }
}
