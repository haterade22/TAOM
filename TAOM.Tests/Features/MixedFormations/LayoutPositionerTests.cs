using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.MixedFormations;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

[TestClass]
public class LayoutPositionerTests
{
    private LayoutPositioner _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new LayoutPositioner();

    private static IFormationAdapter MakeFormation(int meleeCount, int rangedCount, float width = 10f, float interval = 1f)
    {
        var formation = Substitute.For<IFormationAdapter>();
        var units = new List<FormationUnit>();
        for (var i = 0; i < meleeCount; i++) units.Add(new FormationUnit(i, isRanged: false));
        for (var i = 0; i < rangedCount; i++) units.Add(new FormationUnit(meleeCount + i, isRanged: true));

        formation.CountOfUnits.Returns(meleeCount + rangedCount);
        formation.Width.Returns(width);
        formation.Interval.Returns(interval);
        formation.Units.Returns(units);
        return formation;
    }

    // -------- BuildInitialAssignment --------

    [TestMethod]
    public void BuildInitialAssignment_InfantryFront_PutsAllMeleeBeforeRanged()
    {
        var f = MakeFormation(meleeCount: 12, rangedCount: 8, width: 6f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.InfantryFrontRangedBack);

        Assert.AreEqual(20, asn.ByAgentIndex.Count);
        // Melee occupy rows 0..1 (12 units / 3 per row = 4 rows? — depends on filesPerRow=3 from width=6 / interval=2)
        // Verify all 12 melee have row < ranged minimum row.
        var melee = new List<(int row, int file)>();
        var ranged = new List<(int row, int file)>();
        for (var i = 0; i < 12; i++) melee.Add(asn.ByAgentIndex[i]);
        for (var i = 12; i < 20; i++) ranged.Add(asn.ByAgentIndex[i]);

        var maxMeleeRow = 0;
        foreach (var slot in melee) if (slot.row > maxMeleeRow) maxMeleeRow = slot.row;
        var minRangedRow = int.MaxValue;
        foreach (var slot in ranged) if (slot.row < minRangedRow) minRangedRow = slot.row;

        Assert.IsTrue(minRangedRow > maxMeleeRow,
            $"Ranged should start AFTER melee. Max melee row={maxMeleeRow}, min ranged row={minRangedRow}");
    }

    [TestMethod]
    public void BuildInitialAssignment_RangedFront_PutsAllRangedBeforeMelee()
    {
        var f = MakeFormation(meleeCount: 8, rangedCount: 6, width: 6f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.RangedFrontInfantryBack);

        var melee = new List<(int row, int file)>();
        var ranged = new List<(int row, int file)>();
        for (var i = 0; i < 8; i++) melee.Add(asn.ByAgentIndex[i]);
        for (var i = 8; i < 14; i++) ranged.Add(asn.ByAgentIndex[i]);

        var maxRangedRow = 0;
        foreach (var slot in ranged) if (slot.row > maxRangedRow) maxRangedRow = slot.row;
        var minMeleeRow = int.MaxValue;
        foreach (var slot in melee) if (slot.row < minMeleeRow) minMeleeRow = slot.row;

        Assert.IsTrue(minMeleeRow > maxRangedRow,
            $"Melee should start AFTER ranged. Max ranged row={maxRangedRow}, min melee row={minMeleeRow}");
    }

    [TestMethod]
    public void BuildInitialAssignment_Wings_RangedAreOnLeftAndRightOfCenter()
    {
        var f = MakeFormation(meleeCount: 12, rangedCount: 6, width: 12f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.RangedWingsInfantryCenter);

        // First melee unit (index 0) should be at file 0 or near center (negative or zero file).
        // Ranged units should be at extreme negative or extreme positive files.
        var meleeFiles = new List<int>();
        var rangedFiles = new List<int>();
        for (var i = 0; i < 12; i++) meleeFiles.Add(asn.ByAgentIndex[i].file);
        for (var i = 12; i < 18; i++) rangedFiles.Add(asn.ByAgentIndex[i].file);

        var maxAbsMeleeFile = 0;
        foreach (var file in meleeFiles)
        {
            var abs = file < 0 ? -file : file;
            if (abs > maxAbsMeleeFile) maxAbsMeleeFile = abs;
        }
        var minAbsRangedFile = int.MaxValue;
        foreach (var file in rangedFiles)
        {
            var abs = file < 0 ? -file : file;
            if (abs < minAbsRangedFile) minAbsRangedFile = abs;
        }

        Assert.IsTrue(minAbsRangedFile >= maxAbsMeleeFile,
            $"Ranged should be on the wings (further from center). Max abs melee file={maxAbsMeleeFile}, min abs ranged file={minAbsRangedFile}");
    }

    [TestMethod]
    public void BuildInitialAssignment_Checkerboard_AlternatesMeleeAndRangedAcrossSquares()
    {
        // Equal counts so checkerboard pattern is fully populated
        var f = MakeFormation(meleeCount: 8, rangedCount: 8, width: 4f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.Checkerboard);

        // For every melee at (row, col), the diagonal-adjacent (row±1, col±1) should be ranged.
        // Simpler check: count adjacency parity. In a perfect checkerboard, melee occupies one color,
        // ranged the other. Sum of (row+col) parity for melee should be uniform (all even or all odd).
        var meleeParities = new HashSet<int>();
        for (var i = 0; i < 8; i++)
        {
            var slot = asn.ByAgentIndex[i];
            meleeParities.Add((slot.row + slot.file + 100) % 2); // +100 to handle negative file
        }
        Assert.IsTrue(meleeParities.Count == 1 || asn.ByAgentIndex.Count < 16,
            "All melee should be on the same color square in a fully populated checkerboard");
    }

    [TestMethod]
    public void BuildInitialAssignment_AllUnitsUnique_NoOverlap()
    {
        var f = MakeFormation(meleeCount: 15, rangedCount: 10, width: 10f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.InfantryFrontRangedBack);

        var occupiedSlots = new HashSet<(int, int)>();
        foreach (var slot in asn.ByAgentIndex.Values)
        {
            Assert.IsTrue(occupiedSlots.Add(slot), $"Slot ({slot.row}, {slot.file}) is double-assigned");
        }
    }

    [TestMethod]
    public void BuildInitialAssignment_NarrowFormation_FallsBackToSquareRoot()
    {
        // Width 0 forces the sqrt(unitCount) fallback.
        var f = MakeFormation(meleeCount: 10, rangedCount: 6, width: 0f, interval: 1f);

        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.InfantryFrontRangedBack);

        Assert.IsTrue(asn.FilesPerRow >= 1);
        Assert.AreEqual(16, asn.ByAgentIndex.Count);
    }

    // -------- AssignNextSlot (mid-mission new units) --------

    [TestMethod]
    public void AssignNextSlot_NewMeleeAfterInitial_AdvancesNextMeleeCounter()
    {
        var f = MakeFormation(meleeCount: 4, rangedCount: 3, width: 4f, interval: 1f);
        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.InfantryFrontRangedBack);
        var preMeleeIdx = asn.NextMeleeIndex;
        var preRangedIdx = asn.NextRangedIndex;

        _ = _sut.AssignNextSlot(asn, new FormationUnit(99, isRanged: false));

        Assert.AreEqual(preMeleeIdx + 1, asn.NextMeleeIndex, "Next-melee counter should advance by 1");
        Assert.AreEqual(preRangedIdx, asn.NextRangedIndex, "Next-ranged counter should NOT change");
        Assert.IsFalse(asn.ByAgentIndex.ContainsKey(99), "AssignNextSlot must not auto-add to map (caller's responsibility — matches dev pattern)");
    }

    [TestMethod]
    public void AssignNextSlot_NewRangedAfterInitial_AdvancesNextRangedCounter()
    {
        var f = MakeFormation(meleeCount: 4, rangedCount: 3, width: 4f, interval: 1f);
        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.RangedFrontInfantryBack);
        var preMeleeIdx = asn.NextMeleeIndex;
        var preRangedIdx = asn.NextRangedIndex;

        _ = _sut.AssignNextSlot(asn, new FormationUnit(99, isRanged: true));

        Assert.AreEqual(preMeleeIdx, asn.NextMeleeIndex, "Next-melee counter should NOT change");
        Assert.AreEqual(preRangedIdx + 1, asn.NextRangedIndex, "Next-ranged counter should advance by 1");
    }

    [TestMethod]
    public void AssignNextSlot_InfantryFrontMeleeNewcomer_RowFollowsMeleeBlock()
    {
        // 4 pre-existing melee, filesPerRow=2 → melee fills rows 0-1 (NextMeleeIndex=4 after init)
        // New melee: num3=4, num4=0 → row = 0 + 4/2 = 2.
        var f = MakeFormation(meleeCount: 4, rangedCount: 3, width: 4f, interval: 1f);
        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.InfantryFrontRangedBack);

        var slot = _sut.AssignNextSlot(asn, new FormationUnit(99, isRanged: false));

        Assert.AreEqual(2, slot.row, "Melee newcomer should land at the next melee-block row");
    }

    [TestMethod]
    public void AssignNextSlot_RangedFrontRangedNewcomer_RowFollowsRangedBlock()
    {
        // 4 pre-existing melee + 3 ranged, filesPerRow=2 → ranged fills rows 0-1 (3 units / 2 per row, second row half-full)
        // After init NextRangedIndex=3. New ranged: num3=3, num4=0 → row = 0 + 3/2 = 1.
        var f = MakeFormation(meleeCount: 4, rangedCount: 3, width: 4f, interval: 1f);
        var asn = _sut.BuildInitialAssignment(f, FormationLayoutType.RangedFrontInfantryBack);

        var slot = _sut.AssignNextSlot(asn, new FormationUnit(99, isRanged: true));

        Assert.AreEqual(1, slot.row, "Ranged newcomer should land at the next ranged-block row");
    }
}
