using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// One sally wave's tally (phase2-plan.md Slice 10, spike S9): the seconds out, the hits by and on the lord and his horse, the time spent outside the mission boundary, the 5 s line while he is
/// more than 20 m outside the keep, and the contact verdict (a hit in the first 180 s within 40 m of the sally point).
/// </summary>
[TestClass]
public class SallyWatchTests
{
    private static readonly LdPlace Point = new(100f, 200f, 30f, 0f, 1f);
    private static readonly LdPoint Keep = new(0f, 0f, 30f);

    private static SallyWatch Watch() => new(3, Point, Point.Point, 2);

    private static LdLordFacts Lord(float x, float y, bool inside = true) =>
        new(true, true, "lord", true, new LdPoint(x, y, 30f), inside, "Cavalry", null, 4f, 9f, "closed");

    [TestMethod]
    public void AHitNearThePoint_InTheFirst180Seconds_IsTheFirstContact_ExactlyOnce()
    {
        var watch = Watch();
        watch.Advance(10f, Lord(100f, 200f), Keep);

        Assert.IsTrue(watch.Add(new LdContact(LdContactKind.Given, false)));
        Assert.IsFalse(watch.Add(new LdContact(LdContactKind.Taken, false)), "the verdict is written once");
        Assert.IsTrue(watch.ContactJudged);
        Assert.AreEqual(1, watch.Given);
        Assert.AreEqual(1, watch.Taken);
    }

    [TestMethod]
    public void AHitMoreThan40mFromThePoint_IsCounted_ButIsNotContact()
    {
        var watch = Watch();
        watch.Advance(10f, Lord(100f, 241f), Keep);

        Assert.IsFalse(watch.Add(new LdContact(LdContactKind.Given, false)));
        Assert.IsFalse(watch.ContactJudged);
        Assert.AreEqual(1, watch.Given, "the hit still shows in the counts");
    }

    [TestMethod]
    public void AHitAtExactly40m_IsContact()
    {
        var watch = Watch();
        watch.Advance(10f, Lord(140f, 200f), Keep);

        Assert.IsTrue(watch.Add(new LdContact(LdContactKind.MountTaken, false)));
        Assert.AreEqual(1, watch.MountTaken);
    }

    [TestMethod]
    public void AHitAfter180Seconds_IsCountedButTooLateToBeContact()
    {
        var watch = Watch();
        watch.Advance(180.5f, Lord(100f, 200f), Keep);

        Assert.IsFalse(watch.Add(new LdContact(LdContactKind.Given, false)));
        Assert.IsFalse(watch.ContactJudged);
        Assert.IsTrue(watch.Silent, "the window closed with no contact");
    }

    [TestMethod]
    public void TheCountsKeepEachKindApart_AndAKillIsTheLordsBlow()
    {
        var watch = Watch();
        watch.Advance(1f, Lord(100f, 200f), Keep);

        watch.Add(new LdContact(LdContactKind.Given, true));
        watch.Add(new LdContact(LdContactKind.Given, false));
        watch.Add(new LdContact(LdContactKind.Taken, false));
        watch.Add(new LdContact(LdContactKind.MountTaken, false));
        watch.Add(new LdContact(LdContactKind.MountTaken, false));

        Assert.AreEqual("hits_given=2 hits_taken=1 mount_hits=2 kills=1", watch.Counts);
    }

    [TestMethod]
    public void NotSilent_BeforeTheWindowCloses_OrOnceJudged()
    {
        var watch = Watch();
        watch.Advance(100f, Lord(100f, 200f), Keep);
        Assert.IsFalse(watch.Silent);

        watch.Add(new LdContact(LdContactKind.Given, false));
        watch.Advance(100f, Lord(100f, 200f), Keep);
        Assert.IsFalse(watch.Silent, "a judged watch is never silent");
    }

    [TestMethod]
    public void TheFiveSecondLine_IsDueEveryFiveSeconds_WhileMoreThan20mOutsideTheKeep()
    {
        var watch = Watch();

        Assert.IsFalse(watch.Advance(2f, Lord(100f, 200f), Keep));
        Assert.IsFalse(watch.Advance(2f, Lord(100f, 200f), Keep));
        Assert.IsTrue(watch.Advance(1f, Lord(100f, 200f), Keep), "5 s since the start");
        Assert.IsFalse(watch.Advance(4.9f, Lord(100f, 200f), Keep));
        Assert.IsTrue(watch.Advance(0.2f, Lord(100f, 200f), Keep));
    }

    [TestMethod]
    public void TheFiveSecondLine_IsNotDue_WithinTheTwentyMetres_OrWithNoKeep()
    {
        var near = Watch();
        var none = Watch();

        Assert.IsFalse(near.Advance(5f, Lord(19f, 0f), Keep));
        Assert.IsFalse(none.Advance(5f, Lord(100f, 200f), null));
        Assert.IsTrue(Watch().Advance(5f, Lord(20.5f, 0f), Keep));
    }

    [TestMethod]
    public void TheOutsideSeconds_RunWhileOutsideTheBoundary_AndStartOverWhenHeComesIn()
    {
        var watch = Watch();

        watch.Advance(2f, Lord(100f, 200f, inside: false), Keep);
        watch.Advance(3f, Lord(100f, 200f, inside: false), Keep);
        Assert.AreEqual(5f, watch.Outside, 1e-4f);

        watch.Advance(1f, Lord(100f, 200f, inside: true), Keep);
        Assert.AreEqual(0f, watch.Outside);
        Assert.AreEqual(5f, watch.MaxOutside, 1e-4f, "the worst stretch is kept");
    }

    [TestMethod]
    public void ARead_WithNoLordStanding_MovesNothing_ButTheClockRuns()
    {
        var watch = Watch();
        watch.Advance(1f, Lord(100f, 200f), Keep);

        watch.Advance(2f, LdLordFacts.None, Keep);

        Assert.AreEqual(3f, watch.Seconds, 1e-4f);
        Assert.AreEqual(Point.Point, watch.LastAt);
    }

    [TestMethod]
    public void TheWatch_RemembersTheWaveThePointAndTheDeathsAtTheMoveOut()
    {
        var watch = Watch();

        Assert.AreEqual(3, watch.Wave);
        Assert.AreEqual(Point, watch.Point);
        Assert.AreEqual(2, watch.DeathsAtOut);
    }
}
