using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Sole authority over <see cref="EnlistmentState"/> mutation. Every state change in the
/// feature flows through <see cref="TryTransition"/>; illegal edges are rejected and
/// logged, never applied.
/// </summary>
public interface IEnlistmentStateMachine
{
    EnlistmentState State { get; }

    bool CanTransition(EnlistmentState to);

    /// <summary>Apply a legal transition. Returns false (state unchanged, warning logged) on an illegal edge.</summary>
    bool TryTransition(EnlistmentState to);
}
