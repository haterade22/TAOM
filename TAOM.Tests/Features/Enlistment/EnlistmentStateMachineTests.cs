using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentStateMachineTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
    }

    [TestMethod]
    public void State_ReflectsStoreRecord()
    {
        _store.Record.State = EnlistmentState.PetitionPending;

        Assert.AreEqual(EnlistmentState.PetitionPending, _machine.State);
    }

    [TestMethod]
    public void TryTransition_LegalEdge_UpdatesStateAndReturnsTrue()
    {
        var result = _machine.TryTransition(EnlistmentState.PetitionPending);

        Assert.IsTrue(result);
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
    }

    [TestMethod]
    public void TryTransition_LegalEdge_RaisesTransitionedEventWithFromAndTo()
    {
        EnlistmentState? observedFrom = null;
        EnlistmentState? observedTo = null;
        _machine.Transitioned += (from, to) => { observedFrom = from; observedTo = to; };

        _machine.TryTransition(EnlistmentState.PetitionPending);

        Assert.AreEqual(EnlistmentState.NotEnlisted, observedFrom);
        Assert.AreEqual(EnlistmentState.PetitionPending, observedTo);
    }

    [TestMethod]
    public void TryTransition_IllegalEdge_RejectsKeepsStateLogsWarning()
    {
        var result = _machine.TryTransition(EnlistmentState.EnlistedBattle);

        Assert.IsFalse(result);
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("NotEnlisted") && s.Contains("EnlistedBattle")));
    }

    [TestMethod]
    public void TryTransition_IllegalEdge_DoesNotRaiseEvent()
    {
        var raised = false;
        _machine.Transitioned += (_, _) => raised = true;

        _machine.TryTransition(EnlistmentState.Discharging);

        Assert.IsFalse(raised);
    }

    [TestMethod]
    public void TryTransition_SameState_Rejects()
    {
        Assert.IsFalse(_machine.TryTransition(EnlistmentState.NotEnlisted));
    }

    [TestMethod]
    public void CanTransition_MirrorsTable()
    {
        Assert.IsTrue(_machine.CanTransition(EnlistmentState.PetitionPending));
        Assert.IsFalse(_machine.CanTransition(EnlistmentState.EnlistedAttached));
    }

    [TestMethod]
    public void TryTransition_FullServiceLifecycle_Walkable()
    {
        // petition -> oath -> battle -> back -> duty -> back -> captured -> released
        // -> commander lost -> recovered -> discharge -> done
        foreach (var step in new[]
        {
            EnlistmentState.PetitionPending,
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedBattle,
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedDetachedOnDuty,
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedPlayerCaptive,
            EnlistmentState.EnlistedAttached,
            EnlistmentState.CommanderUnavailable,
            EnlistmentState.EnlistedAttached,
            EnlistmentState.Discharging,
            EnlistmentState.NotEnlisted,
        })
        {
            Assert.IsTrue(_machine.TryTransition(step), $"Lifecycle step to {step} should be legal");
        }
    }
}
