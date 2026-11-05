using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

[TestClass]
public class MarshalVolleyJudgeTests
{
    private const double NoWord = double.NegativeInfinity;

    private readonly MarshalVolleyJudge _judge = new();

    [TestMethod]
    public void JudgeHold_FiveHoldingThroughTheWindowWithNoShots_Passes()
    {
        Assert.IsNull(_judge.JudgeHold(0.0, 6, 0, NoWord));
        Assert.IsNull(_judge.JudgeHold(2.0, 6, 0, NoWord));

        var verdict = _judge.JudgeHold(MarshalTuning.HoldWindowSeconds, 6, 0, NoWord);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Hold, true, 6, 0), verdict);
    }

    [TestMethod]
    public void JudgeHold_MoreShotsThanFivePercentOfTheHolding_Fails()
    {
        _judge.JudgeHold(0.0, 20, 0, NoWord);
        _judge.JudgeHold(2.0, 20, 1, NoWord);

        var verdict = _judge.JudgeHold(5.0, 20, 2, NoWord);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Hold, false, 20, 2), verdict);
    }

    [TestMethod]
    public void JudgeHold_ShotsAtExactlyFivePercent_Passes()
    {
        _judge.JudgeHold(0.0, 20, 0, NoWord);

        var verdict = _judge.JudgeHold(5.0, 20, 1, NoWord);

        Assert.IsTrue(verdict!.Value.Pass);
    }

    [TestMethod]
    public void JudgeHold_ShotsBeforeTheWindowBegan_AreNotCounted()
    {
        _judge.JudgeHold(0.0, 6, 40, NoWord);

        var verdict = _judge.JudgeHold(5.0, 6, 40, NoWord);

        Assert.AreEqual(0, verdict!.Value.Shots);
        Assert.IsTrue(verdict.Value.Pass);
    }

    [TestMethod]
    public void JudgeHold_TheHoldingCountDipsUnderFive_RestartsTheWindow()
    {
        _judge.JudgeHold(0.0, 6, 0, NoWord);
        _judge.JudgeHold(3.0, 4, 0, NoWord);

        Assert.IsNull(_judge.JudgeHold(5.0, 6, 0, NoWord), "the window began again at 5 s");
        Assert.IsNotNull(_judge.JudgeHold(10.0, 6, 0, NoWord));
    }

    [TestMethod]
    public void JudgeHold_AWordInsideTheWindow_RestartsIt()
    {
        _judge.JudgeHold(0.0, 6, 0, NoWord);

        Assert.IsNull(_judge.JudgeHold(5.0, 6, 3, lastWord: 2.0));
    }

    [TestMethod]
    public void JudgeHold_TheSmallestHoldingCountOfTheWindowSetsTheShare()
    {
        _judge.JudgeHold(0.0, 40, 0, NoWord);
        _judge.JudgeHold(2.0, 5, 0, NoWord);

        var verdict = _judge.JudgeHold(5.0, 40, 1, NoWord);

        Assert.AreEqual(5, verdict!.Value.Holding);
        Assert.IsFalse(verdict.Value.Pass, "1 shot in 5 holding is over a twentieth");
    }

    [TestMethod]
    public void JudgeHold_AfterAVerdict_ANewWindowBegins()
    {
        _judge.JudgeHold(0.0, 6, 0, NoWord);
        Assert.IsNotNull(_judge.JudgeHold(5.0, 6, 0, NoWord));

        Assert.IsNull(_judge.JudgeHold(6.0, 6, 0, NoWord));
        Assert.IsNotNull(_judge.JudgeHold(11.0, 6, 0, NoWord));
    }

    [TestMethod]
    public void JudgeRelease_SeventyPercentLooseInsideTheWindow_Passes()
    {
        _judge.NoteWord(now: 10.0, holding: 10, shotsAfterWord: 4);

        var verdict = _judge.JudgeRelease(10.0 + MarshalTuning.WordJitter + MarshalTuning.ReleaseWindowPadSeconds, shotsAfterWord: 11);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, true, 10, 7), verdict);
    }

    [TestMethod]
    public void JudgeRelease_UnderSeventyPercent_Fails()
    {
        _judge.NoteWord(10.0, 10, 0);

        var verdict = _judge.JudgeRelease(11.3, 6);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, false, 10, 6), verdict);
    }

    [TestMethod]
    public void JudgeRelease_BeforeTheWindowEnds_JudgesNothing()
    {
        _judge.NoteWord(10.0, 10, 0);

        Assert.IsNull(_judge.JudgeRelease(11.2, 10));
    }

    [TestMethod]
    public void JudgeRelease_AWordWithFewerThanFiveHolding_IsNeverJudged()
    {
        _judge.NoteWord(10.0, 4, 0);

        Assert.IsNull(_judge.JudgeRelease(20.0, 4));
    }

    [TestMethod]
    public void NoteWord_ASecondWordWithFewerThanFiveHolding_DoesNotEraseTheOpenWindow()
    {
        _judge.NoteWord(10.0, 10, 0);
        _judge.NoteWord(10.5, 0, 0);

        var verdict = _judge.JudgeRelease(12.0, 0);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, false, 10, 0), verdict);
    }

    [TestMethod]
    public void NoteWord_ASecondWordBeforeTheFirstIsJudged_KeepsTheFirstWordsBaselineAndHolders()
    {
        _judge.NoteWord(10.0, 10, 0);
        _judge.NoteWord(10.5, 12, 3);

        var verdict = _judge.JudgeRelease(11.4, 7);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, true, 10, 7), verdict);
    }

    [TestMethod]
    public void NoteWord_AfterTheFirstWindowWasJudged_OpensANewOne()
    {
        _judge.NoteWord(10.0, 10, 0);
        Assert.IsNotNull(_judge.JudgeRelease(11.3, 8));

        _judge.NoteWord(20.0, 6, 8);
        var verdict = _judge.JudgeRelease(21.3, 13);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, true, 6, 5), verdict);
    }

    [TestMethod]
    public void JudgeRelease_OnlyOnceForOneWord()
    {
        _judge.NoteWord(10.0, 10, 0);
        Assert.IsNotNull(_judge.JudgeRelease(11.3, 10));

        Assert.IsNull(_judge.JudgeRelease(12.0, 10));
    }

    [TestMethod]
    public void JudgeRelease_TheShotsAreCountedFromTheWord_NotFromTheStartOfTheBattle()
    {
        _judge.NoteWord(10.0, 10, shotsAfterWord: 500);

        var verdict = _judge.JudgeRelease(11.3, 500);

        Assert.AreEqual(0, verdict!.Value.Shots);
        Assert.IsFalse(verdict.Value.Pass);
    }

    [TestMethod]
    public void JudgeHold_AWordAtTheSecondTheWindowBegan_RestartsIt()
    {
        _judge.JudgeHold(0.0, 6, 0, NoWord);

        Assert.IsNull(_judge.JudgeHold(5.0, 6, 0, lastWord: 0.0), "a word given as the window began is a word inside it");
    }

    [TestMethod]
    public void JudgeRelease_AWordWithExactlyFiveHolding_IsJudged()
    {
        _judge.NoteWord(10.0, MarshalTuning.VolleyMinHolding, 0);

        var verdict = _judge.JudgeRelease(11.3, 4);

        Assert.AreEqual(new VolleyVerdict(VolleyCheck.Release, true, 5, 4), verdict);
    }
}
