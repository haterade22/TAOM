using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// Dismissal of a promoted companion back to the ranks (#540). Stateless: every verdict is
/// re-derived from the merit service's promoted-id list and a fresh hero snapshot, so there is
/// nothing to reset between campaigns. Owns its inquiry chain through
/// <see cref="IInquiryPresenterAdapter"/> the way <see cref="FieldCommissionOfferFlowService"/>
/// owns the promotion prompts, so both entry points stay thin.
/// </summary>
public class FieldCommissionDismissService : IFieldCommissionDismissService
{
    private readonly IFieldCommissionMeritService _merit;
    private readonly IHeroCommissionAdapter _heroCommission;
    private readonly ITroopRosterQueryAdapter _roster;
    private readonly IInquiryPresenterAdapter _presenter;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IModLogger _logger;

    public FieldCommissionDismissService(
        IFieldCommissionMeritService merit,
        IHeroCommissionAdapter heroCommission,
        ITroopRosterQueryAdapter roster,
        IInquiryPresenterAdapter presenter,
        IEnlistmentStateQuery enlistment,
        IModLogger logger)
    {
        _merit = merit;
        _heroCommission = heroCommission;
        _roster = roster;
        _presenter = presenter;
        _enlistment = enlistment;
        _logger = logger;
    }

    public DismissCandidate Evaluate(string heroId) => Evaluate(heroId, out _);

    public IReadOnlyList<DismissCandidate> GetDismissableCompanions()
    {
        var result = new List<DismissCandidate>();
        var promoted = _merit.GetPromotedHeroIds();
        if (promoted == null)
            return result;

        foreach (var heroId in promoted)
        {
            var candidate = Evaluate(heroId);
            if (candidate.IsDismissable)
                result.Add(candidate);
        }

        return result;
    }

    public DismissOutcome Dismiss(string heroId) => Dismiss(heroId, out _);

    public DismissOutcome DismissAndReport(string heroId)
    {
        var outcome = Dismiss(heroId, out var candidate);
        if (outcome == DismissOutcome.Ok)
        {
            _presenter.ShowDismissed(candidate.HeroName, candidate.TroopName);
            return outcome;
        }

        _logger?.LogWarning($"[FieldCommission] dismissal of '{heroId}' refused: {outcome}");
        _presenter.ShowDismissFailed(candidate.HeroName);
        return outcome;
    }

    public void OpenDismissPicker()
    {
        var candidates = GetDismissableCompanions();
        if (candidates.Count == 0)
            return;

        _presenter.ShowDismissPicker(candidates, OnPicked);
    }

    private void OnPicked(string heroId)
    {
        // Re-read at pick time, not list time: the list was built when the picker opened and
        // nothing pins the world while it sits there.
        var candidate = Evaluate(heroId);
        if (!candidate.IsDismissable)
        {
            _logger?.LogWarning($"[FieldCommission] '{heroId}' was picked for dismissal but is now {candidate.Outcome}");
            _presenter.ShowDismissFailed(candidate.HeroName);
            return;
        }

        _presenter.ShowDismissConfirm(candidate.HeroName, candidate.TroopName, () => DismissAndReport(heroId), () => { });
    }

    /// <summary>The guards in cheapest-first order; each names the state that stopped it.</summary>
    private DismissCandidate Evaluate(string heroId, out PromotedHeroSnapshot hero)
    {
        hero = PromotedHeroSnapshot.Missing;

        if (string.IsNullOrEmpty(heroId) || _merit.GetPromotedHeroIds()?.Contains(heroId) != true)
            return DismissCandidate.Refused(heroId, null, DismissOutcome.NotPromoted);

        // Symmetric with the offer pump, which sleeps while enlisted (FieldCommissionBehavior.OnTick).
        if (_enlistment.IsEnlisted)
            return DismissCandidate.Refused(heroId, null, DismissOutcome.PlayerEnlisted);

        hero = _heroCommission.GetPromotedHeroSnapshot(heroId);
        if (hero.IsMissing)
            return DismissCandidate.Refused(heroId, null, DismissOutcome.HeroGone);

        if (!hero.IsPlayerCompanion)
            return DismissCandidate.Refused(heroId, hero.Name, DismissOutcome.NotACompanion);

        // Only the main party: a governor, a party or caravan leader, a refuge warden, a prisoner
        // and a fugitive all have somewhere else to be, and the refund goes into THIS roster.
        if (!hero.IsInMainParty || !_roster.HasMainParty)
            return DismissCandidate.Refused(heroId, hero.Name, DismissOutcome.NotInMainParty);

        // KillCharacterAction only marks the hero and defers while the party fights; the adapter
        // refuses that too, but refusing here keeps the option and the line off the screen.
        if (hero.IsPartyInBattle)
            return DismissCandidate.Refused(heroId, hero.Name, DismissOutcome.PartyInBattle);

        var troop = string.IsNullOrEmpty(hero.OriginTroopId) ? TroopInfo.Missing : _roster.GetTroopInfo(hero.OriginTroopId);
        if (troop.IsMissing || troop.IsHero)
            return DismissCandidate.Refused(heroId, hero.Name, DismissOutcome.TroopUnresolved);

        return new DismissCandidate(heroId, hero.Name, troop.StringId, troop.Name, DismissOutcome.Ok);
    }

    private DismissOutcome Dismiss(string heroId, out DismissCandidate candidate)
    {
        candidate = Evaluate(heroId, out var hero);
        if (!candidate.IsDismissable)
            return candidate.Outcome;

        // Remove first. The engine step is the only one with a runtime failure mode (a deferral
        // behind a DeathMark, a throw), and it is detectable in-frame before anything else has
        // moved, so there is nothing to roll back. The refund's own preconditions were checked a
        // moment ago in the same paused frame.
        if (!_heroCommission.RemoveCompanionFromGame(heroId))
        {
            _logger?.LogWarning($"[FieldCommission] the engine declined to remove '{heroId}'; nothing changed");
            return DismissOutcome.RemovalFailed;
        }

        // The hero is gone, so this cannot be undone. It must not pass silently either: what the
        // player would see is a companion who left and no soldier who came back.
        if (!_roster.AddOneToRoster(candidate.TroopId, hero.IsWounded))
            _logger?.LogWarning($"[FieldCommission] dismissed '{heroId}' but the {candidate.TroopId} refund failed; the party is one soldier short");

        _merit.ForgetPromotedHero(heroId);
        _logger?.LogInfo($"[FieldCommission] '{candidate.HeroName}' ({heroId}) returned to the ranks as {candidate.TroopId}{(hero.IsWounded ? ", wounded" : string.Empty)}");
        return DismissOutcome.Ok;
    }
}
