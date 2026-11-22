using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The ladder rows a co-op guest's armoury lists (docs/features/armour-acquisition.md, item 8): built from the co-op
/// server's view of the guest's rung, never from the guest's own stale copy. Only a hand-in and a claim; no quest row.
/// </summary>
[TestClass]
public class GuestLadderRowsTests
{
    private static GuestLadderView View(bool ready = false, bool canClaim = true, string? material = "m_gondor",
        int needed = 10, int carried = 0) =>
        new(LadderSlot.Hands, ready, canClaim, 3, material, needed, carried, ready ? new[] { "gloves" } : Array.Empty<string>());

    [TestMethod]
    public void Plan_NoView_ListsNothing()
    {
        Assert.AreEqual(0, GuestLadderRows.Plan(null).Count);
    }

    [TestMethod]
    public void Plan_ReadyAtALordLevelArmoury_ListsAnEnabledClaim()
    {
        var row = GuestLadderRows.Plan(View(ready: true, material: null)).Single();

        Assert.AreEqual(GuestLadderRowKind.Claim, row.Kind);
        Assert.IsTrue(row.Enabled);
    }

    [TestMethod]
    public void Plan_ReadyBelowTheLordLevel_ListsADisabledClaim()
    {
        var row = GuestLadderRows.Plan(View(ready: true, canClaim: false, material: null)).Single();

        Assert.AreEqual(GuestLadderRowKind.Claim, row.Kind);
        Assert.IsFalse(row.Enabled);
    }

    [TestMethod]
    public void Plan_EnoughMaterials_ListsAnEnabledHandIn()
    {
        var row = GuestLadderRows.Plan(View(carried: 10)).Single();

        Assert.AreEqual(GuestLadderRowKind.HandIn, row.Kind);
        Assert.IsTrue(row.Enabled);
    }

    [TestMethod]
    public void Plan_TooFewMaterials_ListsADisabledHandIn()
    {
        var row = GuestLadderRows.Plan(View(carried: 9)).Single();

        Assert.AreEqual(GuestLadderRowKind.HandIn, row.Kind);
        Assert.IsFalse(row.Enabled);
    }

    [TestMethod]
    public void Plan_NotDoneAndNoMaterial_ListsNothing()
    {
        // No material, or nothing the rung could award: the server sends no hand-in, and the rung quest is not offered.
        Assert.AreEqual(0, GuestLadderRows.Plan(View(material: null)).Count);
    }
}
