using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the save-definer base-id collision detector.
///
/// Why this exists: the engine's DefinitionContext instantiates every non-abstract
/// SaveableTypeDefiner across all loaded assemblies and registers each definition into a plain
/// Dictionary keyed by save id. An exact collision throws during Module.Initialize — before any
/// campaign exists — with a message that names neither mod. TAOM is unusually exposed here because
/// FormationPresetSaveableTypeDefiner DELIBERATELY reuses an upstream mod's base id (726900601) so
/// existing CompanionTactics saves import, which makes "enable the donor mod alongside TAOM" a
/// guaranteed, unattributable startup crash.
///
/// The detector groups by base id and reports before the engine crashes on it. Base-id granularity
/// is the correct level for a warning: reading class ids would require invoking DefineClassTypes()
/// on third-party definers, which is not safe to do speculatively.
/// </summary>
[TestClass]
public class SaveDefinerCollisionDetectorTests
{
    private readonly SaveDefinerCollisionDetector _sut = new();

    private static SaveDefinerRecord Record(string assembly, string type, int baseId) =>
        new(assembly, type, baseId);

    [TestMethod]
    public void Detect_EmptyInput_ReturnsNoCollisions()
    {
        var result = _sut.Detect(new List<SaveDefinerRecord>());

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_NullInput_ReturnsNoCollisions()
    {
        var result = _sut.Detect(null);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_DistinctBaseIds_ReturnsNoCollisions()
    {
        var result = _sut.Detect(new[]
        {
            Record("TAOM", "PresetSaveableTypeDefiner", 726900501),
            Record("SomeMod", "TheirDefiner", 900000001),
        });

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_SameBaseIdAcrossTwoAssemblies_ReturnsOneCollisionNamingBoth()
    {
        var result = _sut.Detect(new[]
        {
            Record("TAOM", "FormationPresetSaveableTypeDefiner", 726900601),
            Record("CompanionTactics", "FormationPresetSaveableTypeDefiner", 726900601),
        });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(726900601, result[0].BaseId);
        Assert.IsTrue(result[0].IsCrossAssembly);
        CollectionAssert.AreEquivalent(
            new[] { "TAOM", "CompanionTactics" },
            result[0].Records.Select(r => r.AssemblyName).ToArray());
    }

    [TestMethod]
    public void Detect_SameBaseIdTwiceWithinOneAssembly_IsFlaggedAsInternalDefect()
    {
        // A TAOM-vs-TAOM duplicate is our own bug (copy-pasted definer), not a mod conflict, and
        // the user-facing message must say so rather than blaming another mod.
        var result = _sut.Detect(new[]
        {
            Record("TAOM", "DefinerA", 726900701),
            Record("TAOM", "DefinerB", 726900701),
        });

        Assert.AreEqual(1, result.Count);
        Assert.IsFalse(result[0].IsCrossAssembly);
    }

    [TestMethod]
    public void Detect_SameTypeReportedTwice_IsNotACollision()
    {
        // Assembly enumeration can surface the same definer twice (e.g. a type loaded through two
        // load contexts). One definer is not a conflict with itself.
        var result = _sut.Detect(new[]
        {
            Record("TAOM", "PresetSaveableTypeDefiner", 726900501),
            Record("TAOM", "PresetSaveableTypeDefiner", 726900501),
        });

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Detect_MultipleCollisions_ReturnsOnePerBaseId()
    {
        var result = _sut.Detect(new[]
        {
            Record("TAOM", "A", 1), Record("Other", "B", 1),
            Record("TAOM", "C", 2), Record("Other", "D", 2),
        });

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, result.Select(c => c.BaseId).ToArray());
    }

    // No "TAOM's own base ids are distinct" test here on purpose. An earlier draft asserted
    // distinctness over a hardcoded literal list, which is a tautology: adding a fifth definer that
    // collides would leave it green because nothing coupled the list to the real subclasses.
    // `PresetSaveableTypeDefinerTests.BaseId_UniqueAcrossDiscoverableDefinersInTaomAssembly` already
    // reflects over the TAOM assembly and is the check that actually catches that drift.
}
