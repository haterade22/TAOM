using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Tests.Features.LongDefeat;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// The verdicts behind the #1361 [Check] lines (tools/ingame_checks/1361.toml). The detail strings are pinned word for
/// word, and each sample line is matched against the manifest's own pass and fail patterns, so a format change that the
/// manifest no longer reads fails here and not in a game run.
/// </summary>
[TestClass]
public class HeroRangedChecksTests
{
    private const string Hero = "taom_hero_legolas";

    // ---- precision ----

    [TestMethod]
    public void Precision_AllZero_Pass()
    {
        var v = HeroRangedChecks.Precision(Hero, zeroAimError: true, nonZero: 0, shooterError: 0f, inaccuracy: 0f);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas ranged=1 nonzero=0 shooter_error=0.0000 inaccuracy=0.0000", v.Detail);
        AssertManifestReads(HeroRangedChecks.PrecisionId, v);
    }

    [TestMethod]
    public void Precision_OneNonZero_Fail()
    {
        var v = HeroRangedChecks.Precision(Hero, zeroAimError: true, nonZero: 1, shooterError: 0.0125f, inaccuracy: 0f);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas ranged=1 nonzero=1 shooter_error=0.0125 inaccuracy=0.0000", v.Detail);
        AssertManifestReads(HeroRangedChecks.PrecisionId, v);
    }

    [TestMethod]
    public void Precision_NaN_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, HeroRangedChecks.Precision(Hero, true, 0, float.NaN, 0f).Result);
        Assert.AreEqual(CheckResult.Fail, HeroRangedChecks.Precision(Hero, true, 0, 0f, float.NaN).Result);
        Assert.AreEqual(CheckResult.Fail, HeroRangedChecks.Precision(Hero, true, 0, float.PositiveInfinity, 0f).Result);
    }

    // The kit does not zero his aim, so there is nothing to judge (HeroRangedHooks.ApplyFor acts only on ZeroAimError).
    [TestMethod]
    public void Precision_KitSwitchOff_SkipOff()
    {
        var v = HeroRangedChecks.Precision(Hero, zeroAimError: false, nonZero: 10, shooterError: 0.0125f, inaccuracy: 0.01f);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=off hero=taom_hero_legolas ranged=1 nonzero=10 shooter_error=0.0125 inaccuracy=0.0100", v.Detail);
        AssertManifestReads(HeroRangedChecks.PrecisionId, v);
    }

    [TestMethod]
    public void NeverRanged_Skip()
    {
        var v = HeroRangedChecks.NeverRanged(Hero);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=never-ranged hero=taom_hero_legolas", v.Detail);
        AssertManifestReads(HeroRangedChecks.PrecisionId, v);
    }

    // ---- hits ----

    [TestMethod]
    public void Hits_HalfOfTwenty_Pass()
    {
        var v = HeroRangedChecks.Hits(Hero, shots: 20, contact: 10, shield: 3);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas shots=20 contact=10 share=0.50 shield=3", v.Detail);
        AssertManifestReads(HeroRangedChecks.HitsId, v);
    }

    [TestMethod]
    public void Hits_UnderHalf_Fail()
    {
        var v = HeroRangedChecks.Hits(Hero, shots: 40, contact: 19, shield: 0);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas shots=40 contact=19 share=0.48 shield=0", v.Detail);
        AssertManifestReads(HeroRangedChecks.HitsId, v);
    }

    [TestMethod]
    public void Hits_FewShots_Skip()
    {
        var v = HeroRangedChecks.Hits(Hero, shots: 19, contact: 19, shield: 0);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=few-shots hero=taom_hero_legolas shots=19 contact=19 share=1.00 shield=0", v.Detail);
        AssertManifestReads(HeroRangedChecks.HitsId, v);
    }

    [TestMethod]
    public void Hits_NoShots_SkipWithZeroShare()
    {
        var v = HeroRangedChecks.Hits(Hero, shots: 0, contact: 0, shield: 0);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.Contains(v.Detail, "share=0.00");
    }

    // ---- pierce ----

    [TestMethod]
    public void Pierce_OnePassed_Pass()
    {
        var v = HeroRangedChecks.Pierce(Hero, pierceShields: true, shieldHits: 2, passed: 1, broken: 1);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas shield_hits=2 passed=1 broken=1", v.Detail);
        AssertManifestReads(HeroRangedChecks.PierceId, v);
    }

    [TestMethod]
    public void Pierce_FiveShieldHitsNonePassed_Fail()
    {
        var v = HeroRangedChecks.Pierce(Hero, pierceShields: true, shieldHits: 5, passed: 0, broken: 0);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        AssertManifestReads(HeroRangedChecks.PierceId, v);
    }

    [TestMethod]
    public void Pierce_FourShieldHits_Skip()
    {
        var v = HeroRangedChecks.Pierce(Hero, pierceShields: true, shieldHits: 4, passed: 0, broken: 4);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=few-shield-hits hero=taom_hero_legolas shield_hits=4 passed=0 broken=4", v.Detail);
        AssertManifestReads(HeroRangedChecks.PierceId, v);
    }

    // The kit grants no pierce (CombatMechanicsHooks asks PiercesShields), so shield hits with no pass-through prove nothing.
    [TestMethod]
    public void Pierce_KitSwitchOff_SkipOff()
    {
        var v = HeroRangedChecks.Pierce(Hero, pierceShields: false, shieldHits: 9, passed: 0, broken: 2);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=off hero=taom_hero_legolas shield_hits=9 passed=0 broken=2", v.Detail);
        AssertManifestReads(HeroRangedChecks.PierceId, v);
        Assert.AreEqual(CheckResult.Skip, HeroRangedChecks.Pierce(Hero, false, 9, 1, 0).Result);
    }

    // ---- multishot ----

    private static CheckVerdict Multi(bool item = true, int windows = 1, int windowShots = 6, int fired = 4,
        int noTarget = 0, int outOfRange = 0, int hits = 1) =>
        HeroRangedChecks.MultiShot(Hero, item, windows, windowShots, fired, noTarget, outOfRange, hits);

    [TestMethod]
    public void MultiShot_FiredAndHit_Pass()
    {
        var v = Multi(fired: 2, hits: 1, noTarget: 3, outOfRange: 1);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas item=1 windows=1 window_shots=6 fired=2 no_target=3 out_of_range=1 hits=1", v.Detail);
        AssertManifestReads(HeroRangedChecks.MultiShotId, v);
    }

    [TestMethod]
    public void MultiShot_WindowShotsButNoneFired_Fail()
    {
        var v = Multi(item: false, windows: 1, windowShots: 3, fired: 0, hits: 0);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas item=0 windows=1 window_shots=3 fired=0 no_target=0 out_of_range=0 hits=0", v.Detail);
        AssertManifestReads(HeroRangedChecks.MultiShotId, v);
    }

    [TestMethod]
    public void MultiShot_TenFiredNoHit_Fail()
    {
        Assert.AreEqual(CheckResult.Fail, Multi(fired: 10, hits: 0).Result);
        Assert.AreEqual(CheckResult.Skip, Multi(fired: 9, hits: 0).Result);
    }

    [TestMethod]
    public void MultiShot_NoWindow_SkipNoWindow()
    {
        var v = Multi(windows: 0, windowShots: 0, fired: 0, hits: 0);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.StartsWith(v.Detail, "reason=no-window hero=taom_hero_legolas ");
        AssertManifestReads(HeroRangedChecks.MultiShotId, v);
    }

    [TestMethod]
    public void MultiShot_FewShots_SkipFewShots()
    {
        var v = Multi(windows: 1, windowShots: 2, fired: 0, hits: 0);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.StartsWith(v.Detail, "reason=few-shots hero=taom_hero_legolas ");
        Assert.AreEqual(CheckResult.Skip, Multi(fired: 1, hits: 1).Result, "one extra proves too little");
    }

    // ---- arrow-kill ----

    [TestMethod]
    public void ArrowKill_OneKill_Pass()
    {
        var v = HeroRangedChecks.ArrowKill(Hero, bodyHits: 30, head: 2, exempt: 4, deadShooter: 0, calls: 26, kills: 1);

        Assert.AreEqual(CheckResult.Pass, v.Result);
        Assert.AreEqual("hero=taom_hero_legolas body_hits=30 head=2 exempt=4 dead_shooter=0 calls=26 kills=1", v.Detail);
        AssertManifestReads(HeroRangedChecks.ArrowKillId, v);
    }

    [TestMethod]
    public void ArrowKill_FiftyCallsNoKill_Fail()
    {
        var v = HeroRangedChecks.ArrowKill(Hero, 60, 0, 10, 0, calls: 50, kills: 0);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        AssertManifestReads(HeroRangedChecks.ArrowKillId, v);
    }

    [TestMethod]
    public void ArrowKill_FewCalls_Skip()
    {
        var v = HeroRangedChecks.ArrowKill(Hero, 49, 0, 0, 1, calls: 49, kills: 0);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        Assert.AreEqual("reason=few-calls hero=taom_hero_legolas body_hits=49 head=0 exempt=0 dead_shooter=1 calls=49 kills=0", v.Detail);
        AssertManifestReads(HeroRangedChecks.ArrowKillId, v);
    }

    // ---- the mission-end writer ----

    [TestMethod]
    public void WriteEnd_WritesEveryCheckUnderTheHeroKey()
    {
        var log = new RecordingOutcomeLog();
        var tally = new HeroRangedTally(Hero, 2) { Shots = 25, Contact = 20 };

        HeroRangedChecks.WriteEnd(log, "hr3", tally);

        foreach (var id in new[] { HeroRangedChecks.PrecisionId, HeroRangedChecks.HitsId, HeroRangedChecks.PierceId, HeroRangedChecks.MultiShotId, HeroRangedChecks.ArrowKillId })
        {
            var lines = log.Of(id);
            Assert.AreEqual(1, lines.Count, id);
            StringAssert.StartsWith(lines[0], "hr3-taom_hero_legolas-2 ", id);
        }
        Assert.IsTrue(log.Has("skip", HeroRangedChecks.PrecisionId), "no sample: never-ranged");
        Assert.IsTrue(log.Has("pass", HeroRangedChecks.HitsId));
    }

    [TestMethod]
    public void WriteEnd_PierceReadsTheKitSwitchTheTallyHolds()
    {
        var off = new RecordingOutcomeLog();
        var on = new RecordingOutcomeLog();

        HeroRangedChecks.WriteEnd(off, "hr1", new HeroRangedTally(Hero, 1) { ShieldHits = 6 });
        HeroRangedChecks.WriteEnd(on, "hr1", new HeroRangedTally(Hero, 1) { ShieldHits = 6, PierceShields = true });

        StringAssert.Contains(off.Of(HeroRangedChecks.PierceId)[0], "reason=off ");
        Assert.IsTrue(off.Has("skip", HeroRangedChecks.PierceId));
        Assert.IsTrue(on.Has("fail", HeroRangedChecks.PierceId));
    }

    [TestMethod]
    public void WriteEnd_PrecisionAlreadyWritten_NoSecondPrecisionLine()
    {
        var log = new RecordingOutcomeLog();
        var tally = new HeroRangedTally(Hero, 1) { PrecisionWritten = true };

        HeroRangedChecks.WriteEnd(log, "hr1", tally);

        Assert.AreEqual(0, log.Of(HeroRangedChecks.PrecisionId).Count);
    }

    [TestMethod]
    public void EveryId_IsAValidOutcomeId()
    {
        foreach (var id in new[] { HeroRangedChecks.PrecisionId, HeroRangedChecks.HitsId, HeroRangedChecks.PierceId, HeroRangedChecks.MultiShotId, HeroRangedChecks.ArrowKillId })
            Assert.IsTrue(Regex.IsMatch(id, @"^[a-z0-9]+(\.[a-z0-9-]+)+$"), id);
    }

    // ---- the manifest ----

    // The pass or fail line the outcome log writes ("[Check] id=<id> result=<r> <detail>", OutcomeLog.cs) must match a
    // pattern of its own result in 1361.toml and none of the other; a skip matches neither.
    private static void AssertManifestReads(string id, CheckVerdict v)
    {
        var result = v.Result == CheckResult.Pass ? "pass" : v.Result == CheckResult.Fail ? "fail" : "skip";
        var line = $"2026-11-16 12:00:00 [INFO] [Check] id={id} result={result} {v.Detail}";
        var (pass, fail) = Patterns(id);

        var passHit = pass.Any(p => Regex.IsMatch(line, p));
        var failHit = fail.Any(p => Regex.IsMatch(line, p));
        Assert.AreEqual(v.Result == CheckResult.Pass, passHit, "pass patterns against: " + line);
        Assert.AreEqual(v.Result == CheckResult.Fail, failHit, "fail patterns against: " + line);
    }

    private static (List<string> Pass, List<string> Fail) Patterns(string id)
    {
        var toml = File.ReadAllText(RepoPaths.RepoPath("tools", "ingame_checks", "1361.toml")).Replace("\r\n", "\n");
        var blocks = toml.Split(new[] { "[[check]]" }, StringSplitOptions.None);
        var block = blocks.Single(b => b.Contains($"\nid = \"{id}\"\n"));
        return (Quoted(Section(block, "pass")), Quoted(Section(block, "fail")));
    }

    private static string Section(string block, string key)
    {
        var start = block.IndexOf($"\n{key} = [", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, key + " not found");
        var next = Regex.Match(block.Substring(start + 1), @"\n[a-z_]+ = ");
        return next.Success ? block.Substring(start, next.Index + 1) : block.Substring(start);
    }

    private static List<string> Quoted(string section) =>
        Regex.Matches(section, "'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
}
