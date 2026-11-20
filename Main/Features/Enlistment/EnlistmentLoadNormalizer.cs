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
    private readonly IModLogger _logger;

    public EnlistmentLoadNormalizer(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IEnlistmentReconciler reconciler,
        IMobilePartyAttachmentAdapter partyAdapter,
        IDischargeService discharge,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _reconciler = reconciler;
        _partyAdapter = partyAdapter;
        _discharge = discharge;
        _logger = logger;
    }

    public void Normalize(string currentMainHeroId, double nowDays)
    {
        var record = _store.Record;
        var presence = _partyAdapter.GetPresence();
        _store.HeirDischargeHeld = false;

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
        // is no longer the player. Quiet, penalty-free discharge. A save made mid-battle waits
        // (#1030): the discharge's RestorePresence clears AttachedTo, which pulls a merged soldier
        // out of his live battle. The rest of the load runs as usual, and the hourly tick
        // discharges once the battle is over.
        if (IsHeirMismatch(record, currentMainHeroId))
        {
            if (!presence.IsInMapEvent)
            {
                DischargeHeirMismatch(record, currentMainHeroId);
                return;
            }

            _store.HeirDischargeHeld = true;
            _logger?.LogInfo($"[Enlistment] enlisted hero '{record.EnlistedHeroId}' is no longer the player ('{currentMainHeroId}'), but the party is in its battle: the quiet discharge waits for the battle to end (#1030)");
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
        // reloaded as an Attached soldier who is still in the map event, and every gate keyed on
        // EnlistedBattle then read the wrong state: the deployment-screen model and the role strip
        // deferred to vanilla for that battle (#576 again), and the presence hold in Assess did not
        // fire, so the reconciler parked the party out of its live encounter (#577 again, via reload
        // instead of x64).
        //
        // Matrix rows: Attached + in a map event -> Battle; Attached + an encounter with no map
        // event behind it (it outlived its battle) -> unchanged: the sweep does not own a leftover
        // whose encountered party is a mobile opposing leader outside a settlement (R4 returns
        // SkipNotOurs), so it stays until a discharge or the stale-latch recovery closes it. Since
        // #1021 the recovery does: an Attached soldier outside a settlement whose encounter is a
        // joined battle is a timed latch episode too. Attached + a settlement encounter (#510 opens one on every placement, not a
        // battle) -> unchanged.
        // EnlistedAttached -> EnlistedBattle is the join's own edge, so it is legal here.
        if (record.State == EnlistmentState.EnlistedAttached && presence.IsInMapEvent)
        {
            _logger?.LogInfo("[Enlistment] load: the record reads EnlistedAttached but the party is still in its battle (the save coerced EnlistedBattle away); restoring EnlistedBattle before the reconcile (#577)");
            _machine.TryTransition(EnlistmentState.EnlistedBattle);
        }

        // Everything else IS the hourly reconciliation problem — one authority, run now.
        _reconciler.ReconcileHourly(nowDays);
    }

    public bool DischargeHeirMismatchOutsideBattle(string currentMainHeroId)
    {
        if (!_store.HeirDischargeHeld)
            return false;
        var record = _store.Record;
        if (!record.IsEnlisted || !IsHeirMismatch(record, currentMainHeroId))
        {
            _store.HeirDischargeHeld = false;
            return false;
        }

        // Captivity after a lost battle waits too: vanilla keeps a captive's party inactive, and the
        // discharge's RestorePresence would make it active (the crown-war release waits the same way).
        var presence = _partyAdapter.GetPresence();
        if (presence.IsInMapEvent || presence.IsCaptive)
            return false;

        _store.HeirDischargeHeld = false;
        DischargeHeirMismatch(record, currentMainHeroId);
        return true;
    }

    private static bool IsHeirMismatch(EnlistmentRecord record, string currentMainHeroId) =>
        !string.IsNullOrEmpty(record.EnlistedHeroId)
        && !string.IsNullOrEmpty(currentMainHeroId)
        && record.EnlistedHeroId != currentMainHeroId;

    private void DischargeHeirMismatch(EnlistmentRecord record, string currentMainHeroId)
    {
        _logger?.LogInfo($"[Enlistment] enlisted hero '{record.EnlistedHeroId}' is no longer the player ('{currentMainHeroId}') — quiet discharge");
        _discharge.Execute(DischargeReason.HeirSuccessionOrPossessionMismatch);
    }
}
