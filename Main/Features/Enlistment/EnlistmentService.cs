using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentService : IEnlistmentService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IDischargeService _discharge;
    private readonly ICommanderLordAdapter _commander;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IEncounterAdapter _encounter;
    private readonly IEncounterOwnershipPolicy _ownership;
    private readonly IEnlistmentConfigProvider _config;
    private readonly IServiceDiplomacyService _diplomacy;
    private readonly IModLogger _logger;

    public EnlistmentService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IDischargeService discharge,
        ICommanderLordAdapter commander,
        IMobilePartyAttachmentAdapter attachment,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        IEnlistmentConfigProvider config,
        IServiceDiplomacyService diplomacy,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _discharge = discharge;
        _commander = commander;
        _attachment = attachment;
        _encounter = encounter;
        _ownership = ownership;
        _config = config;
        _diplomacy = diplomacy;
        _logger = logger;
    }

    public event Action<string> EnlistmentStarted;

    public PetitionResult SubmitPetition(string commanderHeroId)
    {
        if (_store.Record.IsEnlisted)
            return PetitionResult.AlreadyEnlisted;
        if (string.IsNullOrEmpty(commanderHeroId))
            return PetitionResult.InvalidCommander;

        if (!IsCommanderFit(commanderHeroId))
            return PetitionResult.CommanderUnavailable;

        _store.Record.PetitionCommanderId = commanderHeroId;
        if (_store.Record.State == EnlistmentState.NotEnlisted)
            _machine.TryTransition(EnlistmentState.PetitionPending);

        _logger?.LogInfo($"[Enlistment] petition submitted to {commanderHeroId}");
        return PetitionResult.Accepted;
    }

    public bool WithdrawPetition()
    {
        if (_store.Record.State != EnlistmentState.PetitionPending)
            return false;

        _machine.TryTransition(EnlistmentState.NotEnlisted);
        _store.Record.PetitionCommanderId = null;
        return true;
    }

    public OathResult CompleteOath(string commanderHeroId, string enlistedHeroId, double nowDays)
    {
        if (_store.Record.State != EnlistmentState.PetitionPending
            || _store.Record.PetitionCommanderId != commanderHeroId)
        {
            return OathResult.NoMatchingPetition;
        }

        if (!IsCommanderFit(commanderHeroId))
            return OathResult.CommanderUnavailable;

        _store.Record.EnlistedHeroId = enlistedHeroId;
        _store.Record.CommanderHeroId = commanderHeroId;
        _store.Record.EnlistedAtDay = nowDays;
        _store.Record.ContractEndDay = nowDays + _config.GetConfig().ContractDays;
        _store.Record.PetitionCommanderId = null;
        _machine.TryTransition(EnlistmentState.EnlistedAttached);

        // The oath is sworn inside a conversation, which runs inside a PlayerEncounter. Parking
        // without closing it leaves PlayerEncounter.Current live for the whole term, and
        // EncounterManager refuses EVERY main-party encounter while that is set — so the player
        // cannot click a lord or settlement again, and the state survives into discharge.
        // Observed in-game 2026-08-07: playerEncounter=True on 93 of 93 ticks. The donor closes it
        // here too (RFEnlistmentCampaignBehavior.FinalizeEnlistmentConversation: Finish() then
        // attach) — ordering matters, the encounter must go before the park. The ownership policy
        // decides WHETHER it is ours: a settlement visit or someone else's business is left alone.
        var commanderPartyId = _commander.GetSnapshot(commanderHeroId).PartyId;
        var verdict = _ownership.Evaluate(EncounterFinishIntent.OathHandoff, _encounter.GetOwnership(commanderPartyId));
        if (verdict == EncounterFinishVerdict.Finish)
        {
            // forcePlayerOutFromSettlement: true — leaving CurrentSettlement set would keep the
            // player settlement-bound, and EncounterManager refuses every main-party encounter
            // while that is so.
            if (_encounter.Finish(true))
                _logger?.LogInfo($"[EnlistDiag] closed the enlistment conversation's PlayerEncounter before parking on {commanderHeroId}");
            else
                _logger?.LogError("[EnlistDiag] could not close the enlistment conversation's PlayerEncounter — the player may be unable to interact with anything while enlisted");
        }
        else if (verdict != EncounterFinishVerdict.NothingToFinish)
        {
            _logger?.LogInfo($"[EnlistDiag] oath left the live encounter alone: {verdict}");
        }

        if (!_attachment.ParkNear(commanderHeroId))
            _logger?.LogWarning($"[Enlistment] ParkNear failed right after the oath with {commanderHeroId} — reconciler will re-park");

        // Mirror the commander's wars BEFORE announcing the oath, so any subscriber that reads
        // diplomatic state sees the finished picture rather than a half-applied one.
        _diplomacy.ApplyServiceWars(commanderHeroId);

        try
        {
            EnlistmentStarted?.Invoke(commanderHeroId);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] EnlistmentStarted subscriber threw: {ex.Message}");
        }

        _logger?.LogInfo($"[Enlistment] oath sworn to {commanderHeroId}, contract ends day {_store.Record.ContractEndDay:F1}");
        return OathResult.Sworn;
    }

    public bool RequestDischarge(DischargeReason reason) => _discharge.Execute(reason);

    private bool IsCommanderFit(string commanderHeroId)
    {
        var snapshot = _commander.GetSnapshot(commanderHeroId);
        return snapshot.Exists && snapshot.IsAlive && !snapshot.IsPrisoner
            && snapshot.HasParty && snapshot.PartyIsActive;
    }
}
