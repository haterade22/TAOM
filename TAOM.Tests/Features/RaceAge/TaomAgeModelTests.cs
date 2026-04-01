using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Tests.Features.RaceAge;

[TestClass]
public class TaomAgeModelTests
{
    [TestMethod]
    public void ApplyRaceAgeLimits_DwarfComesOfAgeExceedsLocationMax_ClampsMinToMax()
    {
        // Arrange — exact tavern wench crash: min=20, max=28, dwarf comesOfAge=30
        int min = 20, max = 28;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 30, raceMax: 250, additionalTags: "");

        // Assert
        Assert.AreEqual(28, min);
        Assert.AreEqual(28, max);
    }

    [TestMethod]
    public void ApplyRaceAgeLimits_NormalAdultRange_KeepsBothValues()
    {
        // Arrange — human adult NPC, no clamping needed
        int min = 20, max = 40;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 18, raceMax: 200, additionalTags: "");

        // Assert
        Assert.AreEqual(20, min);
        Assert.AreEqual(40, max);
    }

    [TestMethod]
    public void ApplyRaceAgeLimits_MaxExceedsRaceMax_ClampsMax()
    {
        // Arrange
        int min = 20, max = 100;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 18, raceMax: 60, additionalTags: "");

        // Assert
        Assert.AreEqual(20, min);
        Assert.AreEqual(60, max);
    }

    [TestMethod]
    public void ApplyRaceAgeLimits_ChildTag_SkipsComesOfAgeEnforcement()
    {
        // Arrange — child NPC, comesOfAge should not raise minimum
        int min = 8, max = 15;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 30, raceMax: 250, additionalTags: "Child");

        // Assert — min stays at 8, not raised to 30
        Assert.AreEqual(8, min);
        Assert.AreEqual(15, max);
    }

    [TestMethod]
    public void ApplyRaceAgeLimits_OrcLowComesOfAge_NoClampNeeded()
    {
        // Arrange — orc comesOfAge=12, well below any location ceiling
        int min = 20, max = 40;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 12, raceMax: 60, additionalTags: "");

        // Assert — no change
        Assert.AreEqual(20, min);
        Assert.AreEqual(40, max);
    }

    [TestMethod]
    public void ApplyRaceAgeLimits_BothClampsApply_MinNeverExceedsMax()
    {
        // Arrange — hypothetical race: raceMax=25 AND comesOfAge=30
        // max gets clamped to 25, then min gets raised to 30 → guard clamps min back to 25
        int min = 20, max = 40;

        // Act
        TaomAgeModel.ApplyRaceAgeLimits(ref min, ref max, raceComesOfAge: 30, raceMax: 25, additionalTags: "");

        // Assert — both clamped, min == max == 25
        Assert.AreEqual(25, min);
        Assert.AreEqual(25, max);
    }
}
