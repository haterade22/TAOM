using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The vanilla tactic lifecycle (<c>TacticDefensiveEngagement.TickOccasionally</c>) as a pure
/// step: re-apply when the formation set changed, when the battle-joined flag flipped, or when
/// the engine asked for a re-apply; recount formations only when the set changed; otherwise do
/// nothing. <c>TaomTacticBase</c> calls this once per <c>TickOccasionally</c>.
/// </summary>
[TestClass]
public class TacticPhaseMachineTests
{
    [TestMethod]
    public void Step_FirstTickWithChangedFormations_AppliesDefendAndRecounts()
    {
        var sut = new TacticPhaseMachine();

        var phase = sut.Step(formationsChanged: true, battleJoined: false, reapplyNeeded: false, alwaysEngaged: false, out var recount);

        Assert.AreEqual(TacticPhase.Defend, phase);
        Assert.IsTrue(recount);
    }

    [TestMethod]
    public void Step_NothingChanged_DoesNothing()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);

        var phase = sut.Step(false, false, false, false, out var recount);

        Assert.IsNull(phase);
        Assert.IsFalse(recount);
    }

    [TestMethod]
    public void Step_BattleJoins_AppliesEngageWithoutRecount()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);

        var phase = sut.Step(false, true, false, false, out var recount);

        Assert.AreEqual(TacticPhase.Engage, phase);
        Assert.IsFalse(recount);
    }

    [TestMethod]
    public void Step_BattleJoinedStaysTrue_DoesNotReapplyEveryTick()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);
        sut.Step(false, true, false, false, out _);

        Assert.IsNull(sut.Step(false, true, false, false, out _));
    }

    [TestMethod]
    public void Step_BattleDisengages_GoesBackToDefend()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);
        sut.Step(false, true, false, false, out _);

        Assert.AreEqual(TacticPhase.Defend, sut.Step(false, false, false, false, out _));
    }

    [TestMethod]
    public void Step_ReapplyRequested_ReappliesTheCurrentPhase()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);
        sut.Step(false, true, false, false, out _);

        var phase = sut.Step(false, true, reapplyNeeded: true, false, out var recount);

        Assert.AreEqual(TacticPhase.Engage, phase);
        Assert.IsFalse(recount);
    }

    [TestMethod]
    public void Step_AlwaysEngaged_NeverDefends()
    {
        var sut = new TacticPhaseMachine();

        Assert.AreEqual(TacticPhase.Engage, sut.Step(true, false, false, alwaysEngaged: true, out _));
        Assert.IsNull(sut.Step(false, false, false, alwaysEngaged: true, out _));
        Assert.AreEqual(TacticPhase.Engage, sut.Step(true, false, false, alwaysEngaged: true, out _));
    }

    [TestMethod]
    public void Step_FormationsChangeMidBattle_ReappliesEngageAndRecounts()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, false, false, false, out _);
        sut.Step(false, true, false, false, out _);

        var phase = sut.Step(formationsChanged: true, battleJoined: true, false, false, out var recount);

        Assert.AreEqual(TacticPhase.Engage, phase);
        Assert.IsTrue(recount);
    }

    [TestMethod]
    public void Current_TracksTheLastAppliedPhase()
    {
        var sut = new TacticPhaseMachine();
        Assert.IsNull(sut.Current, "nothing applied yet");

        sut.Step(true, false, false, false, out _);
        Assert.AreEqual(TacticPhase.Defend, sut.Current);

        sut.Step(false, false, false, false, out _);
        Assert.AreEqual(TacticPhase.Defend, sut.Current, "a no-op tick keeps the phase");

        sut.Step(false, true, false, false, out _);
        Assert.AreEqual(TacticPhase.Engage, sut.Current);

        sut.Reset();
        Assert.IsNull(sut.Current);
    }

    [TestMethod]
    public void Reset_ForgetsTheJoinedFlag()
    {
        var sut = new TacticPhaseMachine();
        sut.Step(true, true, false, false, out _);
        sut.Reset();

        Assert.AreEqual(TacticPhase.Engage, sut.Step(false, true, false, false, out _), "after a reset a joined battle is news again");
    }
}
