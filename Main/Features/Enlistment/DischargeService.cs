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
    private readonly IModLogger _logger;

    public DischargeService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IMobilePartyAttachmentAdapter attachment,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
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

        if (!_attachment.RestorePresence())
            _logger?.LogError($"DischargeService: RestorePresence failed during discharge ({reason}) — continuing pipeline");

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

        _logger?.LogInfo($"[Enlistment] service ended: {reason}");
        return true;
    }
}
