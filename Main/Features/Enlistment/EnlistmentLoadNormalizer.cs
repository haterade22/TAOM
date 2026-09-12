using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentLoadNormalizer : IEnlistmentLoadNormalizer
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IMobilePartyAttachmentAdapter _partyAdapter;
    private readonly IDischargeService _discharge;
    private readonly IEncounterAdapter _encounter;
    private readonly IModLogger _logger;

    public EnlistmentLoadNormalizer(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IEnlistmentReconciler reconciler,
        IMobilePartyAttachmentAdapter partyAdapter,
        IDischargeService discharge,
        IEncounterAdapter encounter,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _reconciler = reconciler;
        _partyAdapter = partyAdapter;
        _discharge = discharge;
        _encounter = encounter;
        _logger = logger;
    }

    public void Normalize(string currentMainHeroId, double nowDays)
    {
        var record = _store.Record;
        var presence = _partyAdapter.GetPresence();

        if (!record.IsEnlisted)
        {
            // Matrix row: NotEnlisted (or petition) with a hidden+inactive MainParty and
            // no captivity — an ownerless parked party (foreign save, dropped record) is
            // rescued unconditionally. Captivity legitimately hides the party: not ours.
            if (presence.LooksParked && !presence.IsCaptive)
            {
                _logger?.LogWarning("[Enlistment] load rescue: main party was hidden+inactive with no service record (ownerless) — restoring presence");
                // ONE SHOT. Normalize runs only from OnGameLoaded, and the hourly reconciler
            // early-returns when the record says NotEnlisted — so nothing ever retries this.
            // A silent failure here leaves the player hidden and inactive for the whole
            // session with no way to act.
            if (!_partyAdapter.RestorePresence())
                _logger?.LogError("[Enlistment] load rescue FAILED — the main party is hidden and inactive with no service record, and nothing will retry. The player cannot act until this is resolved.");
            }
            return;
        }

        // Identity guard (co-op join / heir succession): the oath belongs to a hero that
        // is no longer the player. Quiet, penalty-free discharge.
        if (!string.IsNullOrEmpty(record.EnlistedHeroId)
            && !string.IsNullOrEmpty(currentMainHeroId)
            && record.EnlistedHeroId != currentMainHeroId)
        {
            _logger?.LogInfo($"[Enlistment] enlisted hero '{record.EnlistedHeroId}' is no longer the player ('{currentMainHeroId}') — quiet discharge");
            _discharge.Execute(DischargeReason.HeirSuccessionOrPossessionMismatch);
            return;
        }

        // Captivity sync: while captive, move the machine to the captive state (legal
        // from every persisted enlisted state except CommanderUnavailable, whose grace
        // simply freezes) and touch nothing else — vanilla captivity owns the party.
        if (presence.IsCaptive)
        {
            if (record.State != EnlistmentState.EnlistedPlayerCaptive
                && record.State != EnlistmentState.CommanderUnavailable)
            {
                _machine.TryTransition(EnlistmentState.EnlistedPlayerCaptive);
            }
            return;
        }

        // BATTLE RE-DERIVATION (#577). EnlistmentRecord.ToPersistedState coerces EnlistedBattle to
        // EnlistedAttached on save, on the stated grounds that battle reality is re-derived at load.
        // Until 2026-09-12 that re-derivation did not exist. A save taken at the encounter menu
        // between the join and the mission, or during the loot and aftermath window, therefore
        // reloaded as an Attached soldier who is still in the map event (or whose battle encounter
        // is still open), and every gate keyed on EnlistedBattle then read the wrong state: the
        // deployment-screen model and the role strip deferred to vanilla for that battle (#576
        // again), and the presence hold in Assess did not fire, so the reconciler parked the party
        // out of its live encounter (#577 again, via reload instead of x64).
        //
        // Matrix rows: Attached + in a map event -> Battle; Attached + a live BATTLE encounter
        // (PlayerEncounter.Battle still set, the aftermath window) -> Battle; Attached + a
        // settlement encounter (#510 opens one on every placement, not a battle) -> unchanged.
        // EnlistedAttached -> EnlistedBattle is the join's own edge, so it is legal here.
        if (record.State == EnlistmentState.EnlistedAttached && IsMidBattle(presence))
        {
            _logger?.LogInfo("[Enlistment] load: the record reads EnlistedAttached but the party is still in its battle (the save coerced EnlistedBattle away); restoring EnlistedBattle before the reconcile (#577)");
            _machine.TryTransition(EnlistmentState.EnlistedBattle);
        }

        // Everything else IS the hourly reconciliation problem — one authority, run now.
        _reconciler.ReconcileHourly(nowDays);
    }

    private bool IsMidBattle(PlayerPresenceSnapshot presence)
    {
        if (presence.IsInMapEvent)
            return true;

        // The encounter's own battle handle outlives the map event through the aftermath
        // (MapEventSide.Clear() nulls MainParty.MapEvent BEFORE the encounter closes), which is
        // exactly the window the presence hold protects. Ownership policy R1b reads the same bit.
        return presence.HasPlayerEncounter && _encounter.GetOwnership(null).IsBattleEncounter;
    }
}
