using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Autorun;

namespace TAOM.Tests.Features.FordsOfWar.Autorun;

[TestClass]
public class FordsAutorunPlanTests
{
    private const string ArmPath = @"E:\Temp\fords-runs\20261031-120000\arm.txt";

    private static FordsAutorunPlan Resolve(string? text, string? env = ArmPath, bool exists = true)
    {
        Func<string, string?> getEnv = name => name == FordsAutorunPlan.EnvVar ? env : null;
        return FordsAutorunPlan.Resolve(getEnv, _ => exists, _ => text ?? throw new InvalidOperationException("read"));
    }

    [TestMethod]
    public void Screenshots_AreReadInRisingOrder_AndABadOneWarns()
    {
        var plan = Resolve("out=E:\\x\nscreenshots=90, 30,nope");

        CollectionAssert.AreEqual(new[] { 30.0, 90.0 }, plan.ScreenshotSeconds.ToList());
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("nope")));
        Assert.AreEqual(0, Resolve("out=E:\\x").ScreenshotSeconds.Count);
    }

    [TestMethod]
    public void ArmedInThisProcess_FollowsTheArmVariable_AndABlankOneIsUnarmed()
    {
        var before = Environment.GetEnvironmentVariable(FordsAutorunPlan.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(FordsAutorunPlan.EnvVar, "some/arm.txt");
            Assert.IsTrue(FordsAutorunPlan.ArmedInThisProcess);

            Environment.SetEnvironmentVariable(FordsAutorunPlan.EnvVar, "  ");
            Assert.IsFalse(FordsAutorunPlan.ArmedInThisProcess);

            Environment.SetEnvironmentVariable(FordsAutorunPlan.EnvVar, null);
            Assert.IsFalse(FordsAutorunPlan.ArmedInThisProcess);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FordsAutorunPlan.EnvVar, before);
        }
    }

    [TestMethod]
    public void Resolve_EnvVariableUnset_IsUnarmedWithNoProblem()
    {
        var plan = Resolve("out=E:\\x", env: null);

        Assert.IsFalse(plan.Armed);
        Assert.IsNull(plan.Problem);
    }

    [TestMethod]
    public void Resolve_WestAiKey_PutsAnAiOnTheWestSide_AndItIsOffByDefault()
    {
        Assert.IsTrue(Resolve("out=E:\\x\nwest_ai=1").WestAi);
        Assert.IsTrue(Resolve("out=E:\\x\nwest_ai=true").WestAi);
        Assert.IsFalse(Resolve("out=E:\\x").WestAi);
        var bad = Resolve("out=E:\\x\nwest_ai=maybe");
        Assert.IsFalse(bad.WestAi);
        Assert.IsTrue(bad.Warnings.Any(w => w.Contains("west_ai")));
        StringAssert.Contains(Resolve("out=E:\\x\nwest_ai=1").Describe(), "west_ai=1");
    }

    [TestMethod]
    public void Resolve_EnvVariableBlank_IsUnarmedWithNoProblem()
    {
        var plan = Resolve("out=E:\\x", env: "   ");

        Assert.IsFalse(plan.Armed);
        Assert.IsNull(plan.Problem);
    }

    [TestMethod]
    public void Resolve_VariablePointsAtMissingFile_ReportsTheProblemAndStaysUnarmed()
    {
        var plan = Resolve("out=E:\\x", exists: false);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "TAOM_FORDS_AUTORUN points to a missing file");
        StringAssert.Contains(plan.Problem, ArmPath);
    }

    [TestMethod]
    public void Resolve_FileCannotBeRead_ReportsTheProblemAndStaysUnarmed()
    {
        var plan = Resolve(null);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "could not be read");
    }

    [TestMethod]
    public void Resolve_FullFile_ParsesEveryKey()
    {
        var plan = Resolve("west=gondor\neast=mordor\nai=hard\nscene=scene_a\nlive_seconds=120\nlive_wait_seconds=300\n"
            + "wall_limit_seconds=900\nout=E:\\Temp\\fords-runs\\r1\nquit=1\n");

        Assert.IsTrue(plan.Armed);
        Assert.IsNull(plan.Problem);
        Assert.AreEqual(0, plan.Warnings.Count);
        Assert.AreEqual(ArmPath, plan.ArmPath);
        Assert.AreEqual("gondor", plan.West);
        Assert.AreEqual("mordor", plan.East);
        Assert.AreEqual("hard", plan.Ai);
        Assert.AreEqual("scene_a", plan.Scene);
        Assert.AreEqual(120.0, plan.LiveSeconds);
        Assert.AreEqual(300.0, plan.LiveWaitSeconds);
        Assert.AreEqual(900.0, plan.WallLimitSeconds);
        Assert.AreEqual(@"E:\Temp\fords-runs\r1", plan.OutFolder);
        Assert.IsTrue(plan.QuitWhenDone);
    }

    [TestMethod]
    public void Resolve_OnlyOut_UsesTheDocumentedDefaults()
    {
        var plan = Resolve("out=E:\\x");

        Assert.IsTrue(plan.Armed);
        Assert.IsNull(plan.West);
        Assert.IsNull(plan.East);
        Assert.IsNull(plan.Ai);
        Assert.IsNull(plan.Scene);
        Assert.AreEqual(600.0, plan.LiveSeconds);
        Assert.AreEqual(600.0, plan.LiveWaitSeconds);
        Assert.AreEqual(2400.0, plan.WallLimitSeconds);
        Assert.IsFalse(plan.QuitWhenDone);
    }

    [TestMethod]
    public void Resolve_CrlfAndLfLineEnds_ParseTheSame()
    {
        var crlf = Resolve("west=gondor\r\neast=mordor\r\nout=E:\\x\r\nquit=1\r\n");
        var lf = Resolve("west=gondor\neast=mordor\nout=E:\\x\nquit=1\n");

        Assert.AreEqual(lf.West, crlf.West);
        Assert.AreEqual("mordor", crlf.East);
        Assert.AreEqual(@"E:\x", crlf.OutFolder);
        Assert.IsTrue(crlf.QuitWhenDone);
        Assert.AreEqual(0, crlf.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_LeadingByteOrderMark_IsIgnored()
    {
        var plan = Resolve("\uFEFFwest=gondor\nout=E:\\x\n");

        Assert.AreEqual("gondor", plan.West);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [TestMethod]
    public void Resolve_BlankLinesCommentsAndPadding_AreTolerated()
    {
        var plan = Resolve("# a comment\n\n  WEST = gondor  \nOut=E:\\x\n");

        Assert.AreEqual("gondor", plan.West);
        Assert.AreEqual(@"E:\x", plan.OutFolder);
        Assert.AreEqual(0, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("live_seconds=abc")]
    [DataRow("live_seconds=0")]
    [DataRow("live_seconds=-5")]
    [DataRow("live_seconds=NaN")]
    public void Resolve_BadNumber_WarnsAndKeepsTheDefault(string line)
    {
        var plan = Resolve(line + "\nout=E:\\x");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(600.0, plan.LiveSeconds);
        Assert.AreEqual(1, plan.Warnings.Count);
        StringAssert.Contains(plan.Warnings[0], "live_seconds");
    }

    [TestMethod]
    public void Resolve_NumbersParseWithTheInvariantCulture()
    {
        var plan = Resolve("live_seconds=12.5\nout=E:\\x");

        Assert.AreEqual(12.5, plan.LiveSeconds);
    }

    [TestMethod]
    public void Resolve_UnknownKeyAndMalformedLine_WarnAndStayArmed()
    {
        var plan = Resolve("colour=red\nnot a pair\nout=E:\\x");

        Assert.IsTrue(plan.Armed);
        Assert.AreEqual(2, plan.Warnings.Count);
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("colour")));
    }

    [TestMethod]
    public void Resolve_BadQuitValue_WarnsAndDoesNotQuit()
    {
        var plan = Resolve("quit=maybe\nout=E:\\x");

        Assert.IsFalse(plan.QuitWhenDone);
        Assert.AreEqual(1, plan.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("west=gondor")]
    [DataRow("out=")]
    [DataRow("out=relative\\folder")]
    public void Resolve_MissingOrRelativeOut_ReportsAProblemAndStaysUnarmed(string text)
    {
        var plan = Resolve(text);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "out=");
    }

    [TestMethod]
    public void Describe_NamesTheChoicesTheRunWillUse()
    {
        var plan = Resolve("west=gondor\nlive_seconds=90\nout=E:\\x");

        var text = plan.Describe();

        StringAssert.Contains(text, "west=gondor");
        StringAssert.Contains(text, "live_seconds=90");
        StringAssert.Contains(text, @"out=E:\x");
    }

    [DataTestMethod]
    [DataRow("out=\"E:\\x\"")]
    [DataRow("out=E:\\x|y")]
    [DataRow("out=E:\\<x>")]
    public void Resolve_OutWithInvalidPathCharacters_ReportsAProblemInsteadOfThrowing(string text)
    {
        var plan = Resolve(text);

        Assert.IsFalse(plan.Armed);
        StringAssert.Contains(plan.Problem, "out=");
    }

    [TestMethod]
    public void Describe_AnArmedPlan_NamesTheArmFile()
    {
        var plan = Resolve("out=E:\\x");

        StringAssert.Contains(plan.Describe(), "arm=" + ArmPath);
    }

    [DataTestMethod]
    [DataRow("live_seconds=600\nwall_limit_seconds=600")]
    [DataRow("live_seconds=900\nwall_limit_seconds=300")]
    [DataRow("live_seconds=3000")]
    public void Resolve_LiveSecondsNotBelowTheWallLimit_WarnsButStaysArmed(string lines)
    {
        var plan = Resolve(lines + "\nout=E:\\x");

        Assert.IsTrue(plan.Armed);
        Assert.IsTrue(plan.Warnings.Any(w => w.Contains("wall_limit_seconds") && w.Contains("live_seconds")), string.Join("; ", plan.Warnings));
    }

    [TestMethod]
    public void Resolve_LiveSecondsBelowTheWallLimit_AddsNoWarning()
    {
        var plan = Resolve("live_seconds=300\nwall_limit_seconds=900\nout=E:\\x");

        Assert.AreEqual(0, plan.Warnings.Count);
    }
}
