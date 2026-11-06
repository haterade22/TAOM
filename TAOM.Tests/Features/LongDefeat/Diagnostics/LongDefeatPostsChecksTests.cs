using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>posts</c> verdict (phase3-plan.md section 6, Slice 7), one line per tower and wave: at the wave start every post the fill filled is manned (posts the cap left empty are named, not failed), 30 s into the wave each
/// post archer is still within 1.5 m and 2 m of his own post, and by the clear the tower's archers loosed at least one arrow. A float is written as the requirement, so a NaN fails it.
/// </summary>
[TestClass]
public class LongDefeatPostsChecksTests
{
    private static PostsWatch Watch(int expected = 4, int manned = 4, int shortBy = 0, PostsVariant variant = PostsVariant.A, int level = 1, int posts = 8, int failed = 0)
        => new("hornburg", 2, level, variant, 4, posts, expected, shortBy, failed, manned, shotsAtStart: 10);

    private static PostsWatch Sampled(PostsWatch w, int standing = 4, int held = 4, float drift = 0.4f, float dz = 0.2f)
    {
        w.Sample(new LdTowerPosts("hornburg", standing, held, drift, dz, 0));
        return w;
    }

    private static PostsWatch Good(int shots = 15)
    {
        var w = Sampled(Watch());
        w.Count(10 + shots);
        return w;
    }

    [TestMethod]
    public void AMannedTower_HeldAtTheSample_AndShooting_Passes()
    {
        var verdict = LongDefeatPostsChecks.Posts(Good(), false);

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, "tower=hornburg w=2 level=1 variant=A formation=4 posts=8 expected=4 manned=4 held=4 sampled=4 max_drift=0.4 max_dz=0.2 shots=15 split=0 out=0 short=0 failed=0");
    }

    [TestMethod]
    public void TheDetail_IsNameValuePairsOnly_SoTheLogSanitiserLeavesItAlone()
    {
        // OutcomeLog.SanitiseDetail joins a word with no '=' to the value before it with '_': "held=4 of 4" was logged as "held=4_of_4" and no pattern could match it.
        foreach (var verdict in new[] { LongDefeatPostsChecks.Posts(Good(), false), LongDefeatPostsChecks.Posts(Good(), true), LongDefeatPostsChecks.Posts(Watch(expected: 0, manned: 0), false), LongDefeatPostsChecks.Posts(Watch(), false) })
        {
            foreach (var word in verdict.Detail.Split(' '))
                Assert.IsTrue(word.IndexOf('=') > 0, $"'{word}' in '{verdict.Detail}' would be glued to the value before it");
        }
    }

    [TestMethod]
    public void ThePassLine_AsTheLogWritesIt_MatchesThePassPatternOfTheManifestRow()
    {
        // The same transform the game applies (OutcomeLog.SanitiseDetail) and the same text the verdict tool reads: a key renamed in Describe, or a new field, fails here instead of grading every real pass NOT_REACHED.
        var toml = RepoPaths.ReadSource("tools/ingame_checks/1079.toml");
        var row = toml.Substring(toml.IndexOf("id = \"taom.long-defeat.posts\"", System.StringComparison.Ordinal));
        var pattern = Regex.Match(row, @"^pass = \['(.*)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.IsTrue(pattern.Length > 0, "the posts row has no pass pattern");

        var second = Sampled(Watch(expected: 8, manned: 8, level: 2));
        second.Count(30);
        foreach (var verdict in new[] { LongDefeatPostsChecks.Posts(Good(), false), LongDefeatPostsChecks.Posts(second, false), LongDefeatPostsChecks.Posts(Good(), true) })
        {
            Assert.AreEqual(CheckResult.Pass, verdict.Result);
            var line = "[Check] id=taom.long-defeat.posts result=pass " + OutcomeLog.SanitiseDetail(verdict.Detail);
            Assert.IsTrue(Regex.IsMatch(line, pattern), line);
        }
    }

    [TestMethod]
    public void ASpawnThatFailed_IsNamedApartFromTheCap_AndFailsTheLine()
    {
        // 4 posts owed, the cap left none empty, one spawn threw: the expectation still stands at 4, so three archers are a fail, and the line names the failure.
        var w = Sampled(Watch(expected: 4, manned: 3, shortBy: 0, failed: 1), standing: 3, held: 3);
        w.Count(20);
        var verdict = LongDefeatPostsChecks.Posts(w, false);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "short=0 failed=1");
    }

    [TestMethod]
    public void TheDetail_NamesTheVariantAndTheSplitCounts()
    {
        var w = Good();
        w.MovedBack = 3;
        w.MovedOut = 1;
        var verdict = LongDefeatPostsChecks.Posts(w, false);

        StringAssert.Contains(verdict.Detail, "split=3 out=1");
        StringAssert.Contains(LongDefeatPostsChecks.Posts(Sampled(Watch(variant: PostsVariant.C)), false).Detail, "variant=C");
    }

    [TestMethod]
    public void FewerArchersThanTheFillOwed_Fails()
    {
        var w = Sampled(Watch(expected: 4, manned: 3), standing: 3, held: 3);
        w.Count(20);

        Assert.AreEqual(CheckResult.Fail, LongDefeatPostsChecks.Posts(w, false).Result);
    }

    [TestMethod]
    public void PostsTheCapLeftEmpty_AreNamedAndNotFailed()
    {
        var w = Sampled(Watch(expected: 2, manned: 2, shortBy: 2), standing: 2, held: 2);
        w.Count(20);
        var verdict = LongDefeatPostsChecks.Posts(w, false);

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, "short=2");
    }

    [TestMethod]
    public void AFillThatLeftNothingToProve_IsASkip()
    {
        var w = Watch(expected: 0, manned: 0, shortBy: 4);
        w.Count(10);
        var verdict = LongDefeatPostsChecks.Posts(w, false);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-archers");
    }

    [TestMethod]
    public void AnArcherOffHisPost_FailsTheHeldLeg_ByDistanceAndByHeight()
    {
        foreach (var (held, drift, dz) in new[] { (3, 3.2f, 0.2f), (3, 0.4f, 2.6f), (4, float.NaN, 0.2f), (4, 0.4f, float.NaN) })
        {
            var w = Sampled(Watch(), held: held, drift: drift, dz: dz);
            w.Count(20);

            Assert.AreEqual(CheckResult.Fail, LongDefeatPostsChecks.Posts(w, false).Result, $"held {held} drift {drift} dz {dz}");
        }
    }

    [TestMethod]
    public void ASampleWithNoOneStanding_IsASkip_NotAFail()
    {
        var w = Sampled(Watch(), standing: 0, held: 0, drift: 0f, dz: 0f);
        w.Count(12);
        var verdict = LongDefeatPostsChecks.Posts(w, false);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=none-standing");
    }

    [TestMethod]
    public void AWaveShorterThanTheSample_IsASkip_ButAFailStillFails()
    {
        var short1 = Watch();
        short1.Count(15);
        var verdict = LongDefeatPostsChecks.Posts(short1, false);
        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=short-wave");

        var bad = Watch(expected: 4, manned: 1);
        bad.Count(15);
        Assert.AreEqual(CheckResult.Fail, LongDefeatPostsChecks.Posts(bad, false).Result);
    }

    [TestMethod]
    public void NoArrowByTheClear_Fails_ButTheRunEndingFirstIsASkip()
    {
        var silent = Good(shots: 0);
        Assert.AreEqual(CheckResult.Fail, LongDefeatPostsChecks.Posts(silent, false).Result);

        var cut = LongDefeatPostsChecks.Posts(silent, true);
        Assert.AreEqual(CheckResult.Skip, cut.Result);
        StringAssert.Contains(cut.Detail, "reason=window-cut");
        StringAssert.Contains(cut.Detail, "ended=run");

        Assert.AreEqual(CheckResult.Pass, LongDefeatPostsChecks.Posts(Good(), true).Result, "a run that ended after the arrows came passes");
    }

    [TestMethod]
    public void TheWatchCounts_TheArrowsOfTheWaveAlone()
    {
        var w = Watch();
        w.Count(10);
        Assert.AreEqual(0, w.Shots);
        w.Count(17);
        Assert.AreEqual(7, w.Shots);
    }

    [TestMethod]
    public void TheVariantNames_ParseInEitherCase_AndAnythingElseKeepsTheDefault()
    {
        Assert.AreEqual(PostsVariant.A, PostsVariants.Parse("a"));
        Assert.AreEqual(PostsVariant.B, PostsVariants.Parse(" B "));
        Assert.AreEqual(PostsVariant.C, PostsVariants.Parse("c"));
        Assert.IsNull(PostsVariants.Parse(null));
        Assert.IsNull(PostsVariants.Parse(""));
        Assert.IsNull(PostsVariants.Parse("D"));
        Assert.AreEqual(PostsVariant.A, PostsVariants.Default, "the plan's first choice");
    }

    [TestMethod]
    public void TheHoldRule_IsClosedAtTheLimits()
    {
        Assert.IsTrue(PostsRules.Held(1.5f, 2f));
        Assert.IsFalse(PostsRules.Held(1.51f, 0f));
        Assert.IsFalse(PostsRules.Held(0f, 2.01f));
        Assert.IsFalse(PostsRules.Held(float.NaN, 0f));
    }
}

/// <summary>Which posts a fill spawns (D20): the empty ones, in order, inside the room the garrison cap leaves; the rest are short.</summary>
[TestClass]
public class PostsFillPlanTests
{
    [TestMethod]
    public void AnEmptyTower_SpawnsEveryPost_WhenThereIsRoom()
    {
        var plan = PostsFillPlan.For(4, new int[0], 100);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, plan.Spawn.ToArray());
        Assert.AreEqual(0, plan.Short);
    }

    [TestMethod]
    public void ARefill_SpawnsOnlyThePostsNoOneHolds()
    {
        var plan = PostsFillPlan.For(8, new[] { 0, 1, 2, 4, 5 }, 100);

        CollectionAssert.AreEqual(new[] { 3, 6, 7 }, plan.Spawn.ToArray());
        Assert.AreEqual(0, plan.Short);
    }

    [TestMethod]
    public void TheCap_StopsTheFill_AndTheRestIsShort()
    {
        var plan = PostsFillPlan.For(8, new[] { 0, 1 }, 3);

        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, plan.Spawn.ToArray());
        Assert.AreEqual(3, plan.Short);
    }

    [TestMethod]
    public void NoRoomAtAll_SpawnsNothing_AndNamesEveryEmptyPost()
    {
        foreach (var room in new[] { 0, -5 })
        {
            var plan = PostsFillPlan.For(4, new[] { 1 }, room);

            Assert.AreEqual(0, plan.Spawn.Count);
            Assert.AreEqual(3, plan.Short);
        }
    }

    [TestMethod]
    public void AFullTower_NeedsNothing()
    {
        var plan = PostsFillPlan.For(2, new[] { 0, 1 }, 10);

        Assert.AreEqual(0, plan.Spawn.Count);
        Assert.AreEqual(0, plan.Short);
        Assert.AreEqual(0, PostsFillPlan.For(0, new int[0], 10).Short);
    }
}
