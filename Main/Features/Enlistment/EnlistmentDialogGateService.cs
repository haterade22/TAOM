using TAOM.Adapters;

namespace TAOM.Features.Enlistment;

public class EnlistmentDialogGateService : IEnlistmentDialogGateService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerContextAdapter _playerContext;
    private readonly IEnlistmentConfigProvider _config;

    public EnlistmentDialogGateService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IPlayerContextAdapter playerContext,
        IEnlistmentConfigProvider config)
    {
        _store = store;
        _commander = commander;
        _playerContext = playerContext;
        _config = config;
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

    /// <summary>
    /// Every branch here is written to fall OPEN. The failure this gate can cause is a player
    /// locked in service with no exit — strictly worse than releasing someone a day early — so
    /// missing data, a poisoned config and a NaN clock all grant the release.
    /// </summary>
    public Domain.ReleaseRequest EvaluateReleaseRequest(double nowDays)
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return Domain.ReleaseRequest.Granted;

        // Outranks the term: walking off mid-engagement is not a paperwork question. Deliberately
        // a refusal of the MOMENT, not of the request — the player is told to ask again after.
        if (record.State == Domain.EnlistmentState.EnlistedBattle)
            return Domain.ReleaseRequest.RefusedInBattle;

        var minimum = _config?.GetConfig()?.MinimumServiceDays ?? 0.0;

        // Positive requirement, per the NaN-gate rule: we refuse only when we can PROVE a debt.
        // Written as `<= 0 || !finite -> granted` rather than `> 0 -> refuse` so every
        // non-finite value lands on the safe side without a separate guard.
        if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum <= 0.0)
            return Domain.ReleaseRequest.Granted;

        // No recorded start day cannot prove a debt either.
        if (record.EnlistedAtDay == null)
            return Domain.ReleaseRequest.Granted;

        var served = nowDays - record.EnlistedAtDay.Value;

        // NaN served => `served < minimum` is false => granted. That is the intent, not an
        // accident: do not rewrite this as `if (served >= minimum) return Granted;`.
        if (!(served < minimum))
            return Domain.ReleaseRequest.Granted;

        return Domain.ReleaseRequest.TooSoon((int)System.Math.Ceiling(minimum - served));
    }
}
