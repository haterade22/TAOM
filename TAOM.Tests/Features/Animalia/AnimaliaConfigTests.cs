using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.Animalia;

namespace TAOM.Tests.Features.Animalia;

/// <summary>
/// The Animalia elk and moose's antler attack tuning (#646). The reach is the war ram's 1.5 m trigger and 2 m radius
/// at 1.0x, multiplied in the shared nodes by each animal's live size, which lives on its Monster
/// (docs/features/monster-size.md). The live-Armory size pin sits with the other Armory reads in
/// <see cref="AnimaliaMountWiringTests"/>.
/// </summary>
[TestClass]
public class AnimaliaConfigTests
{
    [TestMethod]
    public void MonsterAndActionSetIds_AreTheArmorysNames()
    {
        // Literal pins: a typo in a constant would otherwise pass every test and simply never attach a tree.
        Assert.AreEqual("taom_animalia_elk", AnimaliaConfig.ElkMonsterId);
        Assert.AreEqual("taom_animalia_moose", AnimaliaConfig.MooseMonsterId);
        Assert.AreEqual("as_animalia_elk", AnimaliaConfig.ElkActionSetId);
        Assert.AreEqual("as_animalia_moose", AnimaliaConfig.MooseActionSetId);
    }

    [TestMethod]
    public void Reach_IsTheRamsAtOneX_ScaledByTheLiveBody_TriggerInsideTheRadius()
    {
        Assert.AreEqual(1.5f, AnimaliaConfig.AttackTriggerRange, 0.001f);
        Assert.AreEqual(2f, AnimaliaConfig.AttackRadius, 0.001f);
        Assert.IsTrue(AnimaliaConfig.ReachScalesWithBody, "the reach must follow each animal's Monster size");
        // ElephantLikeEngageDecorator scans once at the radius and filters by the trigger range.
        Assert.IsTrue(AnimaliaConfig.AttackTriggerRange <= AnimaliaConfig.AttackRadius);
    }

    [TestMethod]
    public void Cooldown_OutlastsEachAttackClip()
    {
        // The tree would restart the attack mid-clip otherwise. The generated clips' Duration
        // (tools/gen_animalia_anim_clips.ps1, frames / 30): attack_front_low 75 frames, attack_head_01 47.
        Assert.IsTrue(AnimaliaConfig.AttackCooldownSeconds > 75 / 30.0);
        Assert.IsTrue(AnimaliaConfig.AttackCooldownSeconds > 47 / 30.0);
    }

    [TestMethod]
    public void Attacks_AreOneBluntBlowOnOneEnemy_AtEachAnimalsDamageAndKnockback()
    {
        // Mike's numbers (2026-09-23): the elk as the great elk (60, knockback 35), the moose harder (70, 45).
        Assert.AreEqual(60, AnimaliaConfig.ElkAttackDamage);
        Assert.AreEqual(70, AnimaliaConfig.MooseAttackDamage);
        Assert.AreEqual(35f, AnimaliaConfig.ElkBlowMagnitude);
        Assert.AreEqual(45f, AnimaliaConfig.MooseBlowMagnitude);
        Assert.AreEqual(DamageTypes.Blunt, AnimaliaConfig.AttackDamageType);
        Assert.IsTrue(AnimaliaConfig.AttackSingleTarget, "one victim per attack, the ram's #618 rule");
    }

    [TestMethod]
    public void TheAttacksAreTheirOwnActions_NotTheHorsesKick()
    {
        // The inherited horse usage set fires act_horse_kick itself as its kick_action, so a behavior-tree attack
        // bound there would also fire whenever the engine kicks (the war ram's lesson, creature-mount-authoring.md).
        Assert.AreNotEqual("act_horse_kick", AnimaliaConfig.ElkAttackActionName);
        Assert.AreNotEqual("act_horse_kick", AnimaliaConfig.MooseAttackActionName);
        Assert.AreNotEqual(AnimaliaConfig.ElkAttackActionName, AnimaliaConfig.MooseAttackActionName);
    }
}
