using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// Patch170: a save made after "Continue (risky)" on a failed battle end holds a player encounter whose finalized
/// siege map event never finished (parties still attached) and, after a load, has no component back-reference, so
/// every map tick throws a NullReferenceException in <c>OnPlayerEncounterFinalizeBattle</c>. This service runs the
/// rest of vanilla's finish: the side-end step, then <c>MapEventVisual.OnMapEventEnd</c> and <c>FinalizeComponent</c>
/// (vanilla's <c>FinalizeBattle</c> reaches only <c>FinalizeEvent</c>, a no-op on a finalized event, so they run once).
/// After a load most of the last two do nothing, but a blockade's <c>OnFinalize</c> still ends its siege when the
/// defenders lost. Vanilla's <c>FinalizeBattle</c> then completes. A healthy encounter never matches. Heroes in the attached parties marked
/// <c>DiedInBattle</c> die as vanilla would (the side-end step runs vanilla's kill loop). Marked heroes still alive afterwards
/// (for example the party that threw, already detached) are counted in the log, over all living marked heroes, and die at the
/// end of their next battle as vanilla would. If restoring the back-reference works but ending a side throws, vanilla still
/// finishes the encounter and clears its battle, so this repair never sees that event again; <see cref="OrphanedBattleService"/>
/// finishes it hourly unless the player's party is still attached to it. Under a shared world (host defers) it stands down. Details: docs/features/map-event-guard.md.
/// </summary>
public sealed class HalfFinishedEncounterService
{
    public enum Action
    {
        None,
        RestoreBackReferenceOnly,
        CompleteFinalize,
    }

    private readonly IHalfFinishedEncounterAdapter _encounter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public HalfFinishedEncounterService(IHalfFinishedEncounterAdapter encounter, ICoopSessionProvider coop, IModLogger logger)
    {
        _encounter = encounter;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>Pure rule: act only on a finalized event with a missing back-reference or parties still attached.</summary>
    public static Action Decide(HalfFinishedEncounterSnapshot snapshot)
    {
        if (!snapshot.HasBattle || !snapshot.IsFinalized)
            return Action.None;
        if (snapshot.DefenderParties > 0 || snapshot.AttackerParties > 0)
            return Action.CompleteFinalize;
        return snapshot.BackReferenceMissing ? Action.RestoreBackReferenceOnly : Action.None;
    }

    /// <summary>Repairs the encounter when needed. The caller then lets vanilla run.</summary>
    public void RepairIfNeeded()
    {
        if (_coop.ShouldDeferToHost)
            return;

        var snapshot = _encounter.Read();
        var action = Decide(snapshot);
        if (action == Action.None)
            return;

        if (snapshot.BackReferenceMissing)
            _encounter.RestoreBackReference();

        int defenders = 0;
        int attackers = 0;
        if (action == Action.CompleteFinalize)
        {
            defenders = _encounter.EndSide(0);
            attackers = _encounter.EndSide(1);
            _encounter.FinishEvent();
        }

        int marked = _encounter.CountMarkedAliveHeroes();
        _logger.LogWarning(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "[MapEventGuard] finished a half-finished encounter: action={0}, backReferenceRestored={1}, defenderParties={2}, attackerParties={3}, heroesAliveMarkedDiedInBattle={4}",
            action, snapshot.BackReferenceMissing, defenders, attackers, marked));
    }
}
