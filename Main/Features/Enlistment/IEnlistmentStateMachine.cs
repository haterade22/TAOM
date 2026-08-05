using System;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Sole authority over <see cref="EnlistmentState"/> mutation. Every state change in the
/// feature flows through <see cref="TryTransition"/>; illegal edges are rejected and
/// logged, never applied. Consumers react via <see cref="Transitioned"/>.
/// </summary>
public interface IEnlistmentStateMachine
{
    EnlistmentState State { get; }

    bool CanTransition(EnlistmentState to);

    /// <summary>Apply a legal transition. Returns false (state unchanged, warning logged) on an illegal edge.</summary>
    bool TryTransition(EnlistmentState to);

    /// <summary>Raised after a successful transition with (from, to).</summary>
    event Action<EnlistmentState, EnlistmentState> Transitioned;
}
