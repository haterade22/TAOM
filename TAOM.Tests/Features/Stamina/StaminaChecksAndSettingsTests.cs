using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaChecksAndSettingsTests
{
    private IOutcomeLog _log = null!;

    [TestInitialize]
    public void Setup() => _log = Substitute.For<IOutcomeLog>();

    [TestMethod]
    public void Armed_GateOpen_Passes()
    {
        StaminaChecks.Armed(_log, "m1", enabled: true, armed: true);

        _log.Received(1).Pass(StaminaChecks.ArmedId, "m1", "armed=true");
    }

    [TestMethod]
    public void Armed_SwitchOff_SkipsAndSaysSo()
    {
        StaminaChecks.Armed(_log, "m1", enabled: false, armed: false);

        _log.Received(1).Skip(StaminaChecks.ArmedId, "m1", "reason=toggle-off");
        _log.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void Armed_SwitchOnButNotABattle_SkipsAsGateClosed()
    {
        StaminaChecks.Armed(_log, "m2", enabled: true, armed: false);

        _log.Received(1).Skip(StaminaChecks.ArmedId, "m2", "reason=gate-closed");
    }

    [TestMethod]
    public void Lock_SoldiersRecoveredAndNoneStuck_Passes()
    {
        StaminaChecks.Lock(_log, "m1", exhaustions: 12, recoveries: 9, stuck: 0);

        _log.Received(1).Pass(StaminaChecks.LockId, "m1", "entered=12 recovered=9 stuck=0");
    }

    [TestMethod]
    public void Lock_ASoldierStayedLocked_Fails_EvenWhenOthersRecovered()
    {
        StaminaChecks.Lock(_log, "m1", exhaustions: 12, recoveries: 9, stuck: 1);

        _log.Received(1).Fail(StaminaChecks.LockId, "m1", "entered=12 recovered=9 stuck=1");
        _log.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void Lock_NobodyWasExhausted_Skips()
    {
        StaminaChecks.Lock(_log, "m1", exhaustions: 0, recoveries: 0, stuck: 0);

        _log.Received(1).Skip(StaminaChecks.LockId, "m1", "reason=none-exhausted entered=0 recovered=0 stuck=0");
    }

    [TestMethod]
    public void Lock_ExhaustedButNoneLivedToRecover_Skips_BecauseTheLockIsNotProven()
    {
        StaminaChecks.Lock(_log, "m1", exhaustions: 3, recoveries: 0, stuck: 0);

        _log.Received(1).Skip(StaminaChecks.LockId, "m1", "reason=none-recovered entered=3 recovered=0 stuck=0");
    }

    [TestMethod]
    public void Refresh_BatchesWithinTheBudget_Passes()
    {
        StaminaChecks.Refresh(_log, "m1", refreshes: 400, slowestBatchMs: 0.31);

        _log.Received(1).Pass(StaminaChecks.RefreshId, "m1", "refreshes=400 slowest_batch_ms=0.310");
    }

    [TestMethod]
    public void Refresh_ASlowBatch_Fails()
    {
        StaminaChecks.Refresh(_log, "m1", refreshes: 400, slowestBatchMs: 1.5);

        _log.Received(1).Fail(StaminaChecks.RefreshId, "m1", "refreshes=400 slowest_batch_ms=1.500");
    }

    [TestMethod]
    public void Refresh_TimeNotANumber_Fails()
    {
        StaminaChecks.Refresh(_log, "m1", refreshes: 5, slowestBatchMs: double.NaN);

        _log.ReceivedWithAnyArgs(1).Fail(default!, default!, default!);
        _log.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void Refresh_NoBandEverChanged_Skips()
    {
        StaminaChecks.Refresh(_log, "m1", refreshes: 0, slowestBatchMs: 0);

        _log.Received(1).Skip(StaminaChecks.RefreshId, "m1", "reason=no-refresh refreshes=0 slowest_batch_ms=0.000");
    }

    [TestMethod]
    public void Settings_NoMcm_MatchTheCompiledDefaults()
    {
        var sut = new StaminaSettingsProvider();
        var defaults = new TaomSettings();

        Assert.AreEqual(defaults.EnableStamina, sut.Enabled);
        Assert.AreEqual(defaults.StaminaCostScale, sut.CostScale, 0.0001f);
        Assert.AreEqual(1f, sut.CostScale);
        Assert.AreEqual(defaults.StaminaApplyToPlayer, sut.ApplyToPlayer);
        Assert.AreEqual(defaults.StaminaApplyToAi, sut.ApplyToAi);
        Assert.IsTrue(sut.ApplyToPlayer);
        Assert.IsTrue(sut.ApplyToAi);
    }

    [TestMethod]
    public void Report_NamesTheActionsByKind()
    {
        // Spike S4 reads these: a jump or a bash that the poll saw on both action channels in different frames shows
        // as twice the jumps or bashes the tester made.
        var sut = new StaminaTelemetry();
        sut.CountAction(StaminaAction.Swing);
        sut.CountAction(StaminaAction.Swing);
        sut.CountAction(StaminaAction.Jump);
        sut.CountAction(StaminaAction.Bash);

        StringAssert.Contains(sut.Report(), "actions=4 (swing 2, bow 0, crossbow 0, throw 0, jump 1, kick 0, bash 1) ");

        sut.Reset();
        StringAssert.Contains(sut.Report(), "actions=0 (swing 0, bow 0, crossbow 0, throw 0, jump 0, kick 0, bash 0) ");
    }

    [TestMethod]
    public void PeoplesReport_CountsFightersAndExhaustionsPerPeople_InNameOrder()
    {
        // The peoples table of stamina.json is tuned from this line: without it the logs say how many soldiers were
        // exhausted, and not whether they were the orcs with the small pool or the dwarves with the large one.
        var sut = new StaminaTelemetry();
        sut.CountFighter("orc");
        sut.CountFighter("orc");
        sut.CountFighter("elf");
        sut.CountExhausted(first: true, "orc");
        sut.CountExhausted(first: false, "orc");

        Assert.AreEqual("peoples: elf fighters=1 exhaustedSoldiers=0 enteredExh=0; orc fighters=2 exhaustedSoldiers=1 enteredExh=2", sut.PeoplesReport());
        Assert.AreEqual(3, sut.Fighters, "the totals still count everybody");
        Assert.AreEqual(2, sut.Exhaustions);
        Assert.AreEqual(1, sut.ExhaustedSoldiers);

        sut.Reset();
        Assert.AreEqual("peoples: none", sut.PeoplesReport());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void PeoplesReport_ASoldierWithNoRaceName_IsCountedAsUnknown(string? people)
    {
        var sut = new StaminaTelemetry();
        sut.CountFighter(people);

        Assert.AreEqual("peoples: unknown fighters=1 exhaustedSoldiers=0 enteredExh=0", sut.PeoplesReport());
    }

    [TestMethod]
    public void OptOuts_AreIndependent_AndReadLive()
    {
        var mcm = new TaomSettings();
        var sut = new StaminaSettingsProvider(mcm);

        mcm.StaminaApplyToPlayer = false;
        Assert.IsFalse(sut.ApplyToPlayer);
        Assert.IsTrue(sut.ApplyToAi);

        mcm.StaminaApplyToPlayer = true;
        mcm.StaminaApplyToAi = false;
        Assert.IsTrue(sut.ApplyToPlayer);
        Assert.IsFalse(sut.ApplyToAi);
    }

    [DataTestMethod]
    [DataRow(true, true, true, true, DisplayName = "the player's soldier, both on")]
    [DataRow(true, false, true, false, DisplayName = "the player opted out: his soldier has no stamina")]
    [DataRow(true, true, false, true, DisplayName = "the AI switch does not reach the player's soldier")]
    [DataRow(false, true, true, true, DisplayName = "an AI soldier, both on")]
    [DataRow(false, true, false, false, DisplayName = "the AI opted out")]
    [DataRow(false, false, true, true, DisplayName = "the player switch does not reach an AI soldier")]
    [DataRow(true, false, false, false)]
    [DataRow(false, false, false, false)]
    public void Applies_EachSwitchReachesItsOwnSoldiersOnly(bool playerSoldier, bool applyToPlayer, bool applyToAi, bool expected)
        => Assert.AreEqual(expected, StaminaService.Applies(playerSoldier, applyToPlayer, applyToAi));

    [DataTestMethod]
    [DataRow(0.5f, 0.5f)]
    [DataRow(1.25f, 1.25f)]
    [DataRow(2f, 2f)]
    [DataRow(0.1f, 1f, DisplayName = "below the slider: a hand-edited file")]
    [DataRow(9f, 1f)]
    [DataRow(0f, 1f, DisplayName = "0 would make every action free")]
    [DataRow(-1f, 1f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    public void CostScale_OutsideTheSliderOrNotANumber_ReadsAsOne(float stored, float expected)
    {
        var sut = new StaminaSettingsProvider(new TaomSettings { StaminaCostScale = stored });

        Assert.AreEqual(expected, sut.CostScale);
    }

    [TestMethod]
    public void Settings_AreReadLiveThroughTheCachedObject()
    {
        var mcm = new TaomSettings { StaminaCostScale = 1.5f };
        var sut = new StaminaSettingsProvider(mcm);
        Assert.AreEqual(1.5f, sut.CostScale);

        mcm.StaminaCostScale = 0.75f;

        Assert.AreEqual(0.75f, sut.CostScale);
    }
}
