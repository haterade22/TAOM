using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Spider;

namespace TAOM.Tests.Features.Spider;

/// <summary>
/// The slide meter of the spider gait step (#1559): the seconds in which a fast spider's leg tips hardly move against its body,
/// and the seconds it judged at all.
/// </summary>
[TestClass]
public class SpiderSlideMeterTests
{
    private const float Tick = 1f / 60f;

    private static void Feed(SpiderSlideMeter meter, object mount, float from, float seconds, float speed, float legSpeed)
    {
        for (var t = from; t < from + seconds; t += Tick)
            meter.Add(mount, t, speed, legSpeed * Tick);
    }

    [TestMethod]
    public void ASpiderWhoseLegsStep_IsNotSliding()
    {
        var meter = new SpiderSlideMeter();

        Feed(meter, new object(), 0f, 5f, speed: 7f, legSpeed: 6.7f);

        Assert.AreEqual(0f, meter.SlideSeconds);
    }

    [TestMethod]
    public void AFastSpiderWhoseLegsHardlyMove_SlidesForAboutThatLong()
    {
        var meter = new SpiderSlideMeter();

        Feed(meter, new object(), 0f, 3f, speed: 7f, legSpeed: 1.2f);

        Assert.AreEqual(3f, meter.SlideSeconds, 0.6f);
        Assert.IsTrue(meter.SlideSeconds >= 2.4f, meter.SlideSeconds.ToString());
    }

    [TestMethod]
    public void ASlowSpider_IsNeverJudged()
    {
        var meter = new SpiderSlideMeter();

        Feed(meter, new object(), 0f, 5f, speed: SpiderSlideMeter.FastMetresPerSecond - 0.1f, legSpeed: 0f);

        Assert.AreEqual(0f, meter.SlideSeconds);
    }

    [TestMethod]
    public void TheShare_IsOfTheSpidersOwnSpeed()
    {
        var below = new SpiderSlideMeter();
        var above = new SpiderSlideMeter();

        Feed(below, new object(), 0f, 2f, speed: 10f, legSpeed: 10f * SpiderSlideMeter.LegShare - 0.2f);
        Feed(above, new object(), 0f, 2f, speed: 10f, legSpeed: 10f * SpiderSlideMeter.LegShare + 0.2f);

        Assert.IsTrue(below.SlideSeconds > 1f);
        Assert.AreEqual(0f, above.SlideSeconds);
    }

    [TestMethod]
    public void TwoSpiders_AddUp_AndEachHasItsOwnWindow()
    {
        var meter = new SpiderSlideMeter();
        var a = new object();
        var b = new object();

        for (var t = 0f; t < 2f; t += Tick)
        {
            meter.Add(a, t, 7f, 1f * Tick);
            meter.Add(b, t, 7f, 6.5f * Tick);
        }

        Assert.AreEqual(2f, meter.SlideSeconds, 0.6f);
    }

    [TestMethod]
    public void AGapInTheReadings_StartsANewWindow()
    {
        var meter = new SpiderSlideMeter();
        var mount = new object();

        Feed(meter, mount, 0f, 0.3f, speed: 7f, legSpeed: 0f);
        Feed(meter, mount, 5f, 0.3f, speed: 7f, legSpeed: 0f);

        Assert.AreEqual(0f, meter.SlideSeconds, "two windows of 0.3 s, neither a whole one");
    }

    [TestMethod]
    public void ASlowReading_IsLeftOut_AndDoesNotHideTheSlideAroundIt()
    {
        var meter = new SpiderSlideMeter();
        var mount = new object();

        var t = 0f;
        for (var i = 0; i < 240; i++, t += Tick)
            meter.Add(mount, t, i % 8 == 7 ? 3.9f : 7f, 0f);

        Assert.IsTrue(meter.SlideSeconds >= 2.5f, meter.SlideSeconds.ToString());
    }

    [TestMethod]
    public void AHalt_EndsTheWindow_SoARunBeforeItIsNotWeighedWithASlideAfterIt()
    {
        var meter = new SpiderSlideMeter();
        var mount = new object();

        Feed(meter, mount, 0f, 0.375f, speed: 7f, legSpeed: 6.7f);
        Feed(meter, mount, 0.375f, 2f, speed: 0f, legSpeed: 0f);
        Feed(meter, mount, 2.375f, 0.7f, speed: 7f, legSpeed: 0f);

        Assert.IsTrue(meter.SlideSeconds >= 0.5f, meter.SlideSeconds.ToString());
    }

    [TestMethod]
    public void WhatWasJudged_IsCounted_SlidOrNot()
    {
        var meter = new SpiderSlideMeter();

        Feed(meter, new object(), 0f, 3f, speed: 7f, legSpeed: 6.7f);

        Assert.AreEqual(0f, meter.SlideSeconds);
        Assert.AreEqual(3f, meter.JudgedSeconds, 0.6f);
    }

    [TestMethod]
    public void ReadingsTooFarApart_ASlowSpider_AndUnreadLegs_JudgeNothing()
    {
        var sparse = new SpiderSlideMeter();
        var slow = new SpiderSlideMeter();
        var unread = new SpiderSlideMeter();
        var mount = new object();

        for (var t = 0f; t < 5f; t += 0.25f)
            sparse.Add(mount, t, 7f, 0f);
        Feed(slow, mount, 0f, 5f, speed: 3f, legSpeed: 0f);
        for (var t = 0f; t < 5f; t += Tick)
            unread.Add(mount, t, 7f, float.NaN);

        Assert.AreEqual(0f, sparse.JudgedSeconds);
        Assert.AreEqual(0f, slow.JudgedSeconds);
        Assert.AreEqual(0f, unread.JudgedSeconds);
        Assert.AreEqual(0f, sparse.SlideSeconds + slow.SlideSeconds + unread.SlideSeconds);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f)]
    [DataRow(7f, float.NaN)]
    public void AReadingThatIsNoNumber_IsNotCounted_AndDoesNotPoisonTheSum(float speed, float legTravel)
    {
        var meter = new SpiderSlideMeter();
        var mount = new object();

        meter.Add(mount, 0f, 7f, 0f);
        meter.Add(mount, Tick, speed, legTravel);
        Feed(meter, mount, 2f * Tick, 1.2f, speed: 7f, legSpeed: 0f);

        Assert.IsTrue(meter.SlideSeconds >= 0.5f && meter.SlideSeconds <= 1.3f, "the still legs after it are still counted, as a number: " + meter.SlideSeconds);
    }
}
