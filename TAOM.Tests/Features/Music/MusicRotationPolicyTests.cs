using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicRotationPolicyTests
{
    [TestMethod]
    public void ShouldRotate_SchedulesFirstBoundaryWhenUnscheduled()
    {
        var snapshot = EnabledSnapshot();

        var decision = MusicRotationPolicy.ShouldRotate(snapshot, MusicBucket.World, timer: 10f, nextRotateAt: 0f);

        Assert.IsFalse(decision.Rotate);
        Assert.AreEqual(40f, decision.NextRotateAt);
    }

    [TestMethod]
    public void ShouldRotate_RotatesWhenTimerReachesBoundary()
    {
        var snapshot = EnabledSnapshot();

        var decision = MusicRotationPolicy.ShouldRotate(snapshot, MusicBucket.World, timer: 40f, nextRotateAt: 40f);

        Assert.IsTrue(decision.Rotate);
        Assert.AreEqual(70f, decision.NextRotateAt);
    }

    [TestMethod]
    public void ShouldRotate_DisabledBucketReturnsMaxBoundary()
    {
        var snapshot = new MusicRotationPolicy.RotationSnapshot(
            musicEnabled: true,
            enableWorldRotation: true,
            enableTownRotation: false,
            enableBattleRotation: true,
            worldRotateIntervalSeconds: 30f,
            townRotateIntervalSeconds: 20f,
            battleRotateIntervalSeconds: 10f,
            characterCreationRotateIntervalSeconds: 0f);

        var decision = MusicRotationPolicy.ShouldRotate(snapshot, MusicBucket.Town, timer: 100f, nextRotateAt: 20f);

        Assert.IsFalse(decision.Rotate);
        Assert.AreEqual(float.MaxValue, decision.NextRotateAt);
    }

    [TestMethod]
    public void ShouldRotate_UsesTownIntervalForTavern()
    {
        var snapshot = EnabledSnapshot();

        var decision = MusicRotationPolicy.ShouldRotate(snapshot, MusicBucket.Tavern, timer: 25f, nextRotateAt: 25f);

        Assert.IsTrue(decision.Rotate);
        Assert.AreEqual(45f, decision.NextRotateAt);
    }

    [TestMethod]
    public void ShouldRotate_MusicDisabledKeepsExistingBoundary()
    {
        var snapshot = new MusicRotationPolicy.RotationSnapshot(
            musicEnabled: false,
            enableWorldRotation: true,
            enableTownRotation: true,
            enableBattleRotation: true,
            worldRotateIntervalSeconds: 30f,
            townRotateIntervalSeconds: 20f,
            battleRotateIntervalSeconds: 10f,
            characterCreationRotateIntervalSeconds: 15f);

        var decision = MusicRotationPolicy.ShouldRotate(snapshot, MusicBucket.Battle, timer: 100f, nextRotateAt: 50f);

        Assert.IsFalse(decision.Rotate);
        Assert.AreEqual(50f, decision.NextRotateAt);
    }

    private static MusicRotationPolicy.RotationSnapshot EnabledSnapshot()
    {
        return new MusicRotationPolicy.RotationSnapshot(
            musicEnabled: true,
            enableWorldRotation: true,
            enableTownRotation: true,
            enableBattleRotation: true,
            worldRotateIntervalSeconds: 30f,
            townRotateIntervalSeconds: 20f,
            battleRotateIntervalSeconds: 10f,
            characterCreationRotateIntervalSeconds: 15f);
    }
}
