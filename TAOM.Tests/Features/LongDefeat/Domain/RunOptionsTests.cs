using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class RunOptionsTests
{
    [TestMethod]
    public void Defaults_AreABoardRun()
    {
        var options = RunOptions.Default;

        Assert.IsNull(options.Seed);
        Assert.AreEqual(1, options.StartWave);
        Assert.AreEqual(0, options.StartRerolls);
    }

    [TestMethod]
    public void Seeded_WithNoSeed_TakesTheStartTimesTicks()
    {
        Assert.AreEqual(638000000000000000UL & RunOptions.MaxSeed, RunOptions.Default.Seeded(638000000000000000L).Seed);
    }

    [TestMethod]
    public void Seeded_KeepsAGivenSeed_EvenZero()
    {
        Assert.AreEqual(0UL, new RunOptions { Seed = 0UL }.Seeded(638000000000000000L).Seed);
        Assert.AreEqual(42UL, new RunOptions { Seed = 42UL }.Seeded(638000000000000000L).Seed);
    }

    [TestMethod]
    public void Seeded_KeepsTheOtherOptions()
    {
        var seeded = new RunOptions { StartWave = 12, StopAfterWaves = 4, StartRerolls = 2 }.Seeded(5L);

        Assert.AreEqual(12, seeded.StartWave);
        Assert.AreEqual(4, seeded.StopAfterWaves);
        Assert.AreEqual(2, seeded.StartRerolls);
        Assert.AreEqual(5UL, seeded.Seed);
    }

    [TestMethod]
    public void Seeded_ANegativeClockStaysInTheSeedRange()
    {
        Assert.IsTrue(RunOptions.Default.Seeded(-1L).Seed <= RunOptions.MaxSeed);
    }

    [TestMethod]
    public void MaxSeed_IsTwoToTheFiftyThirdMinusOne_SoAJavaScriptReaderKeepsEveryDigit()
    {
        Assert.AreEqual(9007199254740991UL, RunOptions.MaxSeed);
    }

    [TestMethod]
    public void Seeded_ALargeClockIsMaskedIntoTheCap_AndALowOneIsKept()
    {
        Assert.AreEqual(RunOptions.MaxSeed, RunOptions.Default.Seeded(long.MaxValue).Seed);
        Assert.AreEqual(638000000000000000UL & RunOptions.MaxSeed, RunOptions.Default.Seeded(638000000000000000L).Seed);
        Assert.AreEqual(12345UL, RunOptions.Default.Seeded(12345L).Seed);
    }
}
