using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Tests.Features.Execution;

[TestClass]
public class ExecutionActionHookTests
{
    private IAlignmentService _alignmentService;
    private ExecutionActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _alignmentService = Substitute.For<IAlignmentService>();
        _sut = new ExecutionActionHook(_alignmentService);
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_CrossAlignment_ReturnsFalse()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);

        Assert.IsFalse(_sut.ShouldApplyHonorPenalty("empire_s", "empire_w"));
    }

    [TestMethod]
    public void ShouldApplyHonorPenalty_SameAlignment_ReturnsTrue()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);

        Assert.IsTrue(_sut.ShouldApplyHonorPenalty("vlandia", "empire_w"));
    }



    [TestMethod]
    public void GetRelationModifier_CrossAlignment_EvaluatorSameAsExecutor_ReturnsZero()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("vlandia", "empire_w").Returns(true);

        int result = _sut.GetRelationModifier("empire_w", "empire_s", "vlandia", -60);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetRelationModifier_CrossAlignment_EvaluatorSameAsVictim_ReturnsVanillaPenalty()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("isengard", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("isengard", "empire_s").Returns(true);

        int result = _sut.GetRelationModifier("empire_w", "empire_s", "isengard", -60);

        Assert.AreEqual(-60, result);
    }

    [TestMethod]
    public void GetRelationModifier_CrossAlignment_EvaluatorNeutral_ReturnsZero()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("umbar", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("umbar", "empire_s").Returns(false);

        int result = _sut.GetRelationModifier("empire_w", "empire_s", "umbar", -60);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetRelationModifier_Kinslaying_Returns150PercentPenalty()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        int result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -60);

        Assert.AreEqual(-90, result);
    }

    [TestMethod]
    public void GetRelationModifier_Kinslaying_SmallerPenalty_RoundsCorrectly()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        int result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -10);

        Assert.AreEqual(-15, result);
    }

    [TestMethod]
    public void GetRelationModifier_Kinslaying_FriendPenalty()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        int result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -30);

        Assert.AreEqual(-45, result);
    }

    // ---- the Blood Feud relation seam (v1.5.0) ----------------------------------------------
    //
    // These pin the rule the feature exists for, now that it runs through
    // ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch rather than the deleted
    // TaomExecutionRelationModel. Gondor is empire_w, Mordor is empire_s, Rohan is vlandia.

    [TestMethod]
    public void GetRelationModifier_GoodKillsEvil_SameSideObserver_NoPenalty()
    {
        // Gondor executes a Mordor lord. Rohan, a fellow free people, does not care.
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("vlandia", "empire_w").Returns(true);

        Assert.AreEqual(0, _sut.GetRelationModifier("empire_w", "empire_s", "vlandia", -45));
    }

    [TestMethod]
    public void GetRelationModifier_GoodKillsEvil_NeutralObserver_NoPenalty()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("khuzait", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("khuzait", "empire_s").Returns(false);

        Assert.AreEqual(0, _sut.GetRelationModifier("empire_w", "empire_s", "khuzait", -45));
    }

    [TestMethod]
    public void GetRelationModifier_GoodKillsEvil_VictimSideObserver_KeepsPenalty()
    {
        // The blood feud is meant to land: Mordor's allies are entitled to object.
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("isengard", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("isengard", "empire_s").Returns(true);

        Assert.AreEqual(-45, _sut.GetRelationModifier("empire_w", "empire_s", "isengard", -45));
    }

    [TestMethod]
    public void GetRelationModifier_EvilKillsGood_IsSymmetric()
    {
        // The rule is stated symmetrically, so the mirror case must behave identically.
        _alignmentService.AreEnemyAlignments("empire_s", "empire_w").Returns(true);
        _alignmentService.AreSameAlignment("isengard", "empire_s").Returns(true);

        Assert.AreEqual(0, _sut.GetRelationModifier("empire_s", "empire_w", "isengard", -45));
    }

    [TestMethod]
    public void GetRelationModifier_Kinslaying_AmplifiesPenalty()
    {
        // Gondor executes a Rohirrim lord: same side, so the penalty is amplified, not waived.
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        Assert.AreEqual(-67, _sut.GetRelationModifier("empire_w", "vlandia", "sturgia", -45));
    }
}
