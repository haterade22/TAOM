using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The live harness's acceptance rules, pinned without the game (Codex review of plan 042, 2026-10-03): the gate may
/// not pass on matching exception types, on a missing heavy type, on a module that was asked for and is absent, or when
/// the fast path misses the plan's speed bar. XmlMergeLiveEquivalenceTests feeds these rules; they run in the default
/// suite and on hosted CI.
/// </summary>
[TestClass]
public class LiveGateRulesTests
{
    private static LiveTypeOutcome Merged(string id, double engineMs = 100, double fastMs = 10) =>
        new LiveTypeOutcome(id, engineMs, fastMs);

    private static List<LiveTypeOutcome> EveryHeavyType(double engineMs, double fastMs) =>
        LiveGateRules.HeavyTypes.Select(id => Merged(id, engineMs, fastMs)).ToList();

    [TestMethod]
    public void CheckTypes_EveryHeavyTypeMergedWithinTheBar_ReportsNothing()
    {
        // Arrange
        var outcomes = EveryHeavyType(engineMs: 1000, fastMs: 100);
        outcomes.Add(Merged("Monsters"));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckTypes_BothSidesThrowTheSameExceptionType_Fails()
    {
        // Arrange: two different failures that share a type used to read as equivalence.
        var outcomes = EveryHeavyType(1000, 100);
        outcomes.Add(new LiveTypeOutcome("Monsters", 5, 5,
            engineError: new ArgumentException("engine side"), fastError: new ArgumentException("fast side")));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert: the message carries both sides, so the reader sees what the fast path did with the same input.
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "Monsters");
        StringAssert.Contains(failures[0], "the engine threw ArgumentException: engine side");
        StringAssert.Contains(failures[0], "the fast path threw ArgumentException: fast side");
    }

    [TestMethod]
    public void CheckTypes_OnlyTheEngineThrew_Fails()
    {
        // Arrange
        var outcomes = EveryHeavyType(1000, 100);
        outcomes.Add(new LiveTypeOutcome("Monsters", 5, 5, engineError: new InvalidOperationException("boom")));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert: "engine threw, fast merged" is the case where the fast path takes input the engine rejects, so the
        // message names that, and names both causes instead of blaming the harness alone.
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "the engine threw InvalidOperationException: boom");
        StringAssert.Contains(failures[0], "the fast path merged, so it accepts what the engine rejected");
        StringAssert.Contains(failures[0], "the harness's lists differ from the game's, or the fast path diverges");
    }

    [TestMethod]
    public void CheckTypes_TheEngineMergedAndTheFastPathThrew_Fails()
    {
        // Arrange
        var outcomes = EveryHeavyType(1000, 100);
        outcomes.Add(new LiveTypeOutcome("Monsters", 5, 5, fastError: new InvalidOperationException("boom")));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "the engine merged, the fast path threw InvalidOperationException: boom");
    }

    [TestMethod]
    public void CheckTypes_TheDocumentsDiffer_ReportsTheDifferenceText()
    {
        // Arrange
        var outcomes = EveryHeavyType(1000, 100);
        outcomes.Add(new LiveTypeOutcome("Monsters", 5, 5, difference: "Monsters: documents differ; first difference at 7"));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        CollectionAssert.AreEqual(new[] { "Monsters: documents differ; first difference at 7" }, failures.ToList());
    }

    [TestMethod]
    public void CheckTypes_AHeavyTypeThrewOnBothSides_FailsOnceAndWritesNoRatioLine()
    {
        // Arrange: the other three sit above the bar, so a ratio measured over a set with no merge for the fourth
        // would add a second message. The sides throw different types, so the type the message gives the fast path
        // can be told from the engine's.
        var outcomes = EveryHeavyType(engineMs: 1000, fastMs: 900)
            .Where(o => o.Id != "NPCCharacters")
            .ToList();
        outcomes.Add(new LiveTypeOutcome("NPCCharacters", 5, 5,
            engineError: new ArgumentException("a"), fastError: new InvalidOperationException("b")));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "NPCCharacters");
        StringAssert.Contains(failures[0], "the fast path threw InvalidOperationException: b");
    }

    [DataTestMethod]
    [DataRow("NPCCharacters")]
    [DataRow("Items")]
    [DataRow("EquipmentRosters")]
    [DataRow("GameText")]
    public void CheckTypes_AHeavyTypeIsMissing_FailsNamingItOnceAndWritesNoRatioLine(string missing)
    {
        // Arrange: the other three sit above the bar, so a ratio measured without the fourth would add a message.
        var outcomes = EveryHeavyType(engineMs: 1000, fastMs: 900).Where(o => o.Id != missing).ToList();

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "heavy type " + missing + " is not among the merged types");
    }

    [TestMethod]
    public void CheckTypes_NoTypesAtAll_ReportsEveryHeavyTypeMissing()
    {
        // Arrange and Act
        var failures = LiveGateRules.CheckTypes(new List<LiveTypeOutcome>());

        // Assert
        Assert.AreEqual(LiveGateRules.HeavyTypes.Count, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckTypes_FastTotalAboveHalfTheEngineTotal_FailsWithBothTotals()
    {
        // Arrange: 4 x 1000 ms engine, 4 x 600 ms fast.
        var outcomes = EveryHeavyType(engineMs: 1000, fastMs: 600);

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "2400 ms");
        StringAssert.Contains(failures[0], "4000 ms");
        StringAssert.Contains(failures[0], "60.0%");
        StringAssert.Contains(failures[0], "at most 50%");
    }

    [TestMethod]
    public void CheckTypes_FastTotalExactlyHalfTheEngineTotal_Passes()
    {
        // Arrange: the plan's bar is "at most 50%".
        var outcomes = EveryHeavyType(engineMs: 1000, fastMs: 500);

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckTypes_OneHeavyTypeSlowerButTheTotalWithinTheBar_Passes()
    {
        // Arrange: the bar is on the four types' totals, not on each type.
        var outcomes = new List<LiveTypeOutcome>
        {
            Merged("NPCCharacters", engineMs: 8000, fastMs: 500),
            Merged("Items", engineMs: 100, fastMs: 150),
            Merged("EquipmentRosters", engineMs: 100, fastMs: 100),
            Merged("GameText", engineMs: 100, fastMs: 100),
        };

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckTypes_ALightTypeSlowerOnTheFastPath_IsOutsideTheBar()
    {
        // Arrange: a light type's time is not one of the four the bar covers, however slow.
        var outcomes = EveryHeavyType(1000, 100);
        outcomes.Add(Merged("Monsters", engineMs: 1, fastMs: 100000));

        // Act
        var failures = LiveGateRules.CheckTypes(outcomes);

        // Assert
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckModules_NothingMissing_ReportsNothing()
    {
        // Arrange and Act
        var failures = LiveGateRules.CheckModules(new List<string>(), explicitOrder: true);

        // Assert
        Assert.AreEqual(0, failures.Count);
    }

    [TestMethod]
    public void CheckModules_AnExplicitOrderNamesAnAbsentModule_FailsNamingItAndTheVariable()
    {
        // Arrange: a misspelled module used to vanish from the inventory and leave the gate green.
        var missing = new List<string> { "TAOM_Mapp" };

        // Act
        var failures = LiveGateRules.CheckModules(missing, explicitOrder: true);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "TAOM_Mapp");
        StringAssert.Contains(failures[0], LiveMergeListBuilder.ModulesVariable);
    }

    [TestMethod]
    public void CheckModules_AnExplicitOrderNamesTheOptionalModule_StillFails()
    {
        // Arrange: only the default order may lack FastMode; asking for it by name is a request like any other.
        var missing = new List<string> { "FastMode" };

        // Act
        var failures = LiveGateRules.CheckModules(missing, explicitOrder: true);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "FastMode");
    }

    [TestMethod]
    public void CheckModules_TheDefaultOrderLacksOnlyTheOptionalModule_Passes()
    {
        // Arrange: the v1.5.3 install has no FastMode folder.
        var missing = new List<string> { "FastMode" };

        // Act
        var failures = LiveGateRules.CheckModules(missing, explicitOrder: false);

        // Assert
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void CheckModules_TheDefaultOrderLacksARequiredModule_FailsNamingOnlyThatModule()
    {
        // Arrange
        var missing = new List<string> { "FastMode", "TAOM_Map" };

        // Act
        var failures = LiveGateRules.CheckModules(missing, explicitOrder: false);

        // Assert
        Assert.AreEqual(1, failures.Count, string.Join("\n", failures));
        StringAssert.Contains(failures[0], "TAOM_Map");
        Assert.IsFalse(failures[0].Contains("FastMode"), "the optional module is not what is wrong: " + failures[0]);
    }
}
