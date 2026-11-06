using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Combat;

// A raised shield covers only its bearer's front (#1089). The dwarf attacks, the troll's Brute Force ring, the war ram
// and the other elephant-like creatures, and the signature strike rings all cut a shield-blocked blow and skip its
// knockdown; before this rule they did so whichever way the shield faced (rca-dwarf-attacks-2026-11-06.md F4). The
// boundary, CustomAttacksUtils.ShieldBlocks, checks that a shield is raised; this is the facing geometry.

namespace TAOM.Tests.Core.Combat;

[TestClass]
public class ShieldCoverTests
{
    [TestMethod]
    public void Faces_AttackerStraightAhead_IsTrue()
    {
        // The victim looks along +x; the attacker stands 2 m in front of him.
        Assert.IsTrue(ShieldCover.Faces(victimLookX: 1f, victimLookY: 0f, toAttackerX: 2f, toAttackerY: 0f));
    }

    [TestMethod]
    public void Faces_AttackerAtAnAngleInFront_IsTrue()
    {
        Assert.IsTrue(ShieldCover.Faces(1f, 0f, 1f, 1.5f));
    }

    [DataTestMethod]
    [DataRow(-2f, 0f)]    // the attacker is behind him
    [DataRow(0f, 2f)]     // exactly at his side: the edge of the cover
    [DataRow(-1f, 1.5f)]  // behind and to the side
    public void Faces_AttackerNotInFront_IsFalse(float toAttackerX, float toAttackerY)
    {
        Assert.IsFalse(ShieldCover.Faces(1f, 0f, toAttackerX, toAttackerY));
    }

    // Engine floats: a degenerate or non-finite direction cannot prove cover, so the blow lands in full.
    [DataTestMethod]
    [DataRow(0f, 0f, 2f, 0f)]               // no look direction
    [DataRow(1f, 0f, 0f, 0f)]               // the attacker stands on him
    [DataRow(1e-5f, 0f, 2f, 0f)]            // a look shorter than MinLength
    [DataRow(1f, 0f, 1e-5f, 0f)]            // an attacker closer than MinLength
    [DataRow(float.NaN, 0f, 2f, 0f)]
    [DataRow(1f, 0f, float.NaN, 0f)]
    [DataRow(float.PositiveInfinity, 0f, 2f, 0f)]
    public void Faces_DegenerateOrNotFinite_IsFalse(float lookX, float lookY, float toX, float toY)
    {
        Assert.IsFalse(ShieldCover.Faces(lookX, lookY, toX, toY));
    }
}
