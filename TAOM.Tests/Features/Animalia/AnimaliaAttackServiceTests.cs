using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Animalia;
using TAOM.Features.ElephantLike;

namespace TAOM.Tests.Features.Animalia;

/// <summary>
/// The Animalia elk and moose antler attacks (#646): each animal's binding of the shared elephant-like decision
/// service. The monster gate matters most: the Animalia elk, the moose and the great elk (#636, taom_elk) are three
/// different Monsters on one skeleton, and a tree attached to the wrong one would fire the wrong clip.
/// </summary>
[TestClass]
public class AnimaliaAttackServiceTests
{
    private readonly AnimaliaElkAttackService _elk = new();
    private readonly AnimaliaMooseAttackService _moose = new();

    [TestMethod]
    public void IsCreatureMonster_ElkService_AnswersOnlyToTheAnimaliaElk()
    {
        Assert.IsTrue(_elk.IsCreatureMonster("taom_animalia_elk"));
        Assert.IsFalse(_elk.IsCreatureMonster("taom_animalia_moose"));
        Assert.IsFalse(_elk.IsCreatureMonster("taom_elk"), "the great elk has its own tree (#636)");
        Assert.IsFalse(_elk.IsCreatureMonster("horse"));
        Assert.IsFalse(_elk.IsCreatureMonster(null));
    }

    [TestMethod]
    public void IsCreatureMonster_MooseService_AnswersOnlyToTheMoose()
    {
        Assert.IsTrue(_moose.IsCreatureMonster("taom_animalia_moose"));
        Assert.IsFalse(_moose.IsCreatureMonster("taom_animalia_elk"));
        Assert.IsFalse(_moose.IsCreatureMonster("taom_elk"));
        Assert.IsFalse(_moose.IsCreatureMonster(null));
    }

    [TestMethod]
    public void ComputeInflictedDamage_Trample_IsEachAnimalsOneBlowWhateverTheRoll()
    {
        Assert.AreEqual(60, _elk.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f));
        Assert.AreEqual(60, _elk.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 1f));
        Assert.AreEqual(70, _moose.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 0f));
        Assert.AreEqual(70, _moose.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: false, roll: 1f));
    }

    [TestMethod]
    public void ComputeInflictedDamage_ShieldBlocked_IsAQuarterRounded()
    {
        Assert.AreEqual(15, _elk.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: true, roll: 0.5f));
        // 70 x 0.25 = 17.5: the shared service rounds half to even.
        Assert.AreEqual(18, _moose.ComputeInflictedDamage(ElephantLikeAttackKind.Trample, targetBlocking: true, roll: 0.5f));
    }

    [TestMethod]
    public void ShouldEngage_TargetSquarelyInFront_Engages()
    {
        Assert.IsTrue(_elk.ShouldEngage(facingDot: 0.9f, alreadyAttacking: false));
        Assert.IsTrue(_moose.ShouldEngage(facingDot: 0.9f, alreadyAttacking: false));
    }

    [TestMethod]
    public void ShouldEngage_FacingExactlyAtTheThreshold_DoesNot()
    {
        Assert.IsFalse(_elk.ShouldEngage(facingDot: AnimaliaConfig.AttackFacingDot, alreadyAttacking: false));
        Assert.IsFalse(_moose.ShouldEngage(facingDot: AnimaliaConfig.AttackFacingDot, alreadyAttacking: false));
    }

    [TestMethod]
    public void ShouldEngage_AlreadyAttacking_DoesNot()
    {
        Assert.IsFalse(_elk.ShouldEngage(facingDot: 0.9f, alreadyAttacking: true));
        Assert.IsFalse(_moose.ShouldEngage(facingDot: 0.9f, alreadyAttacking: true));
    }
}
