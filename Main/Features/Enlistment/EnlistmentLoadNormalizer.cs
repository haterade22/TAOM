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

        if (!record.IsEnlisted)
        {
            // Matrix row: NotEnlisted (or petition) with a hidden+inactive MainParty and
            // no captivity — an ownerless parked party (foreign save, dropped record) is
            // rescued unconditionally. Captivity legitimately hides the party: not ours.
            if (presence.LooksParked && !presence.IsCaptive)
            {
                _logger?.LogWarning("[Enlistment] load rescue: main party was hidden+inactive with no service record (ownerless) — restoring presence");
                _partyAdapter.RestorePresence();
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

        // Everything else IS the hourly reconciliation problem — one authority, run now.
        _reconciler.ReconcileHourly(nowDays);
    }
}
