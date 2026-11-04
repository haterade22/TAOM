using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The mission policy table of build-spec section 4, one row per cell. The kind is an int in the data rows because a public
/// test method cannot take the internal enum.
/// </summary>
[TestClass]
public class SeatMissionPolicyTests
{
    private static SeatPermissions Decide(SeatMissionKind kind, bool network = false, bool foreign = false) =>
        SeatMissionPolicy.Decide(new SeatMissionFacts(kind, network, foreign));

    // Kind, camera, fight on, orders, time.
    [DataTestMethod]
    [DataRow((int)SeatMissionKind.Battle, true, true, true, true)]
    [DataRow((int)SeatMissionKind.Hideout, true, false, true, true)]
    [DataRow((int)SeatMissionKind.Tournament, true, false, false, false)]
    [DataRow((int)SeatMissionKind.Arena, true, false, false, false)]
    [DataRow((int)SeatMissionKind.Settlement, true, false, false, false)]
    [DataRow((int)SeatMissionKind.Other, true, false, false, false)]
    public void Decide_EachKind_FollowsTheTable(int kind, bool camera, bool fightOn, bool orders, bool time)
    {
        var result = Decide((SeatMissionKind)kind);

        Assert.AreEqual(new SeatPermissions(camera, fightOn, orders, time), result);
    }

    // A session keeps the camera and the orders the kind allows, and loses the hero's hand-off and mission time.
    [DataTestMethod]
    [DataRow((int)SeatMissionKind.Battle, true)]
    [DataRow((int)SeatMissionKind.Hideout, true)]
    [DataRow((int)SeatMissionKind.Tournament, false)]
    [DataRow((int)SeatMissionKind.Arena, false)]
    [DataRow((int)SeatMissionKind.Settlement, false)]
    [DataRow((int)SeatMissionKind.Other, false)]
    public void Decide_NetworkSession_KeepsCameraAndKindOrders_DropsFightOnAndTime(int kind, bool kindOrders)
    {
        var result = Decide((SeatMissionKind)kind, network: true);

        Assert.AreEqual(new SeatPermissions(true, false, kindOrders, false), result);
    }

    [DataTestMethod]
    [DataRow((int)SeatMissionKind.Battle)]
    [DataRow((int)SeatMissionKind.Hideout)]
    [DataRow((int)SeatMissionKind.Settlement)]
    [DataRow((int)SeatMissionKind.Other)]
    public void Decide_ForeignCamera_DeniesEverything(int kind)
    {
        Assert.AreEqual(default(SeatPermissions), Decide((SeatMissionKind)kind, foreign: true));
        Assert.AreEqual(default(SeatPermissions), Decide((SeatMissionKind)kind, network: true, foreign: true));
    }

    [TestMethod]
    public void Decide_DefaultPermissions_AllowNothing()
    {
        var none = default(SeatPermissions);

        Assert.IsFalse(none.Camera || none.FightOn || none.Orders || none.Time);
    }
}
