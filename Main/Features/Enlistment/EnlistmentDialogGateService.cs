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

    /// <summary>
    /// Classify a leave the COMMANDER GRANTS. Always honourable.
    ///
    /// This used to return Desertion whenever the player left before `ContractEndDay`, and the
    /// contract defaults to 365 days — so every realistic exit was desertion, forfeiting the
    /// player's arrears and calling them a deserter for asking their lord's leave and being
    /// given it. Reported in-game 2026-08-07 after two short services both ended "Desertion".
    ///
    /// The donor mod has no desertion concept for discharge at all. Desertion should mean walking
    /// away WITHOUT asking; every path that reaches this method is the player asking and the
    /// commander agreeing, which is a release, not a betrayal. `DischargeReason.Desertion` is kept
    /// for a genuinely unilateral abandonment path, which no dialog currently offers.
    ///
    /// If an early-exit cost is wanted later, make it a RELATION cost on its own reason — not an
    /// arrears forfeit, which punishes the player with wages the commander already owed them.
    /// </summary>
    public Domain.DischargeReason ClassifyLeaveReason(double nowDays) =>
        Domain.DischargeReason.PlayerRequest;
}
