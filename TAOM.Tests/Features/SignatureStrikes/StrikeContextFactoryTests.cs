using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The pure halves of the boundary: the two enum mirrors and the item-type gate. The engine
/// enums load from the TaleWorlds DLLs in the test bin; the agent-reading half
/// (<c>FromMeleeCollision</c>, <c>HasMeleeWeapon</c>) needs a live <c>Agent</c> and is covered
/// by the binding tests and the in-game smoke.
/// </summary>
[TestClass]
public class StrikeContextFactoryTests
{
    [TestMethod]
    public void MapDirection_AttackUp_IsOverhead()
        => Assert.AreEqual(StrikeDirection.Overhead, StrikeContextFactory.MapDirection(Agent.UsageDirection.AttackUp));

    [TestMethod]
    public void MapDirection_AttackDown_IsThrust()
        => Assert.AreEqual(StrikeDirection.Thrust, StrikeContextFactory.MapDirection(Agent.UsageDirection.AttackDown));

    [TestMethod]
    public void MapDirection_AttackLeft_IsLeft()
        => Assert.AreEqual(StrikeDirection.Left, StrikeContextFactory.MapDirection(Agent.UsageDirection.AttackLeft));

    [TestMethod]
    public void MapDirection_AttackRight_IsRight()
        => Assert.AreEqual(StrikeDirection.Right, StrikeContextFactory.MapDirection(Agent.UsageDirection.AttackRight));

    [TestMethod]
    public void MapDirection_DefendOrNone_IsNone()
    {
        Assert.AreEqual(StrikeDirection.None, StrikeContextFactory.MapDirection(Agent.UsageDirection.None));
        Assert.AreEqual(StrikeDirection.None, StrikeContextFactory.MapDirection(Agent.UsageDirection.DefendUp));
    }

    [TestMethod]
    public void MapCollision_MirrorsEveryEngineMemberByName()
    {
        Assert.AreEqual(StrikeCollision.StrikeAgent, StrikeContextFactory.MapCollision(CombatCollisionResult.StrikeAgent));
        Assert.AreEqual(StrikeCollision.HitWorld, StrikeContextFactory.MapCollision(CombatCollisionResult.HitWorld));
        Assert.AreEqual(StrikeCollision.Blocked, StrikeContextFactory.MapCollision(CombatCollisionResult.Blocked));
        Assert.AreEqual(StrikeCollision.Parried, StrikeContextFactory.MapCollision(CombatCollisionResult.Parried));
        Assert.AreEqual(StrikeCollision.ChamberBlocked, StrikeContextFactory.MapCollision(CombatCollisionResult.ChamberBlocked));
        Assert.AreEqual(StrikeCollision.None, StrikeContextFactory.MapCollision(CombatCollisionResult.None));
    }

    [TestMethod]
    public void IsRangedOrThrown_BowsCrossbowsSlingsAndThrown_AreExcluded()
    {
        // Mike, 2026-09-16: any weapon except thrown and bow. A javelin swung in melee mode is
        // still a Thrown ITEM, which is the point of gating on the item type.
        foreach (var type in new[]
        {
            ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Sling,
            ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.Pistol, ItemObject.ItemTypeEnum.Musket,
        })
        {
            Assert.IsTrue(StrikeContextFactory.IsRangedOrThrown(type), type.ToString());
        }
    }

    [TestMethod]
    public void IsRangedOrThrown_AmmoTypes_AreExcluded()
    {
        foreach (var type in new[]
        {
            ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Bolts,
            ItemObject.ItemTypeEnum.Bullets, ItemObject.ItemTypeEnum.SlingStones,
        })
        {
            Assert.IsTrue(StrikeContextFactory.IsRangedOrThrown(type), type.ToString());
        }
    }

    [TestMethod]
    public void IsRangedOrThrown_MeleeWeaponTypes_AreAllowed()
    {
        // Sauron's crafted mace reports OneHandedWeapon; a two-hander and a polearm swing too.
        foreach (var type in new[]
        {
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon,
            ItemObject.ItemTypeEnum.Polearm,
        })
        {
            Assert.IsFalse(StrikeContextFactory.IsRangedOrThrown(type), type.ToString());
        }
    }
}
