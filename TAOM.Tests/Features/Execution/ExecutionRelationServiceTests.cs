using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Execution;

[TestClass]
public class ExecutionRelationServiceTests
{
    private IAlignmentService _alignmentService;
    private ExecutionRelationService _sut;

    [TestInitialize]
    public void Setup()
    {
        _alignmentService = Substitute.For<IAlignmentService>();
        _sut = new ExecutionRelationService(_alignmentService);
    }

    // ----- Null kingdom fall-through (vanilla pass-through) -----

    [TestMethod]
    public void GetRelationModifier_NullExecutorKingdom_ReturnsBaseAndPreservesNotification()
    {
        var result = _sut.GetRelationModifier(null, "empire_s", "vlandia", -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_NullVictimKingdom_ReturnsBaseAndPreservesNotification()
    {
        var result = _sut.GetRelationModifier("empire_w", null, "vlandia", -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_NullEvaluatorKingdom_ReturnsBaseAndPreservesNotification()
    {
        var result = _sut.GetRelationModifier("empire_w", "empire_s", null, -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_EmptyExecutorKingdom_ReturnsBaseAndPreservesNotification()
    {
        // Boundary returns "" when player has no kingdom — must be treated as null
        var result = _sut.GetRelationModifier("", "empire_s", "vlandia", -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    // ----- showQuickNotification suppression on zero-modified -----

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorSameAsExecutor_ZerosNotification()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("vlandia", "empire_w").Returns(true);

        var result = _sut.GetRelationModifier("empire_w", "empire_s", "vlandia", -60, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification, "Notification must be suppressed when modifier zeroes the delta");
    }

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorNeutral_ZerosNotification()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("umbar", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("umbar", "empire_s").Returns(false);

        var result = _sut.GetRelationModifier("empire_w", "empire_s", "umbar", -60, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }

    // ----- non-zero modified preserves base notification -----

    [TestMethod]
    public void GetRelationModifier_CrossAlignmentEvaluatorSameAsVictim_PreservesBaseNotificationTrue()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "empire_s").Returns(true);
        _alignmentService.AreSameAlignment("isengard", "empire_w").Returns(false);
        _alignmentService.AreSameAlignment("isengard", "empire_s").Returns(true);

        var result = _sut.GetRelationModifier("empire_w", "empire_s", "isengard", -60, true);

        Assert.AreEqual(-60, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_KinslayingApplies150PercentMultiplier()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        var result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -60, true);

        Assert.AreEqual(-90, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    [TestMethod]
    public void GetRelationModifier_SameAlignmentNotKinslaying_ReturnsBaseUnchanged()
    {
        // executor & victim different sides per AreSameAlignment? -> no kinslaying, no cross-alignment -> base
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(false);

        var result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -30, true);

        Assert.AreEqual(-30, result.RelationDelta);
        Assert.IsTrue(result.ShowNotification);
    }

    // ----- ZeroBase remains zero, but notification is suppressed (modified == 0 from base) -----

    [TestMethod]
    public void GetRelationModifier_ZeroBaseRelation_SuppressesNotificationEvenIfBaseTrue()
    {
        // If vanilla returned 0 but baseShowNotification was true (it shouldn't, but be defensive),
        // service still suppresses since modified == 0.
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(false);

        var result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", 0, true);

        Assert.AreEqual(0, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }

    // ----- baseShowNotification = false stays false on non-zero modifier -----

    [TestMethod]
    public void GetRelationModifier_BaseNotificationFalse_StaysFalse()
    {
        _alignmentService.AreEnemyAlignments("empire_w", "vlandia").Returns(false);
        _alignmentService.AreSameAlignment("empire_w", "vlandia").Returns(true);

        var result = _sut.GetRelationModifier("empire_w", "vlandia", "erebor", -60, false);

        Assert.AreEqual(-90, result.RelationDelta);
        Assert.IsFalse(result.ShowNotification);
    }
}
