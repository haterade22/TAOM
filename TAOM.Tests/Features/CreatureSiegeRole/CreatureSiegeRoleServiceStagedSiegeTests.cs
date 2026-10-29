using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>The role service yields to the staged siege plan (docs/features/staged-siege.md "Sharing the field").</summary>
[TestClass]
public class CreatureSiegeRoleServiceStagedSiegeTests
{
    private IStagedSiegeFieldReader _reader = null!;
    private bool _attackerOwned;
    private bool _defenderOwned;
    private SiegeSceneFixture _scene = null!;

    [TestInitialize]
    public void Setup()
    {
        CreatureSiegeSnapshot.Clear();
        _attackerOwned = false;
        _defenderOwned = false;
        _reader = Substitute.For<IStagedSiegeFieldReader>();
        _reader.OwnsField(Arg.Any<object>(), StagedSide.Attacker).Returns(_ => _attackerOwned);
        _reader.OwnsField(Arg.Any<object>(), StagedSide.Defender).Returns(_ => _defenderOwned);
        _scene = new SiegeSceneFixture(staged: _reader);
    }

    [TestCleanup]
    public void Cleanup() => _scene.Dispose();

    [TestMethod]
    public void TheReaderIsAskedOncePerSidePerPass_NotPerCreature()
    {
        _scene.Activate();
        _scene.Add();
        _scene.Add();
        _scene.Add();
        _scene.Add(SiegeSide.Defender);
        _scene.Add(SiegeSide.Defender);

        _scene.Pass();

        _reader.Received(1).OwnsField(_scene.Token, StagedSide.Attacker);
        _reader.Received(1).OwnsField(_scene.Token, StagedSide.Defender);

        _reader.ClearReceivedCalls();
        _scene.Pass();

        _reader.Received(1).OwnsField(_scene.Token, StagedSide.Attacker);
        _reader.Received(1).OwnsField(_scene.Token, StagedSide.Defender);
    }

    [TestMethod]
    public void ASideWithNoCreature_IsNeverAsked()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();

        _reader.Received(1).OwnsField(_scene.Token, StagedSide.Attacker);
        _reader.DidNotReceive().OwnsField(Arg.Any<object>(), StagedSide.Defender);
    }

    [TestMethod]
    public void AnAttackerOnAStagedSide_IsNeverScripted()
    {
        _attackerOwned = true;
        _scene.Activate();
        var troll = _scene.Add();

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, troll.Moves.Count, "the staged tactic steers the troll's formation");
    }

    [TestMethod]
    public void ARoutedTroll_IsReleased_WhenTheStagePlanTakesItsSide_AndScriptedAgainWhenItGivesItBack()
    {
        _scene.Activate();
        var attacker = _scene.Add();
        var defender = _scene.Add(SiegeSide.Defender);
        _scene.Pass();
        CollectionAssert.AreEqual(new[] { "strike" }, attacker.Moves);
        CollectionAssert.AreEqual(new[] { "hold" }, defender.Moves);

        _attackerOwned = true;
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, attacker.Moves);
        CollectionAssert.AreEqual(new[] { "hold" }, defender.Moves, "the defender's stage is still vanilla");

        _attackerOwned = false;
        _defenderOwned = true;
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "hold", "release" }, defender.Moves);
        CollectionAssert.AreEqual(new[] { "strike", "release", "strike" }, attacker.Moves,
            "back to the gate script once the plan gives the attackers back");
    }

    [TestMethod]
    public void WithNoReader_TheRoleWorksAsBefore()
    {
        using var scene = new SiegeSceneFixture();
        scene.Activate();
        var troll = scene.Add();

        scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, troll.Moves);
    }

    [TestMethod]
    public void ANonAIControlledTroll_AsksNoOne()
    {
        _scene.Activate();
        var troll = _scene.Add();
        troll.IsAIControlled = false;

        _scene.Pass();

        _reader.DidNotReceive().OwnsField(Arg.Any<object>(), Arg.Any<StagedSide>());
    }

    [TestMethod]
    public void AReaderThatThrows_FailsSafeLikeAnyOtherPassFault()
    {
        _reader.OwnsField(Arg.Any<object>(), Arg.Any<StagedSide>()).Returns(_ => throw new System.InvalidOperationException("slot fault"));
        _scene.Activate();
        var troll = _scene.Add();

        _scene.Pass();

        Assert.AreEqual(1, _scene.Errors.Count);
        Assert.AreEqual(0, troll.Moves.Count);
    }
}
