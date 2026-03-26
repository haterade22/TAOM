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
    public void IsKinslaying_SameAlignment_ReturnsTrue()
    {
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        Assert.IsTrue(_sut.IsKinslaying("vlandia", "empire_w"));
    }

    [TestMethod]
    public void IsKinslaying_CrossAlignment_ReturnsFalse()
    {
        _alignmentService.AreSameAlignment("empire_w", "empire_s").Returns(false);

        Assert.IsFalse(_sut.IsKinslaying("empire_s", "empire_w"));
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
}
