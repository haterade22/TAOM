using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Commands compare by value, because the engine layer applies a command only when it differs from the last one.</summary>
[TestClass]
public class LaneCommandTests
{
    [TestMethod]
    public void Factories_SetTheKindAndPayload()
    {
        var point = new Vec3f(1f, 2f, 3f);

        Assert.AreEqual(LaneCommandKind.None, LaneCommand.None.Kind);
        Assert.AreEqual(LaneCommandKind.Charge, LaneCommand.Charge.Kind);

        var move = LaneCommand.MoveTo(point);
        Assert.AreEqual(LaneCommandKind.MoveTo, move.Kind);
        Assert.AreEqual(point, move.Target);

        var hold = LaneCommand.HoldAt(point);
        Assert.AreEqual(LaneCommandKind.HoldAt, hold.Kind);
        Assert.AreEqual(point, hold.Target);

        var attack = LaneCommand.AttackTower("t1");
        Assert.AreEqual(LaneCommandKind.AttackTower, attack.Kind);
        Assert.AreEqual("t1", attack.TowerId);
    }

    [TestMethod]
    public void Equality_SamePayload_IsEqual()
    {
        Assert.AreEqual(LaneCommand.MoveTo(new Vec3f(1f, 2f, 3f)), LaneCommand.MoveTo(new Vec3f(1f, 2f, 3f)));
        Assert.AreEqual(LaneCommand.AttackTower("a"), LaneCommand.AttackTower("a"));
        Assert.AreEqual(LaneCommand.Charge, LaneCommand.Charge);
    }

    [TestMethod]
    public void Equality_DifferentKindOrPayload_IsNotEqual()
    {
        var p = new Vec3f(1f, 2f, 3f);

        Assert.AreNotEqual(LaneCommand.MoveTo(p), LaneCommand.HoldAt(p));
        Assert.AreNotEqual(LaneCommand.MoveTo(p), LaneCommand.MoveTo(new Vec3f(1f, 2f, 4f)));
        Assert.AreNotEqual(LaneCommand.AttackTower("a"), LaneCommand.AttackTower("b"));
        Assert.AreNotEqual(LaneCommand.None, LaneCommand.Charge);
    }
}
