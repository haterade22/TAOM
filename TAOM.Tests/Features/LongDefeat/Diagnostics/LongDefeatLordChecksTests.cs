using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>rise</c> and <c>general-down</c> verdicts and the extended <c>player-death</c> line (phase4-plan.md section 6, Slice 4). Pure: the director reads the field, these turn the numbers into pass, fail or skip.
/// A float is written as the requirement, so a NaN fails it.
/// </summary>
[TestClass]
public class LongDefeatLordChecksTests
{
    private static readonly LdPlace Keep = new(100f, 200f, 30f, 0f, 0f);

    private static LdLordFacts Lord(bool present = true, bool own = true, float x = 100.4f, float y = 200f) =>
        new(present, own, "lord_4_1", false, new LdPoint(x, y, 31f), true, "Infantry", null, -1f, -1f, "");

    private static CheckVerdict Rise(string at = "recorded", bool autoplay = false, LdRise? rise = null, LdLordFacts? lord = null, LdPlace? place = null, bool noPlace = false,
        string controller = "Player", int others = 0, bool general = true, bool owner = true) =>
        LongDefeatLordChecks.Rise(at, autoplay, rise ?? new LdRise(true, LdHandBack.None, false), lord ?? Lord(), noPlace ? null : place ?? Keep, new LdControlRead(controller, others), general, owner);

    [TestMethod]
    public void ARiseAtTheKeep_ByAPerson_Passes()
    {
        var v = Rise();

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("at=recorded main_is_lord=1 dist=0.4 dz=1.0 controller=Player want=Player others=0 handed_back=none was_using=0 general_is_lord=1 owner_is_lord=1", v.Detail);
    }

    [TestMethod]
    public void UnderAutoplay_TheControllerReadAfterAdoptionIsAi()
    {
        Assert.AreEqual(CheckResult.Pass, Rise(autoplay: true, controller: "AI").Result);
        Assert.AreEqual(CheckResult.Fail, Rise(autoplay: true, controller: "Player").Result, "adoption hands the body to the AI under autoplay");
        Assert.AreEqual(CheckResult.Fail, Rise(autoplay: false, controller: "AI").Result, "a person plays the lord himself");
    }

    [TestMethod]
    public void ARefusedSpawn_Fails_WithItsReason_AndHandsNothingBack()
    {
        var v = LongDefeatLordChecks.RiseRefused("recorded");

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("at=recorded reason=spawn-refused handed_back=none", v.Detail);
    }

    [TestMethod]
    public void EveryLeg_OfTheRise_FailsTheLineAlone()
    {
        Assert.AreEqual(CheckResult.Fail, Rise(rise: LdRise.Refused).Result, "no spawn");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: Lord(present: false)).Result, "no main agent");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: Lord(own: false)).Result, "the main agent is a soldier, not the lord");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: Lord(x: 102.5f)).Result, "2.5 m off the place");
        Assert.AreEqual(CheckResult.Fail, Rise(others: 1).Result, "a second Player body");
        Assert.AreEqual(CheckResult.Fail, Rise(general: false).Result, "the general is not the lord");
        Assert.AreEqual(CheckResult.Fail, Rise(owner: false).Result, "the order owner is not the lord");
    }

    [TestMethod]
    public void TheDistance_IsOnTheGround_AndIsLimitedToTwoMetres()
    {
        Assert.AreEqual(CheckResult.Pass, Rise(lord: Lord(x: 101.99f)).Result);
        Assert.AreEqual(CheckResult.Fail, Rise(lord: Lord(x: 102.01f)).Result);
        var high = new LdLordFacts(true, true, "lord_4_1", false, new LdPoint(100f, 200f, 90f), true, "", null, -1f, -1f, "");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: high).Result, "60 m above the keep is another floor, not the place");
        var settling = new LdLordFacts(true, true, "lord_4_1", false, new LdPoint(100f, 200f, Keep.Z + 2.9f), true, "", null, -1f, -1f, "");
        Assert.AreEqual(CheckResult.Pass, Rise(lord: settling).Result, "a body may settle within 3 m of the place's height");
        var over = new LdLordFacts(true, true, "lord_4_1", false, new LdPoint(100f, 200f, Keep.Z - 3.1f), true, "", null, -1f, -1f, "");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: over).Result);
        var noHeight = new LdLordFacts(true, true, "lord_4_1", false, new LdPoint(100f, 200f, float.NaN), true, "", null, -1f, -1f, "");
        Assert.AreEqual(CheckResult.Fail, Rise(lord: noHeight).Result, "a height that is not a number is no place");
    }

    [TestMethod]
    public void ADistanceThatIsNotANumber_FailsTheGate()
    {
        Assert.AreEqual(CheckResult.Fail, Rise(lord: Lord(x: float.NaN)).Result);
        Assert.AreEqual(CheckResult.Fail, Rise(place: new LdPlace(float.NaN, 0f, 0f, 0f, 0f)).Result);
    }

    [TestMethod]
    public void ARiseWithNoPlace_JudgesNoDistance_AndSaysSo()
    {
        var v = Rise(noPlace: true, lord: Lord(x: 5000f));

        Assert.AreEqual(CheckResult.Pass, v.Result, "the field's own frame placed him: there is no point to be near");
        StringAssert.Contains(v.Detail, "dist=none");
    }

    [DataTestMethod]
    [DataRow(0, "none")]
    [DataRow(1, "1")]
    [DataRow(2, "noop")]
    [DataRow(3, "dead")]
    public void TheHandBack_IsNamedInTheLine(int handBack, string text)
    {
        Assert.AreEqual(new[] { LdHandBack.None, LdHandBack.Done, LdHandBack.Noop, LdHandBack.Dead }[handBack], (LdHandBack)handBack, "the rows follow the enum's order");
        var v = Rise(rise: new LdRise(true, (LdHandBack)handBack, false));

        Assert.AreEqual(CheckResult.Pass, v.Result);
        StringAssert.Contains(v.Detail, " handed_back=" + text + " ");
    }

    [TestMethod]
    public void ASoldierWhoWasUsingAMachine_IsNamed_AndStillPasses()
    {
        var v = Rise(rise: new LdRise(true, LdHandBack.Done, true));

        Assert.AreEqual(CheckResult.Pass, v.Result, "R23: his use is not stopped first, and the line says so");
        StringAssert.Contains(v.Detail, "was_using=1");
    }

    [TestMethod]
    public void TheRiseDetail_IsNameValuePairsOnly_SoTheLogSanitiserLeavesItAlone()
    {
        foreach (var v in new[] { Rise(), Rise(noPlace: true), LongDefeatLordChecks.RiseRefused("scene"), Rise(lord: Lord(x: float.NaN)) })
        {
            foreach (var word in v.Detail.Split(' '))
                Assert.IsTrue(word.IndexOf('=') > 0, $"'{word}' in '{v.Detail}' would be glued to the value before it");
        }
    }

    // ---- general-down ----

    [TestMethod]
    public void TheSeatAfterAFall_IsAHero_OrNobody_NeverTheDeadBody()
    {
        var aragorn = new LdGeneralSeat(GeneralSeatKind.Hero, "aragorn");
        var none = LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.None), LdGeneralSeat.Empty, false);
        var hero = LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Hero, "aragorn"), aragorn, true);
        var dead = LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Stale), LdGeneralSeat.Empty, false);

        Assert.AreEqual((CheckResult.Pass, "seat=none want=none"), (none.Result, none.Detail));
        Assert.AreEqual((CheckResult.Pass, "seat=hero:aragorn want=hero:aragorn stands=1"), (hero.Result, hero.Detail));
        Assert.AreEqual((CheckResult.Fail, "seat=stale want=none"), (dead.Result, dead.Detail));
    }

    [TestMethod]
    public void AHeroInTheSeat_MustBeTheOneTheRunChose()
    {
        var aragorn = new LdGeneralSeat(GeneralSeatKind.Hero, "aragorn");

        Assert.AreEqual(CheckResult.Fail, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Hero, "gimli"), aragorn, false).Result, "another live hero is the wrong general");
        Assert.AreEqual(CheckResult.Fail, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Hero, "aragorn"), LdGeneralSeat.Empty, false).Result, "the run chose nobody");
        var unseated = LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.None), aragorn, true);
        Assert.AreEqual((CheckResult.Fail, "seat=none want=hero:aragorn stands=1"), (unseated.Result, unseated.Detail), "the chosen hero stands and the seat is empty: the write never landed (Codex re-review, 2026-11-21)");
        Assert.AreEqual(CheckResult.Pass, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.None), aragorn, false).Result, "the chosen hero may have fallen on the same tick: nobody is never wrong");
        Assert.AreEqual(CheckResult.Fail, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Main), LdGeneralSeat.Lord, false).Result, "with no main agent a main seat is a read of a body that is gone");
    }

    [TestMethod]
    public void AReadBackStillOwedWhenTheRunEnds_IsASkipThatNamesIt()
    {
        var v = LongDefeatLordChecks.RunEnded("at=recorded");

        Assert.AreEqual((CheckResult.Skip, "at=recorded reason=run-ended"), (v.Result, v.Detail));
    }

    [TestMethod]
    public void ASeatReadWithAMainAgentStanding_IsASkip_NotAPass()
    {
        var v = LongDefeatLordChecks.GeneralDown(true, new LdSeatRead(LdSeatKind.Main), LdGeneralSeat.Lord, false);

        Assert.AreEqual(CheckResult.Skip, v.Result, "the check is about a lord with no body; a take-over beat the read");
        Assert.AreEqual("seat=main want=lord reason=has-main", v.Detail);
    }

    [TestMethod]
    public void TheGeneralDetail_IsNameValuePairsOnly()
    {
        foreach (var v in new[] { LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Hero, "aragorn"), new LdGeneralSeat(GeneralSeatKind.Hero, "aragorn"), false), LongDefeatLordChecks.GeneralDown(true, new LdSeatRead(LdSeatKind.Main), LdGeneralSeat.Lord, false), LongDefeatLordChecks.RunEnded("seat=unread") })
        {
            foreach (var word in v.Detail.Split(' '))
                Assert.IsTrue(word.IndexOf('=') > 0, v.Detail);
        }
    }

    // ---- player-death ----

    [TestMethod]
    public void PlayerDeath_NamesWhoTookTheSoldier_OnlyForATakeover()
    {
        Assert.AreEqual("via=takeover by=bot", LongDefeatChecks.PlayerDeath(true, "takeover", "bot").Detail);
        Assert.AreEqual("via=takeover by=person", LongDefeatChecks.PlayerDeath(true, "takeover", "person").Detail);
        Assert.AreEqual("via=rise", LongDefeatChecks.PlayerDeath(true, "rise").Detail);
        Assert.AreEqual("via=respawn", LongDefeatChecks.PlayerDeath(true, "respawn").Detail, "the old spelling stays valid for the build with no lord adapter");
    }

    // ---- fort-points: the rise point (D49) ----

    private static readonly (string, LdPointFacts)[] NoTowerPoints = new (string, LdPointFacts)[0];

    [TestMethod]
    public void FortPoints_WithOnlyARecordedKeep_JudgesTheRisePoint_AndKeepsItOutOfThePointCount()
    {
        var ok = LongDefeatGateChecks.FortPoints(NoTowerPoints, new LdPointFacts(true, true));
        var bad = LongDefeatGateChecks.FortPoints(NoTowerPoints, new LdPointFacts(false, true));

        Assert.AreEqual((CheckResult.Pass, "points=0 failed=0 rise=recorded"), (ok.Result, ok.Detail));
        Assert.AreEqual((CheckResult.Fail, "points=0 failed=0 rise=scene-fallback rise_navmesh=no rise_boundary=yes"), (bad.Result, bad.Detail));
        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.FortPoints(NoTowerPoints, new LdPointFacts(true, false)).Result, "a keep outside the boundary fails too");
    }

    [TestMethod]
    public void FortPoints_WithNothingRecorded_IsStillTheOldSkip()
    {
        var v = LongDefeatGateChecks.FortPoints(NoTowerPoints);

        Assert.AreEqual((CheckResult.Skip, "points=0 reason=none-recorded"), (v.Result, v.Detail));
    }

    [TestMethod]
    public void FortPoints_ABadTowerPointAndAGoodRisePoint_Fails_AndAGoodTowerPointWithABadRisePointFails()
    {
        var tower = new[] { ("hornburg.post[0]", new LdPointFacts(false, true)) };
        var good = new[] { ("hornburg.post[0]", new LdPointFacts(true, true)) };

        Assert.AreEqual(CheckResult.Fail, LongDefeatGateChecks.FortPoints(tower, new LdPointFacts(true, true)).Result);
        var v = LongDefeatGateChecks.FortPoints(good, new LdPointFacts(false, true));
        Assert.AreEqual(CheckResult.Fail, v.Result);
        StringAssert.StartsWith(v.Detail, "points=1 failed=0 rise=scene-fallback");
        Assert.AreEqual(CheckResult.Pass, LongDefeatGateChecks.FortPoints(good, new LdPointFacts(true, true)).Result);
    }

    [TestMethod]
    public void TheFortPointsPassLine_WithTheRisePoint_StillMatchesThePassPatternOfTheManifestRow()
    {
        MatchesRow("taom.long-defeat.fort-points", LongDefeatChecks.FortPointsId, LongDefeatGateChecks.FortPoints(NoTowerPoints, new LdPointFacts(true, true)));
        MatchesRow("taom.long-defeat.fort-points", LongDefeatChecks.FortPointsId, LongDefeatGateChecks.FortPoints(new[] { ("a", new LdPointFacts(true, true)) }, new LdPointFacts(true, true)));
    }

    // ---- the manifest rows ----

    private static string Row(string id)
    {
        var toml = RepoPaths.ReadSource("tools/ingame_checks/1079.toml");
        var at = toml.IndexOf($"id = \"{id}\"", System.StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, $"the manifest has no row for {id}");
        var row = toml.Substring(at);
        var next = row.IndexOf("[[check]]", 5, System.StringComparison.Ordinal);
        return next > 0 ? row.Substring(0, next) : row;
    }

    private static void MatchesRow(string id, string checkId, CheckVerdict verdict)
    {
        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        var pattern = Regex.Match(Row(id), @"^pass = \['(.*)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.IsTrue(pattern.Length > 0, $"the {id} row has no pass pattern");
        var line = $"[Check] id={checkId} result=pass " + OutcomeLog.SanitiseDetail(verdict.Detail);
        Assert.IsTrue(Regex.IsMatch(line, pattern), line);
    }

    [TestMethod]
    public void TheRisePassLine_AsTheLogWritesIt_MatchesThePassPatternOfTheManifestRow()
    {
        foreach (var at in new[] { "recorded", "scene", "home" })
        {
            MatchesRow("taom.long-defeat.rise", LongDefeatChecks.RiseId, Rise(at));
            MatchesRow("taom.long-defeat.rise", LongDefeatChecks.RiseId, Rise(at, autoplay: true, controller: "AI", rise: new LdRise(true, LdHandBack.Noop, false)));
        }

        MatchesRow("taom.long-defeat.rise", LongDefeatChecks.RiseId, Rise(rise: new LdRise(true, LdHandBack.Done, true)));
        MatchesRow("taom.long-defeat.rise", LongDefeatChecks.RiseId, Rise(rise: new LdRise(true, LdHandBack.Dead, false)));
        MatchesRow("taom.long-defeat.rise", LongDefeatChecks.RiseId, Rise(noPlace: true));
    }

    [TestMethod]
    public void TheGeneralDownPassLine_AsTheLogWritesIt_MatchesThePassPatternOfTheManifestRow()
    {
        var aragorn = new LdGeneralSeat(GeneralSeatKind.Hero, "aragorn");
        MatchesRow("taom.long-defeat.general-down", LongDefeatChecks.GeneralDownId, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.None), LdGeneralSeat.Empty, false));
        MatchesRow("taom.long-defeat.general-down", LongDefeatChecks.GeneralDownId, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.None), aragorn, false));
        MatchesRow("taom.long-defeat.general-down", LongDefeatChecks.GeneralDownId, LongDefeatLordChecks.GeneralDown(false, new LdSeatRead(LdSeatKind.Hero, "aragorn"), aragorn, true));
    }

    [TestMethod]
    public void ThePlayerDeathPassLine_MatchesThePatternOfTheManifestRow_ForEveryVia()
    {
        foreach (var v in new[] { LongDefeatChecks.PlayerDeath(true, "respawn"), LongDefeatChecks.PlayerDeath(true, "rise"), LongDefeatChecks.PlayerDeath(true, "takeover", "bot"), LongDefeatChecks.PlayerDeath(true, "takeover", "person") })
            MatchesRow("taom.long-defeat.player-death", LongDefeatChecks.PlayerDeathId, v);
    }

    [TestMethod]
    public void TheRiseAndGeneralDownRows_AreNotReachedWhenAbsent_AndKeepSkipLinesOutOfTheFailList()
    {
        foreach (var id in new[] { "taom.long-defeat.rise", "taom.long-defeat.general-down" })
        {
            var row = Row(id);
            StringAssert.Contains(row, "absence = \"not-reached\"");
            Assert.IsFalse(Regex.IsMatch(row, @"^fail = \[.*result=skip", RegexOptions.Multiline), "a skip line in the fail list turns NOT REACHED into FAIL");
        }
    }
}
