using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// The complete legal-transition set for <see cref="EnlistmentState"/>. Pure data — the
/// state machine consults it; nothing else may mutate state. Design invariants:
///   - Discharging is the ONLY entry to NotEnlisted from any enlisted-family state
///     (commission-softlock fix by construction).
///   - EnlistedPlayerCaptive only ever returns to EnlistedAttached or Discharging —
///     a captive player never enters battle interception or detached duty.
///   - EnlistedBattle never jumps to duty/grace directly; the reconciler routes through
///     EnlistedAttached after the battle resolves.
/// </summary>
public static class EnlistmentTransitionTable
{
    private static readonly HashSet<(EnlistmentState From, EnlistmentState To)> LegalEdges = new HashSet<(EnlistmentState, EnlistmentState)>
    {
        (EnlistmentState.NotEnlisted, EnlistmentState.PetitionPending),
        (EnlistmentState.PetitionPending, EnlistmentState.NotEnlisted),
        (EnlistmentState.PetitionPending, EnlistmentState.EnlistedAttached),

        (EnlistmentState.EnlistedAttached, EnlistmentState.EnlistedBattle),
        (EnlistmentState.EnlistedBattle, EnlistmentState.EnlistedAttached),

        (EnlistmentState.EnlistedAttached, EnlistmentState.EnlistedDetachedOnDuty),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.EnlistedAttached),

        (EnlistmentState.EnlistedAttached, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedBattle, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.EnlistedAttached),

        (EnlistmentState.EnlistedAttached, EnlistmentState.CommanderUnavailable),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.CommanderUnavailable),
        (EnlistmentState.CommanderUnavailable, EnlistmentState.EnlistedAttached),

        (EnlistmentState.EnlistedAttached, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedBattle, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.Discharging),
        (EnlistmentState.CommanderUnavailable, EnlistmentState.Discharging),

        (EnlistmentState.Discharging, EnlistmentState.NotEnlisted),
    };

    public static bool IsLegal(EnlistmentState from, EnlistmentState to) => LegalEdges.Contains((from, to));
}
