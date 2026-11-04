using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Autorun;

namespace TAOM.Tests.Features.FordsOfWar.Autorun;

/// <summary>The unattended drive of the control check: three takes, a hand-back, a hold until the soldier falls, and a take after that.</summary>
[TestClass]
public class FordsControlCheckTests
{
    private const float Start = 100f;

    private static float FirstHold => FordsControlCheck.Holds[0];

    private static float SecondHold => FordsControlCheck.Holds[1];

    private static float ThirdHold => FordsControlCheck.Holds[2];

    private static float Rest => FordsControlCheck.RestSeconds;

    [TestMethod]
    public void Next_BeforeTheStart_DoesNothing()
    {
        Assert.AreEqual(FordsControlStep.None, new FordsControlCheck(Start).Next(Start - 1f, false));
    }

    [TestMethod]
    public void Next_AtTheStart_AsksForATake_AndAsksAgainUntilControlHolds()
    {
        var check = new FordsControlCheck(Start);

        Assert.AreEqual(FordsControlStep.TakeFront, check.Next(Start, false));
        Assert.AreEqual(FordsControlStep.TakeFront, check.Next(Start + 1f, false));
        Assert.AreEqual(FordsControlStep.None, check.Next(Start + 2f, true));
    }

    [TestMethod]
    public void Next_HoldsForItsTime_ThenAsksForAReturn()
    {
        var check = new FordsControlCheck(Start);
        check.Next(Start, false);
        check.Next(Start, true);

        Assert.AreEqual(FordsControlStep.None, check.Next(Start + FirstHold - 1f, true));
        Assert.AreEqual(FordsControlStep.Return, check.Next(Start + FirstHold, true));
    }

    [TestMethod]
    public void Next_AfterAReturn_RestsBeforeTheNextTake()
    {
        var check = new FordsControlCheck(Start);
        var returned = Start + FirstHold;
        check.Next(Start, false);
        check.Next(Start, true);
        check.Next(returned, true);

        Assert.AreEqual(FordsControlStep.None, check.Next(returned, false));
        Assert.AreEqual(FordsControlStep.None, check.Next(returned + Rest - 1f, false));
        Assert.AreEqual(FordsControlStep.TakeFront, check.Next(returned + Rest, false));
    }

    [TestMethod]
    public void Next_ASoldierThatFallsDuringAHold_EndsThatRound_AndNoReturnIsAskedForIt()
    {
        var check = new FordsControlCheck(Start);
        check.Next(Start, false);
        check.Next(Start, true);

        Assert.AreEqual(FordsControlStep.None, check.Next(Start + 1f, false), "the soldier fell: the round ends");
        Assert.AreEqual(FordsControlStep.TakeFront, check.Next(Start + 1f + Rest, false), "the next round takes after its rest");
    }

    [TestMethod]
    public void Next_AfterThreeRounds_IsDone_AndAsksForNothingMore()
    {
        var check = new FordsControlCheck(Start);
        var now = Start;
        foreach (var hold in new[] { FirstHold, SecondHold, ThirdHold })
        {
            Assert.IsFalse(check.Done);
            Assert.AreEqual(FordsControlStep.TakeFront, check.Next(now, false));
            check.Next(now, true);
            now += hold;
            Assert.AreEqual(FordsControlStep.Return, check.Next(now, true));
            check.Next(now, false);
            now += Rest;
        }

        Assert.IsTrue(check.Done);
        Assert.AreEqual(FordsControlStep.None, check.Next(now + 1000f, false));
    }

    [TestMethod]
    public void Holds_TheMiddleRound_LeavesTimeForASoldierAtTheFrontToFall()
    {
        Assert.IsTrue(SecondHold >= 2f * FirstHold, "the second round waits for a death, so it must outlast a plain hold");
    }
}
