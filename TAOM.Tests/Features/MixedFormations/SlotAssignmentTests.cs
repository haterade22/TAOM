using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Tests.Features.MixedFormations;

/// <summary>
/// The per-formation slot record behind mixed formations. A deleted unit's slot is held for the
/// next unit of the same class, so replacements stand where their predecessors did instead of a
/// counter pushing them a row deeper and onto the other class's rows (#595, Codex review 109).
/// </summary>
[TestClass]
public class SlotAssignmentTests
{
    [TestMethod]
    public void Forget_UnknownIndex_ReturnsFalse_AndFreesNothing()
    {
        var asn = new SlotAssignment(FormationLayoutType.InfantryFrontRangedBack, filesPerRow: 5);

        Assert.IsFalse(asn.Forget(42));
        Assert.AreEqual(0, asn.FreedMeleeCount);
        Assert.AreEqual(0, asn.FreedRangedCount);
    }

    [TestMethod]
    public void Forget_HoldsTheSlotForTheUnitsOwnClass()
    {
        var asn = new SlotAssignment(FormationLayoutType.InfantryFrontRangedBack, filesPerRow: 5);
        asn.Assign(1, isRanged: false, (0, -2));
        asn.Assign(2, isRanged: true, (1, -2));

        Assert.IsTrue(asn.Forget(1));
        Assert.IsTrue(asn.Forget(2));

        Assert.IsFalse(asn.ByAgentIndex.ContainsKey(1));
        Assert.IsFalse(asn.ByAgentIndex.ContainsKey(2));
        Assert.AreEqual(1, asn.FreedMeleeCount);
        Assert.AreEqual(1, asn.FreedRangedCount);
        Assert.IsTrue(asn.TryReclaim(isRanged: false, out var melee));
        Assert.AreEqual((0, -2), melee);
        Assert.IsTrue(asn.TryReclaim(isRanged: true, out var ranged));
        Assert.AreEqual((1, -2), ranged);
        Assert.IsFalse(asn.TryReclaim(isRanged: false, out _), "a slot is handed out once");
    }

    [TestMethod]
    public void TryReclaim_TheOtherClass_LeavesTheSlotWaiting()
    {
        var asn = new SlotAssignment(FormationLayoutType.InfantryFrontRangedBack, filesPerRow: 5);
        asn.Assign(1, isRanged: false, (0, 0));
        asn.Forget(1);

        Assert.IsFalse(asn.TryReclaim(isRanged: true, out _));
        Assert.AreEqual(1, asn.FreedMeleeCount);
    }

    [TestMethod]
    public void Assign_ARecycledIndexWithANewClass_ReplacesTheOldClass()
    {
        var asn = new SlotAssignment(FormationLayoutType.InfantryFrontRangedBack, filesPerRow: 5);
        asn.Assign(7, isRanged: true, (3, 0));
        asn.Forget(7);
        asn.Assign(7, isRanged: false, (0, 1));

        asn.Forget(7);

        Assert.AreEqual(1, asn.FreedRangedCount, "the first tenant's slot");
        Assert.AreEqual(1, asn.FreedMeleeCount, "the second tenant's slot, by its own class");
    }
}
