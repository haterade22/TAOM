using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Spike;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatSpikeOptionsTests
{
    private const string Path = @"E:\Temp\long-defeat-runs\20261103-120000\arm.txt";

    private static LongDefeatSpikeOptions? Parse(params string[] lines) =>
        LongDefeatSpikeOptions.Parse(Path, _ => true, _ => lines, Substitute.For<IModLogger>());

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Parse_VariableUnsetOrBlank_IsUnarmedAndReadsNothing(string? path)
    {
        var options = LongDefeatSpikeOptions.Parse(path, _ => throw new InvalidOperationException("exists"), _ => throw new InvalidOperationException("read"), Substitute.For<IModLogger>());

        Assert.IsNull(options);
    }

    [TestMethod]
    public void Parse_NoFile_IsUnarmedAndWarns()
    {
        var logger = Substitute.For<IModLogger>();

        Assert.IsNull(LongDefeatSpikeOptions.Parse(Path, _ => false, _ => throw new InvalidOperationException("read"), logger));

        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("no file")));
    }

    [TestMethod]
    public void Parse_FileCannotBeRead_IsUnarmedAndWarns()
    {
        var logger = Substitute.For<IModLogger>();

        Assert.IsNull(LongDefeatSpikeOptions.Parse(Path, _ => true, _ => throw new IOException("locked"), logger));

        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("locked")));
    }

    [TestMethod]
    public void Parse_EveryKey_IsRead()
    {
        var options = Parse("fortress=minas", "waves=5", "wave_size=150", "garrison=80", "intermission_seconds=20.5", "live_limit_seconds=600",
            "defenders_ai=0", "quit=1", @"out=E:\Temp\x")!;

        Assert.AreEqual("minas", options.Fortress);
        Assert.AreEqual(5, options.Waves);
        Assert.AreEqual(150, options.WaveSize);
        Assert.AreEqual(80, options.Garrison);
        Assert.AreEqual(20.5f, options.IntermissionSeconds);
        Assert.AreEqual(600f, options.LiveLimitSeconds);
        Assert.IsFalse(options.DefendersAi);
        Assert.IsTrue(options.Quit);
        Assert.AreEqual(@"E:\Temp\x", options.OutFolder);
    }

    [DataTestMethod]
    [DataRow("waves=0")]
    [DataRow("waves=-3")]
    [DataRow("waves=lots")]
    [DataRow("wave_size=0")]
    [DataRow("garrison=NaN")]
    [DataRow("intermission_seconds=NaN")]
    [DataRow("intermission_seconds=Infinity")]
    [DataRow("live_limit_seconds=-1")]
    public void Parse_ABadValue_KeepsTheDefault(string line)
    {
        var options = Parse(line)!;
        var defaults = new LongDefeatSpikeOptions();

        Assert.AreEqual(defaults.Waves, options.Waves);
        Assert.AreEqual(defaults.WaveSize, options.WaveSize);
        Assert.AreEqual(defaults.Garrison, options.Garrison);
        Assert.AreEqual(defaults.IntermissionSeconds, options.IntermissionSeconds);
        Assert.AreEqual(defaults.LiveLimitSeconds, options.LiveLimitSeconds);
    }

    [TestMethod]
    public void Parse_CommentsBlanksAndPadding_AreHandled()
    {
        var options = Parse("# a comment", "", "  waves = 7  ", "no equals sign", "=2")!;

        Assert.AreEqual(7, options.Waves);
    }

    [TestMethod]
    public void Defaults_AreLargeEnoughThatTheAttackersUseTheirEngines()
    {
        Assert.IsTrue(new LongDefeatSpikeOptions().WaveSize > 100, "TacticBreachWalls stops using machines at 100 attackers or fewer");
    }
}
