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
}
