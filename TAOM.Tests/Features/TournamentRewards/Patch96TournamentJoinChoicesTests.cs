using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TournamentRewards.Hooks;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The Join prefix decides whether vanilla's own Join consequence runs after <c>BeginJoin</c>. Vanilla must run at
/// most once: a fault before the dialogs falls back to vanilla, and a fault inside vanilla's own resumed run is
/// vanilla's, so it propagates and never runs vanilla a second time (#1086 follow-up).
/// </summary>
[TestClass]
public class Patch96TournamentJoinChoicesTests
{
    [TestMethod]
    public void AFaultBeforeTheDialogs_RunsVanilla_AndWarns()
    {
        Exception? warned = null;
        int resumed = 0;

        bool runVanilla = Patch96_TournamentJoinChoices.RunChoices(
            _ => throw new InvalidOperationException("no tournament"), () => resumed++, ex => warned = ex);

        Assert.IsTrue(runVanilla);
        Assert.AreEqual(0, resumed);
        Assert.IsInstanceOfType(warned, typeof(InvalidOperationException));
    }

    [TestMethod]
    public void AResumeOnTheSpot_SkipsTheOriginal()
    {
        int resumed = 0;

        bool runVanilla = Patch96_TournamentJoinChoices.RunChoices(proceed => proceed(), () => resumed++, _ => Assert.Fail("no warning"));

        Assert.IsFalse(runVanilla);
        Assert.AreEqual(1, resumed);
    }

    [TestMethod]
    public void ADeferredResume_SkipsTheOriginal()
    {
        int resumed = 0;

        bool runVanilla = Patch96_TournamentJoinChoices.RunChoices(_ => { }, () => resumed++, _ => Assert.Fail("no warning"));

        Assert.IsFalse(runVanilla);
        Assert.AreEqual(0, resumed);
    }

    [TestMethod]
    public void AFaultInsideTheResumedVanillaRun_Propagates_AndNeverRunsVanillaAgain()
    {
        int resumed = 0;
        var fault = new InvalidOperationException("vanilla threw");

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => Patch96_TournamentJoinChoices.RunChoices(
            proceed => proceed(), () => { resumed++; throw fault; }, _ => Assert.Fail("no fallback warning")));

        Assert.AreSame(fault, thrown);
        Assert.AreEqual(1, resumed);
    }
}
