using System;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentStateMachine : IEnlistmentStateMachine
{
    private readonly IEnlistmentStore _store;
    private readonly IModLogger _logger;

    public EnlistmentStateMachine(IEnlistmentStore store, IModLogger logger)
    {
        _store = store;
        _logger = logger;
    }

    public EnlistmentState State => _store.Record.State;

    public event Action<EnlistmentState, EnlistmentState> Transitioned;

    public bool CanTransition(EnlistmentState to) => EnlistmentTransitionTable.IsLegal(State, to);

    public bool TryTransition(EnlistmentState to)
    {
        var from = State;
        if (!EnlistmentTransitionTable.IsLegal(from, to))
        {
            _logger?.LogWarning($"EnlistmentStateMachine: illegal transition {from} -> {to} rejected");
            return false;
        }

        _store.Record.State = to;
        Transitioned?.Invoke(from, to);
        return true;
    }
}
