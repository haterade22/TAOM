using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class DischargeService : IDischargeService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IEncounterAdapter _encounter;
    private readonly IModLogger _logger;

    public DischargeService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IMobilePartyAttachmentAdapter attachment,
        IEncounterAdapter encounter,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
        _encounter = encounter;
        _logger = logger;
    }

    public event Action<DischargeReason> EnlistmentEnded;

    public bool Execute(DischargeReason reason)
    {
        if (!_store.Record.IsEnlisted)
        {
            _logger?.LogWarning($"DischargeService: Execute({reason}) ignored — not enlisted (state {_store.Record.State})");
            return false;
        }

        if (!_machine.TryTransition(EnlistmentState.Discharging))
        {
            // Cannot happen for enlisted-family states per the transition table; if it
            // ever does, the pipeline still runs — presence restoration outranks purity.
            _logger?.LogError($"DischargeService: transition to Discharging failed from {_store.Record.State}; forcing pipeline");
        }

        var before = _attachment.GetPresence();
        _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) begin | {before?.Describe() ?? "presence unavailable"}");

        if (!_attachment.RestorePresence())
            _logger?.LogError($"DischargeService: RestorePresence failed during discharge ({reason}) — continuing pipeline");

        // Restoring IsActive/IsVisible is not enough to hand the player back. EncounterManager
        // refuses to start ANY encounter for the main party while a PlayerEncounter is live or a
        // MapEventSide is attached, so a discharge that leaves either set silently ends the
        // player's ability to talk to anyone, permanently, for that save. Reported in-game
        // 2026-08-07: "cannot click on a lord after leaving the service of another."
        if (_encounter.HasCurrent)
        {
            _logger?.LogWarning($"[EnlistDiag] DISCHARGE({reason}) found a live PlayerEncounter — finishing it, otherwise the player could never interact again");
            if (!_encounter.Finish(false))
                _logger?.LogError($"[EnlistDiag] DISCHARGE({reason}) could not finish the lingering PlayerEncounter — the player may be unable to talk to anyone");
        }

        if (!_machine.TryTransition(EnlistmentState.NotEnlisted))
            _store.Record.State = EnlistmentState.NotEnlisted;

        _store.Record.Reset();

        try
        {
            EnlistmentEnded?.Invoke(reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"DischargeService: EnlistmentEnded subscriber threw for {reason}: {ex.Message}");
        }

        var after = _attachment.GetPresence();
        _logger?.LogInfo($"[Enlistment] service ended: {reason}");
        _logger?.LogInfo($"[EnlistDiag] DISCHARGE({reason}) end | {after?.Describe() ?? "presence unavailable"}");

        // The check that matters: if this fires, the player is out of service but cannot interact
        // with anything, and the save is effectively broken for them.
        if (after != null && after.EncountersBlocked)
        {
            _logger?.LogError(
                $"[EnlistDiag] DISCHARGE({reason}) LEFT THE PLAYER UNABLE TO START ENCOUNTERS — {after?.Describe() ?? "presence unavailable"}. " +
                "They will not be able to click lords or settlements. This is a bug; report this line.");
        }

        return true;
    }
}
