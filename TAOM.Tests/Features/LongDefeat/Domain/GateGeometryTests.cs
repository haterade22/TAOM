using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Which attackers have come through a repaired gate (<c>gate-attacked</c>'s <c>through=</c>): within a few metres behind the gate's plane, on the inside, within the door's width and at the gate's height.
/// The plane is the gate's own forward axis (the prefab lays the passage along it), turned to point at the keep, because the engine has no "inside" flag on a gate.
/// </summary>
[TestClass]
public class GateGeometryTests
{
    private static readonly LdPoint Gate = new(100f, 200f, 20f);

    // A gate whose forward axis runs along +Y (the frame's forward).
    private static GatePlane PlaneToward(LdPoint inside) => GateGeometry.PlaneOf(Gate, 0f, 1f, inside)!.Value;

    [TestMethod]
    public void ThePlane_IsTheForwardAxis_TurnedToFaceTheInside()
    {
        var north = PlaneToward(new LdPoint(100f, 260f, 20f));
        var south = PlaneToward(new LdPoint(100f, 140f, 20f));

        Assert.AreEqual((0f, 1f), (north.Nx, north.Ny), "the keep lies along +Y: the forward axis, pointing inside");
        Assert.AreEqual((0f, -1f), (south.Nx, south.Ny), "the keep lies along -Y: the forward axis turned round");
    }

    [TestMethod]
    public void AKeepMostlyBesideTheGate_StillUsesTheForwardAxis_NeverTheSideOne()
    {
        // The reinforcement frame the keep falls back to at Helm's Deep is about 69 m from the gate; if its bearing were far off the forward axis, the old rule picked the side axis and the box straddled the wall beside the door.
        var plane = PlaneToward(new LdPoint(160f, 205f, 20f));

        Assert.AreEqual((0f, 1f), (plane.Nx, plane.Ny), "the door's passage runs along the forward axis whichever way the keep lies");
    }

    [TestMethod]
    public void AKeepExactlyBesideTheGate_GivesNoPlane_ItDecidesNeitherWay()
    {
        Assert.IsNull(GateGeometry.PlaneOf(Gate, 0f, 1f, new LdPoint(160f, 200f, 20f)));
    }

    [TestMethod]
    public void AnAttackerBehindThePlaneWithinTheDepth_AndTheDoorsWidth_IsThrough()
    {
        var plane = PlaneToward(new LdPoint(100f, 260f, 20f));

        Assert.IsTrue(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 202f, 20f), 3f, 5f, 4f));
        Assert.IsTrue(GateGeometry.IsBehind(Gate, plane, new LdPoint(104f, 203f, 20f), 3f, 5f, 4f), "on the edge of the door's width");
    }

    [TestMethod]
    public void AnAttackerInFrontOfTheGate_FarBehindIt_OrBesideTheDoor_IsNotThrough()
    {
        var plane = PlaneToward(new LdPoint(100f, 260f, 20f));

        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 198f, 20f), 3f, 5f, 4f), "outside, where the ram stands");
        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 204f, 20f), 3f, 5f, 4f), "4 m behind is past the depth");
        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, new LdPoint(107f, 202f, 20f), 3f, 5f, 4f), "behind the wall beside the door");
        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, Gate, 3f, 5f, 4f), "on the plane itself is not behind it");
    }

    [TestMethod]
    public void AnAttackerOnTheWallWalkAboveTheGate_IsNotThrough_HoweverNearItsPlane()
    {
        var plane = PlaneToward(new LdPoint(100f, 260f, 20f));

        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 202f, 32f), 3f, 5f, 4f), "12 m above the gate is the wall walk, not the doorway");
        Assert.IsTrue(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 202f, 22f), 3f, 5f, 4f), "on a step or a slope a couple of metres up");
        Assert.IsFalse(GateGeometry.IsBehind(Gate, plane, new LdPoint(100f, 202f, 14f), 3f, 5f, 4f), "the band holds below the gate as well");
    }

    [TestMethod]
    public void ADegenerateFrame_OrAnInsideAtTheGate_GivesNoPlane()
    {
        Assert.IsNull(GateGeometry.PlaneOf(Gate, 0f, 0f, new LdPoint(100f, 260f, 20f)), "a frame with no horizontal forward axis");
        Assert.IsNull(GateGeometry.PlaneOf(Gate, 0f, 1f, Gate), "no direction to the inside");
        Assert.IsNull(GateGeometry.PlaneOf(Gate, float.NaN, 1f, new LdPoint(100f, 260f, 20f)));
    }

    [TestMethod]
    public void ATiltedFrame_UsesItsHorizontalPartOnly()
    {
        // A forward axis pitched 45 degrees up: (0, 0.7, 0.7) horizontally (0, 0.7), renormalised to (0, 1).
        var plane = GateGeometry.PlaneOf(Gate, 0f, 0.7f, new LdPoint(100f, 260f, 20f))!.Value;

        Assert.AreEqual(1f, plane.Ny, 1e-5f);
    }
}
