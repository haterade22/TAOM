using TAOM.Adapters;

namespace TAOM.Features.Enlistment;

public class EnlistmentDialogGateService : IEnlistmentDialogGateService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerContextAdapter _playerContext;

    public EnlistmentDialogGateService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IPlayerContextAdapter playerContext)
    {
        _store = store;
        _commander = commander;
        _playerContext = playerContext;
    }

    public EnlistGateResult CanEnlistWith(string partnerHeroId)
    {
        if (_store.Record.IsEnlisted)
            return EnlistGateResult.AlreadyEnlisted;
        if (string.IsNullOrEmpty(partnerHeroId) || !_commander.IsLord(partnerHeroId))
            return EnlistGateResult.NotALord;
        if (_playerContext.IsUnderMercenaryService())
            return EnlistGateResult.UnderMercenaryContract;

        var snapshot = _commander.GetSnapshot(partnerHeroId);
        if (!snapshot.Exists || !snapshot.IsAlive || snapshot.IsPrisoner
            || !snapshot.HasParty || !snapshot.PartyIsActive)
        {
            return EnlistGateResult.CommanderUnavailable;
        }

        // Enlisting means fighting the commander's wars — never against your own crown.
        var playerKingdomId = _playerContext.GetPlayerKingdomId();
        if (!string.IsNullOrEmpty(playerKingdomId)
            && _commander.IsAtWarWithFaction(partnerHeroId, playerKingdomId))
        {
            return EnlistGateResult.AtWarWithYourKingdom;
        }

        return EnlistGateResult.Ok;
    }

    public bool CanRequestDischargeFrom(string partnerHeroId)
    {
        return _store.Record.IsEnlisted
            && !string.IsNullOrEmpty(partnerHeroId)
            && _store.Record.CommanderHeroId == partnerHeroId;
    }

    public Domain.DischargeReason ClassifyLeaveReason(double nowDays)
    {
        var contractEnd = _store.Record.ContractEndDay;
        // Positive-requirement polarity: a NaN nowDays fails the comparison and classifies
        // as desertion — the strict outcome, never the generous one.
        if (!contractEnd.HasValue || nowDays >= contractEnd.Value)
            return Domain.DischargeReason.PlayerRequest;
        return Domain.DischargeReason.Desertion;
    }
}
