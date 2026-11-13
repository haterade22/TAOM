using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Controllable BoneCheck subclass that bypasses sealed TaleWorlds engine calls.
/// Tick() returns a configurable value and tracks invocation count.
/// </summary>
internal class FakeBoneCheck : BoneCheck
{
    private readonly Queue<bool> _tickResults;
    public int TickCallCount { get; private set; }
    public bool ExpirationCallbackFired { get; private set; }

    public FakeBoneCheck(bool[] tickResults)
        : base(
            agent: Substitute.For<IAgentAdapter>(),
            targets: new List<IAgentAdapter>(),
            boneIds: new List<sbyte> { 0 },
            maxDuration: 999f,
            boneCollisionRadius: 0.3f,
            stopAfterFirstHit: false,
            onCollisionCallback: null,
            onExpiration: null)
    {
        _tickResults = new Queue<bool>(tickResults);
        _onExpiration = () => ExpirationCallbackFired = true;
    }

    public override bool Tick(float dt)
    {
        TickCallCount++;
        return _tickResults.Count > 0 ? _tickResults.Dequeue() : false;
    }
}

[TestClass]
public class BoneCollisionServiceTests
{
    private BoneCollisionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BoneCollisionService();
    }

    // -------------------------------------------------------------------------
    // AddBoneCheckComponent
    // -------------------------------------------------------------------------

    [TestMethod]
    public void AddBoneCheckComponent_NullComponent_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.ThrowsException<ArgumentNullException>(() => _sut.AddBoneCheckComponent(null));
    }

    [TestMethod]
    public void AddBoneCheckComponent_ValidComponent_ComponentIsTickedOnNextTick()
    {
        // Arrange
        var fake = new FakeBoneCheck(new[] { true });

        // Act
        _sut.AddBoneCheckComponent(fake);
        _sut.TickBoneChecks(0.016f);

        // Assert
        Assert.AreEqual(1, fake.TickCallCount);
    }

    [TestMethod]
    public void AddBoneCheckComponent_MultipleComponents_AllAreTickedEachFrame()
    {
        // Arrange
        var fake1 = new FakeBoneCheck(new[] { true, true });
        var fake2 = new FakeBoneCheck(new[] { true, true });

        _sut.AddBoneCheckComponent(fake1);
        _sut.AddBoneCheckComponent(fake2);

        // Act
        _sut.TickBoneChecks(0.016f);
        _sut.TickBoneChecks(0.016f);

        // Assert
        Assert.AreEqual(2, fake1.TickCallCount);
        Assert.AreEqual(2, fake2.TickCallCount);
    }

    // -------------------------------------------------------------------------
    // TickBoneChecks — expiry / removal
    // -------------------------------------------------------------------------

    [TestMethod]
    public void TickBoneChecks_ComponentReturnsFalse_ComponentIsRemovedAfterTick()
    {
        // Arrange — returns false on first tick (expired)
        var fake = new FakeBoneCheck(new[] { false });
        _sut.AddBoneCheckComponent(fake);

        // Act
        _sut.TickBoneChecks(0.016f);  // removes the component
        _sut.TickBoneChecks(0.016f);  // second tick — should not reach the removed component

        // Assert — Tick was only called once despite two service ticks
        Assert.AreEqual(1, fake.TickCallCount);
    }

    [TestMethod]
    public void TickBoneChecks_MixedAliveAndExpired_OnlyAliveComponentsAreRetained()
    {
        // Arrange
        var alive = new FakeBoneCheck(new[] { true, true });
        var expired = new FakeBoneCheck(new[] { false }); // dies on first tick

        _sut.AddBoneCheckComponent(alive);
        _sut.AddBoneCheckComponent(expired);

        // Act
        _sut.TickBoneChecks(0.016f);
        _sut.TickBoneChecks(0.016f);

        // Assert
        Assert.AreEqual(2, alive.TickCallCount,    "Alive component should be ticked twice");
        Assert.AreEqual(1, expired.TickCallCount,  "Expired component should only be ticked once");
    }

    [TestMethod]
    public void TickBoneChecks_EmptyList_DoesNotThrow()
    {
        // Arrange — no components added

        // Act / Assert
        _sut.TickBoneChecks(0.016f);  // must not throw
    }

    // -------------------------------------------------------------------------
    // Clear
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Clear_AfterAddingComponents_NoComponentsAreTicked()
    {
        // Arrange
        var fake = new FakeBoneCheck(new[] { true, true });
        _sut.AddBoneCheckComponent(fake);

        // Act
        _sut.Clear();
        _sut.TickBoneChecks(0.016f);

        // Assert
        Assert.AreEqual(0, fake.TickCallCount);
    }

    [TestMethod]
    public void Clear_CalledOnEmptyService_DoesNotThrow()
    {
        // Arrange — nothing added

        // Act / Assert
        _sut.Clear();  // must not throw
    }

    [TestMethod]
    public void Clear_ThenAddNewComponent_NewComponentIsTickedNormally()
    {
        // Arrange — add a component, clear, add a fresh one, verify tick count
        var first = new FakeBoneCheck(new[] { true });
        var second = new FakeBoneCheck(new[] { true });

        _sut.AddBoneCheckComponent(first);
        _sut.Clear();

        _sut.AddBoneCheckComponent(second);

        // Act
        _sut.TickBoneChecks(0.016f);

        // Assert
        Assert.AreEqual(0, first.TickCallCount,  "Cleared component must not be ticked");
        Assert.AreEqual(1, second.TickCallCount, "Newly added component must be ticked");
    }
}
