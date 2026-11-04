using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's arm file (build-spec section 12): the environment variable names a UTF-8 key=value file, and nothing else arms it.</summary>
[TestClass]
public class CommandTrialOptionsTests
{
    private const string ArmPath = @"E:\Temp\command-trial-runs\20261104-100000\arm.txt";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private CommandTrialOptions? Parse(params string[] lines) => CommandTrialOptions.Parse(ArmPath, _ => true, _ => lines, _logger);

    [TestMethod]
    public void TheVariable_IsTaomCommandTrial()
    {
        Assert.AreEqual("TAOM_COMMAND_TRIAL", CommandTrialOptions.EnvVar);
    }

    [TestMethod]
    public void Parse_NoPathOrABlankOne_IsNotArmed_AndSaysNothing()
    {
        foreach (var path in new string?[] { null, "", "   " })
            Assert.IsNull(CommandTrialOptions.Parse(path, _ => true, _ => new[] { "out=E:/x" }, _logger));

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Parse_AMissingFile_IsNotArmed_AndWarnsWithThePath()
    {
        var result = CommandTrialOptions.Parse(ArmPath, _ => false, _ => throw new InvalidOperationException("must not read"), _logger);

        Assert.IsNull(result);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains(ArmPath)));
    }

    [TestMethod]
    public void Parse_AnUnreadableFile_IsNotArmed_AndWarns()
    {
        var result = CommandTrialOptions.Parse(ArmPath, _ => true, _ => throw new IOException("locked"), _logger);

        Assert.IsNull(result);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("locked")));
    }

    [TestMethod]
    public void Parse_OnlyAnOutFolder_TakesTheContractsDefaults()
    {
        var options = Parse("out=E:/Temp/command-trial-runs/x");

        Assert.IsNotNull(options);
        Assert.AreEqual("E:/Temp/command-trial-runs/x", options!.OutFolder);
        Assert.AreEqual("battle_terrain_a", options.Scene);
        Assert.AreEqual(420f, options.LiveLimitSeconds);
        Assert.IsNull(options.Steps, "no steps key means every step");
        Assert.IsFalse(options.Quit);
    }

    [TestMethod]
    public void Parse_EveryKey_IsRead_TrimmedAndCaseInsensitive()
    {
        var options = Parse("# a comment", "", "  OUT = E:/out ", "Quit=1", "scene = battle_terrain_b", "live_limit_seconds=90.5",
            "steps= enter, fly ", "unknown_key=1", "novalue", "=nothing");

        Assert.IsNotNull(options);
        Assert.AreEqual("E:/out", options!.OutFolder);
        Assert.IsTrue(options.Quit);
        Assert.AreEqual("battle_terrain_b", options.Scene);
        Assert.AreEqual(90.5f, options.LiveLimitSeconds);
        Assert.AreEqual("enter, fly", options.Steps);
    }

    [TestMethod]
    public void Parse_ABlankSceneOrStepList_KeepsTheDefaultScene_AndMeansEveryStep()
    {
        var options = Parse("out=E:/x", "scene=", "steps=   ");

        Assert.AreEqual("battle_terrain_a", options!.Scene);
        Assert.IsNull(options.Steps);
    }

    [TestMethod]
    public void Parse_AQuitOtherThanOne_IsNotAQuit()
    {
        Assert.IsFalse(Parse("out=E:/x", "quit=true")!.Quit);
        Assert.IsFalse(Parse("out=E:/x", "quit=0")!.Quit);
    }

    [TestMethod]
    public void Parse_ALiveLimitThatIsNotAPositiveNumber_KeepsTheDefault()
    {
        foreach (var value in new[] { "0", "-5", "abc", "", "100000", "NaN" })
            Assert.AreEqual(420f, Parse("out=E:/x", "live_limit_seconds=" + value)!.LiveLimitSeconds, value);
    }

    [TestMethod]
    public void Parse_TheLiveLimit_IsReadWithTheInvariantCulture()
    {
        var keep = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual(90.5f, Parse("out=E:/x", "live_limit_seconds=90.5")!.LiveLimitSeconds);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = keep;
        }
    }

    [TestMethod]
    public void Parse_NoOutFolder_IsNotArmed_BecauseTheGameCannotWriteItsDoneFile()
    {
        Assert.IsNull(Parse("quit=1", "scene=battle_terrain_a"));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("out")));
    }

    [TestMethod]
    public void Parse_ARelativeOutFolder_IsNotArmed()
    {
        Assert.IsNull(Parse("out=runs/today"));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("absolute")));
    }

    [TestMethod]
    public void ToString_NamesEveryChoice_ForTheArmedLogLine()
    {
        var text = Parse("out=E:/out", "steps=enter", "quit=1")!.ToString();

        StringAssert.Contains(text, "steps=enter");
        StringAssert.Contains(text, "quit=True");
        StringAssert.Contains(text, "out=E:/out");
        StringAssert.Contains(text, "scene=battle_terrain_a");
    }
}
