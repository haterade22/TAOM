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
/// is a HEURISTIC, not proof: the engine keys on `_saveBaseId + saveId`, so two definers sharing a
/// base id coexist fine when their per-type offsets differ. Reading the real ids would mean invoking
/// DefineClassTypes() on third-party definers against a synthetic DefinitionContext, which is not
/// safe to do speculatively — so the heuristic stays, but it is reported as a lead rather than a
/// verdict, and groups made up entirely of game-shipped assemblies are dropped (see below).
/// </summary>
[TestClass]
public class SaveDefinerCollisionDetectorTests
{
    private readonly SaveDefinerCollisionDetector _sut = new();

    private static SaveDefinerRecord Record(string assembly, string type, int baseId) =>
        new(assembly, type, baseId);

    // --- Engine-only groups are NOT collisions (false positive, 2026-08-01) --------------------
    //
    // Base-id equality is a HEURISTIC, not proof. The real save id is `_saveBaseId + saveId`
    // (SaveableTypeDefiner.AddClassDefinition, v1.4.7), so two definers can share a base id and
    // never collide as long as their offsets differ — and vanilla does exactly that:
    // SaveableCoreTypeDefiner (TaleWorlds.Core) and SaveableObjectSystemTypeDefiner
    // (TaleWorlds.ObjectSystem) both use base id 10000, in a game that starts fine.
    //
    // Because they are in different assemblies, the old detector took the cross-assembly branch and
    // told players "Two mods claim the same save-system id range... Disable one of them", naming two
    // vanilla engine types. It sat at the top of every user log we collected. A diagnostic that
    // cries wolf is worse than no diagnostic: it discredits every other line the guard emits.
    //
    // Rule: only report a group that contains at least one NON-engine assembly, because that is the
    // only thing a player can act on.

    [TestMethod]
    public void Detect_TwoVanillaDefinersSharingBaseId_ReportsNothing()
    {
        // The exact real-world pair, with the real base id.
        var result = _sut.Detect(new List<SaveDefinerRecord>
        {
            Record("TaleWorlds.Core", "TaleWorlds.Core.SaveableCoreTypeDefiner", 10000),
            Record("TaleWorlds.ObjectSystem", "TaleWorlds.ObjectSystem.SaveableObjectSystemTypeDefiner", 10000),
        });

        Assert.AreEqual(0, result.Count,
            "two vanilla definers sharing a base id is legal and must never be reported");
    }

    [TestMethod]
    public void Detect_ModSharingBaseIdWithVanilla_IsStillReported()
    {
        // Actionable: the player can disable the mod.
        var result = _sut.Detect(new List<SaveDefinerRecord>
        {
            Record("TaleWorlds.Core", "TaleWorlds.Core.SaveableCoreTypeDefiner", 10000),
            Record("SomeMod", "SomeMod.Definer", 10000),
        });

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void Detect_TwoModsSharingBaseId_IsStillReported()
    {
        var result = _sut.Detect(new List<SaveDefinerRecord>
        {
            Record("TAOM", "TAOM.FormationPresetSaveableTypeDefiner", 726900601),
            Record("CompanionTactics", "CompanionTactics.Definer", 726900601),
        });

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].IsCrossAssembly);
    }

    [TestMethod]
    public void Detect_SandBoxModulesCountAsEngine_NotPlayerActionable()
    {
        // SandBox / StoryMode ship with the game; a player cannot disable them either.
        var result = _sut.Detect(new List<SaveDefinerRecord>
        {
            Record("SandBox", "SandBox.Definer", 55000),
            Record("StoryMode", "StoryMode.Definer", 55000),
        });

        Assert.AreEqual(0, result.Count);
    }

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
