using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Autorun;

namespace TAOM.Tests.Features.LongDefeat.Autorun;

[TestClass]
public class LongDefeatAutorunPlanTests
{
    private const string ArmPath = @"E:\Temp\long-defeat-runs\20261104-120000\arm.txt";
    private const string Out = "out=E:\\x\n";

    private static LongDefeatAutorunPlan Resolve(string? text, string? env = ArmPath, bool exists = true)
    {
        Func<string, string?> getEnv = name => name == LongDefeatAutorunPlan.EnvVar ? env : null;
        return LongDefeatAutorunPlan.Resolve(getEnv, _ => exists, _ => text ?? throw new InvalidOperationException("read"));
    }

    [TestMethod]
    public void EnvVar_IsItsOwn_NotTheSpikesAndNotFords()
    {
        Assert.AreEqual("TAOM_LONGDEFEAT_AUTORUN", LongDefeatAutorunPlan.EnvVar);
    }

    [TestMethod]
    public void Resolve_EnvVariableUnset_IsUnarmedWithNoProblem()
    {
        var plan = Resolve(Out, env: null);

        Assert.IsFalse(plan.Armed);
        Assert.IsNull(plan.Problem);
    }

    [TestMethod]
    public void Resolve_MissingFile_IsUnarmedAndSaysWhy()
    {
        var plan = Resolve(Out, exists: false);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "missing file");
    }

    [TestMethod]
    public void Resolve_UnreadableFile_IsUnarmedAndNamesTheException()
    {
        var plan = Resolve(null);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "InvalidOperationException");
    }

    [TestMethod]
    public void Resolve_NoOutFolder_OrARelativeOne_IsUnarmed()
    {
        Assert.IsFalse(Resolve("fortress=helms\n").Armed);
        Assert.IsFalse(Resolve("out=relative\\folder\n").Armed);
        Assert.IsFalse(Resolve("out=a<b\n").Armed, "a character Path.IsPathRooted throws on is no folder");
        StringAssert.Contains(Resolve("fortress=helms\n").Problem, "out=");
    }

    [TestMethod]
    public void Resolve_Defaults_MatchThePlan()
    {
        var plan = Resolve(Out);

        Assert.IsTrue(plan.Armed);
        Assert.IsNull(plan.Fortress);
        Assert.IsNull(plan.Waves);
        Assert.AreEqual(1f, plan.GarrisonScale);
        Assert.AreEqual(3600.0, plan.LiveLimitSeconds);
        Assert.AreEqual(3900.0, plan.WallLimitSeconds);
        Assert.IsFalse(plan.QuitWhenDone);
        Assert.AreEqual(0, plan.ScreenshotSeconds.Count);
        Assert.IsNull(plan.Expect);
    }

    [TestMethod]
    public void Resolve_ReadsEveryKey_CaseInsensitively_AndSkipsComments()
    {
        var plan = Resolve("\uFEFF# a comment\nFortress=helms\nwaves=5\nout=E:\\x\nquit=1\nlive_limit_seconds=120\nwall_limit_seconds=300\n"
            + "garrison_scale=0.2\nexpect=lost\nscreenshots=90, 30\n");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual("helms", plan.Fortress);
        Assert.AreEqual(5, plan.Waves);
        Assert.AreEqual(@"E:\x", plan.OutFolder);
        Assert.IsTrue(plan.QuitWhenDone);
        Assert.AreEqual(120.0, plan.LiveLimitSeconds);
        Assert.AreEqual(300.0, plan.WallLimitSeconds);
        Assert.AreEqual(0.2f, plan.GarrisonScale);
        Assert.AreEqual("lost", plan.Expect);
        CollectionAssert.AreEqual(new[] { 30.0, 90.0 }, plan.ScreenshotSeconds.ToList());
        Assert.AreEqual(0, plan.Warnings.Count, string.Join("; ", plan.Warnings));
    }

    [TestMethod]
    public void Resolve_Speed_DefaultsToOne_AndReadsEachLevel()
    {
        Assert.AreEqual(1, Resolve(Out).Speed);
        foreach (var level in new[] { 1, 2, 4, 10 })
        {
            var plan = Resolve(Out + "speed=" + level + "\n");
            Assert.AreEqual(level, plan.Speed);
            Assert.AreEqual(0, plan.Warnings.Count, string.Join("; ", plan.Warnings));
        }
    }

    [TestMethod]
    public void Resolve_Speed_AnythingElseRevertsToOneWithOneWarning()
    {
        foreach (var bad in new[] { "3", "0", "-2", "20", "x", "2.5", "" })
        {
            var plan = Resolve(Out + "speed=" + bad + "\n");
            Assert.AreEqual(1, plan.Speed, bad);
            Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("speed")), bad);
        }
    }

    [TestMethod]
    public void Describe_NamesTheSpeed()
    {
        StringAssert.Contains(Resolve(Out + "speed=4\n").Describe(), " speed=4 ");
    }

    [TestMethod]
    public void Resolve_BadWaves_WarnAndPlayUntilLost()
    {
        foreach (var bad in new[] { "0", "-2", "x", "2.5", "" })
        {
            var plan = Resolve(Out + "waves=" + bad + "\n");
            Assert.IsNull(plan.Waves, bad);
            Assert.IsTrue(plan.Warnings.Any(w => w.Contains("waves")), bad);
        }
    }

    [TestMethod]
    public void Resolve_GarrisonScale_AcceptsOnlyAPositiveFiniteNumber()
    {
        foreach (var bad in new[] { "NaN", "Infinity", "-1", "0", "x", "11" })
        {
            var plan = Resolve(Out + "garrison_scale=" + bad + "\n");
            Assert.AreEqual(1f, plan.GarrisonScale, bad);
            Assert.IsTrue(plan.Warnings.Any(w => w.Contains("garrison_scale")), bad);
        }
    }

    [TestMethod]
    public void Resolve_LimitsAreSeconds_AndNaNInfinityOrZeroFallBack()
    {
        foreach (var bad in new[] { "NaN", "Infinity", "0", "-5", "x" })
        {
            var plan = Resolve(Out + "live_limit_seconds=" + bad + "\nwall_limit_seconds=" + bad + "\n");
            Assert.AreEqual(3600.0, plan.LiveLimitSeconds, bad);
            Assert.AreEqual(3900.0, plan.WallLimitSeconds, bad);
            Assert.AreEqual(2, plan.Warnings.Count(w => w.Contains("not a positive number")), bad);
        }
    }

    [TestMethod]
    public void Resolve_BadFlag_Warns_AndUnknownKeyIsIgnoredWithAWarning()
    {
        var plan = Resolve(Out + "quit=maybe\nbogus=1\nno equals sign\n");

        Assert.IsFalse(plan.QuitWhenDone);
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("quit")));
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("bogus")));
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("without a key")));
    }

    [TestMethod]
    public void Resolve_Screenshots_ABadOneWarns_AndTheRestStay()
    {
        var plan = Resolve(Out + "screenshots=60,nope,NaN\n");

        CollectionAssert.AreEqual(new[] { 60.0 }, plan.ScreenshotSeconds.ToList());
        Assert.AreEqual(2, plan.Warnings.Count(w => w.Contains("screenshots")));
    }

    [TestMethod]
    public void ToSetup_CarriesTheWaveTarget_TheScale_AutoplayAndTheOutFolder()
    {
        var setup = Resolve(Out + "waves=3\ngarrison_scale=0.5\n").ToSetup("helms");

        Assert.AreEqual("helms", setup.FortressId);
        Assert.AreEqual("autorun", setup.Trigger);
        Assert.AreEqual(3, setup.Options.StopAfterWaves);
        Assert.AreEqual(0.5f, setup.Options.GarrisonScale);
        Assert.IsTrue(setup.Options.Autoplay, "an unattended run needs the bot and the AI-driven main agent");
        Assert.AreEqual(@"E:\x", setup.RecordsFolder, "the best run must never land in the player's file");
    }

    [TestMethod]
    public void ArmedInThisProcess_FollowsTheArmVariable_AndABlankOneIsUnarmed()
    {
        var before = Environment.GetEnvironmentVariable(LongDefeatAutorunPlan.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(LongDefeatAutorunPlan.EnvVar, "some/arm.txt");
            Assert.IsTrue(LongDefeatAutorunPlan.ArmedInThisProcess);

            Environment.SetEnvironmentVariable(LongDefeatAutorunPlan.EnvVar, "  ");
            Assert.IsFalse(LongDefeatAutorunPlan.ArmedInThisProcess);

            Environment.SetEnvironmentVariable(LongDefeatAutorunPlan.EnvVar, null);
            Assert.IsFalse(LongDefeatAutorunPlan.ArmedInThisProcess);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LongDefeatAutorunPlan.EnvVar, before);
        }
    }

    [TestMethod]
    public void Describe_NamesTheChoicesTheRunWillUse()
    {
        var text = Resolve(Out + "fortress=helms\nwaves=2\nquit=1\n").Describe();

        StringAssert.Contains(text, "fortress=helms");
        StringAssert.Contains(text, "waves=2");
        StringAssert.Contains(text, "quit=1");
        StringAssert.Contains(text, "out=E:\\x");
    }

    [TestMethod]
    public void Resolve_PhaseTwoKeys_DefaultToABoardRun()
    {
        var plan = Resolve(Out);

        Assert.IsNull(plan.Seed);
        Assert.AreEqual(1, plan.StartWave);
        Assert.AreEqual(0, plan.StartRerolls);
        Assert.IsNull(plan.Class);
    }

    [TestMethod]
    public void Resolve_ReadsSeedStartWaveStartRerollsAndClass()
    {
        var plan = Resolve(Out + "seed=12345\nstart_wave=12\nstart_rerolls=2\nclass=archer\n");

        Assert.AreEqual(12345UL, plan.Seed);
        Assert.AreEqual(12, plan.StartWave);
        Assert.AreEqual(2, plan.StartRerolls);
        Assert.AreEqual("archer", plan.Class);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("0", 0UL)]
    [DataRow("9007199254740991", 9007199254740991UL)]
    public void Resolve_Seed_TakesZeroToTwoToTheFiftyThirdMinusOne(string value, ulong expected)
    {
        Assert.AreEqual(expected, Resolve(Out + "seed=" + value + "\n").Seed);
    }

    [DataTestMethod]
    [DataRow("-1")]
    [DataRow("-0")]
    [DataRow("9007199254740992")]
    [DataRow("9223372036854775807")]
    [DataRow("9223372036854775808")]
    [DataRow("18446744073709551615")]
    [DataRow("99999999999999999999999")]
    [DataRow("1.5")]
    [DataRow("0x10")]
    [DataRow("abc")]
    [DataRow("")]
    public void Resolve_Seed_ANegativeOrOutOfRangeOrNonDecimalValueIsRefusedWithOneWarning(string value)
    {
        var plan = Resolve(Out + "seed=" + value + "\n");

        Assert.IsNull(plan.Seed, "no seed means the start time's ticks");
        Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("seed")));
    }

    [DataTestMethod]
    [DataRow("1", 1)]
    [DataRow("100", 100)]
    public void Resolve_StartWave_TakesOneToOneHundred(string value, int expected)
    {
        Assert.AreEqual(expected, Resolve(Out + "start_wave=" + value + "\n").StartWave);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("101")]
    [DataRow("-3")]
    [DataRow("x")]
    public void Resolve_StartWave_OutOfRangeKeepsWaveOneWithAWarning(string value)
    {
        var plan = Resolve(Out + "start_wave=" + value + "\n");

        Assert.AreEqual(1, plan.StartWave);
        Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("start_wave")));
    }

    [DataTestMethod]
    [DataRow("0", 0)]
    [DataRow("3", 3)]
    public void Resolve_StartRerolls_TakesZeroToThree(string value, int expected)
    {
        Assert.AreEqual(expected, Resolve(Out + "start_rerolls=" + value + "\n").StartRerolls);
    }

    [DataTestMethod]
    [DataRow("4")]
    [DataRow("-1")]
    [DataRow("many")]
    public void Resolve_StartRerolls_OutOfRangeKeepsZeroWithAWarning(string value)
    {
        var plan = Resolve(Out + "start_rerolls=" + value + "\n");

        Assert.AreEqual(0, plan.StartRerolls);
        Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("start_rerolls")));
    }

    [TestMethod]
    public void ToSetup_CarriesTheSeedTheStartWaveAndTheRerolls()
    {
        var options = Resolve(Out + "seed=7\nstart_wave=12\nstart_rerolls=1\n").ToSetup("helms").Options;

        Assert.AreEqual(7UL, options.Seed);
        Assert.AreEqual(12, options.StartWave);
        Assert.AreEqual(1, options.StartRerolls);
    }

    [TestMethod]
    public void ToSetup_CarriesTheClassId_OrNoneWhenTheArmFileNamesNone()
    {
        Assert.AreEqual("archer", Resolve(Out + "class=archer\n").ToSetup("helms").Options.ClassId);
        Assert.IsNull(Resolve(Out).ToSetup("helms").Options.ClassId, "the game's bot then draws one from the seed");
    }

    [TestMethod]
    public void ToSetup_WithNoSeed_LeavesItForTheLauncherToTakeFromTheClock()
    {
        Assert.IsNull(Resolve(Out).ToSetup("helms").Options.Seed);
    }

    [TestMethod]
    public void Resolve_StartGold_DefaultsToNone_SoABoardRunKeepsTheShopsGold()
    {
        var plan = Resolve(Out);

        Assert.IsNull(plan.StartGold);
        Assert.IsNull(plan.ToSetup("helms").Options.StartGold);
        StringAssert.Contains(plan.Describe(), "start_gold=-");
    }

    [DataTestMethod]
    [DataRow("0", 0)]
    [DataRow("5000", 5000)]
    [DataRow("100000", 100000)]
    public void Resolve_StartGold_TakesZeroToOneHundredThousand(string value, int expected)
    {
        var plan = Resolve(Out + "start_gold=" + value + "\n");

        Assert.AreEqual(expected, plan.StartGold);
        Assert.AreEqual(expected, plan.ToSetup("helms").Options.StartGold);
        Assert.AreEqual(0, plan.Warnings.Count);
        StringAssert.Contains(plan.Describe(), "start_gold=" + expected);
    }

    [DataTestMethod]
    [DataRow("100001")]
    [DataRow("-1")]
    [DataRow("lots")]
    [DataRow("")]
    public void Resolve_StartGold_OutOfRangeIsNoneWithAWarning(string value)
    {
        var plan = Resolve(Out + "start_gold=" + value + "\n");

        Assert.IsNull(plan.StartGold);
        Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("start_gold")));
    }

    [TestMethod]
    public void Resolve_BreakEngines_DefaultsToNone_SoNoEngineIsBroken()
    {
        var plan = Resolve(Out);

        Assert.IsNull(plan.BreakEngines);
        Assert.IsNull(plan.ToSetup("helms").Options.BreakEnginesAtClear);
        StringAssert.Contains(plan.Describe(), "break_engines=-");
    }

    [DataTestMethod]
    [DataRow("1", 1)]
    [DataRow("4", 4)]
    [DataRow("100", 100)]
    public void Resolve_BreakEngines_TakesAClearFromOneToOneHundred(string value, int expected)
    {
        var plan = Resolve(Out + "break_engines=" + value + "\n");

        Assert.AreEqual(expected, plan.BreakEngines);
        Assert.AreEqual(expected, plan.ToSetup("helms").Options.BreakEnginesAtClear);
        Assert.AreEqual(0, plan.Warnings.Count);
        StringAssert.Contains(plan.Describe(), "break_engines=" + expected);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("101")]
    [DataRow("-1")]
    [DataRow("once")]
    [DataRow("")]
    public void Resolve_BreakEngines_OutOfRangeIsNoneWithAWarning(string value)
    {
        var plan = Resolve(Out + "break_engines=" + value + "\n");

        Assert.IsNull(plan.BreakEngines);
        Assert.AreEqual(1, plan.Warnings.Count(w => w.Contains("break_engines")));
    }

    [TestMethod]
    public void Describe_NamesTheSeedStartWaveAndRerolls()
    {
        var text = Resolve(Out + "seed=7\nstart_wave=3\nstart_rerolls=1\nclass=archer\n").Describe();

        StringAssert.Contains(text, "seed=7");
        StringAssert.Contains(text, "start_wave=3");
        StringAssert.Contains(text, "start_rerolls=1");
        StringAssert.Contains(text, "class=archer");
    }
}
