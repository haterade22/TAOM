using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class RunDifficultyTests
{
    [TestMethod]
    public void Id_IsTheLowerCaseWord()
    {
        Assert.AreEqual("normal", RunDifficulties.Id(RunDifficulty.Normal));
        Assert.AreEqual("hard", RunDifficulties.Id(RunDifficulty.Hard));
    }

    [TestMethod]
    public void TryParse_TakesAnyCase()
    {
        Assert.IsTrue(RunDifficulties.TryParse("Hard", out var hard));
        Assert.AreEqual(RunDifficulty.Hard, hard);
        Assert.IsTrue(RunDifficulties.TryParse("NORMAL", out var normal));
        Assert.AreEqual(RunDifficulty.Normal, normal);
    }

    [TestMethod]
    public void TryParse_RefusesAnythingElse()
    {
        Assert.IsFalse(RunDifficulties.TryParse("easy", out _));
        Assert.IsFalse(RunDifficulties.TryParse("", out _));
        Assert.IsFalse(RunDifficulties.TryParse(null, out _));
        Assert.IsFalse(RunDifficulties.TryParse("1", out _));
    }

    [TestMethod]
    public void HardRules_DefaultIsAQuarterMoreAndTwoWavesEarlier()
    {
        Assert.AreEqual(1.25f, HardRules.Default.Factor);
        Assert.AreEqual(2, HardRules.Default.AffixWavesEarlier);
    }

    [TestMethod]
    public void RunOptions_DefaultsToNormal_AndKeepsHardThroughSeeding()
    {
        Assert.AreEqual(RunDifficulty.Normal, RunOptions.Default.Difficulty);
        Assert.AreEqual(RunDifficulty.Hard, (RunOptions.Default with { Difficulty = RunDifficulty.Hard }).Seeded(5L).Difficulty);
    }
}
