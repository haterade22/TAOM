using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

public class FieldCommissionOfferFlowService : IFieldCommissionOfferFlowService
{
    private readonly IFieldCommissionMeritService _merit;
    private readonly ITroopRosterQueryAdapter _roster;
    private readonly IHeroCommissionAdapter _heroCommission;
    private readonly IInquiryPresenterAdapter _presenter;
    private readonly IFieldCommissionConfigProvider _configProvider;
    private readonly CommissionSkillBudget _skillBudget;
    private readonly IModLogger _logger;

    private const int MaxSkillValue = 300;

    public FieldCommissionOfferFlowService(
        IFieldCommissionMeritService merit,
        ITroopRosterQueryAdapter roster,
        IHeroCommissionAdapter heroCommission,
        IInquiryPresenterAdapter presenter,
        IFieldCommissionConfigProvider configProvider,
        CommissionSkillBudget skillBudget,
        IModLogger logger)
    {
        _merit = merit;
        _roster = roster;
        _heroCommission = heroCommission;
        _presenter = presenter;
        _configProvider = configProvider;
        _skillBudget = skillBudget;
        _logger = logger;
    }

    public bool IsShowingOffer { get; private set; }

    public void PumpNextOffer()
    {
        if (IsShowingOffer)
            return;

        if (!_merit.TryDequeueOffer(out var offer) || offer == null)
            return;

        IsShowingOffer = true;
        _presenter.ShowPromotionOffer(offer.TroopName, () => OnAccepted(offer), () => OnDeclined(offer));
    }

    private void OnDeclined(PendingPromotionOffer offer)
    {
        // Merit is untouched (bug fix (a)) — but the refusal is remembered, so this troop type has to
        // distinguish itself again before it asks a second time. Otherwise the bank still clears the
        // threshold after the next won battle and the same prompt returns, indefinitely.
        _merit.RecordDeclinedOffer(offer.TroopId);
        Close();
    }

    private void OnAccepted(PendingPromotionOffer offer)
    {
        var room = _heroCommission.GetCompanionRoomInfo();
        var allowance = _configProvider.GetConfig().RetainerAllowance;

        if (!room.HasRoom(allowance))
        {
            // Bug-fix (a) corollary: merit is untouched here — the offer simply doesn't complete
            // this cycle. The NEXT won+eligible battle recomputes available merit against the
            // then-current roster and requeues fresh offers, so nothing is lost by deferring.
            _presenter.ShowNoCompanionRoom(offer.TroopName, Close);
            return;
        }

        _presenter.ShowRenamePrompt(offer.TroopName, name => OnRenamed(offer, name), () => OnRenamed(offer, null));
    }

    private void OnRenamed(PendingPromotionOffer offer, string chosenName)
    {
        var name = string.IsNullOrWhiteSpace(chosenName) ? offer.TroopName : chosenName.Trim();

        if (Promote(offer.TroopId, name) != null)
            _merit.CompleteOffer(offer.TroopId);
        Close();
    }

    public string PromoteNow(string troopId, string chosenName) =>
        _merit.CanPromote(troopId) ? Promote(troopId, chosenName) : null;

    private string Promote(string troopId, string chosenName)
    {
        var config = _configProvider.GetConfig();
        var info = _roster.GetTroopInfo(troopId);
        var troopSkills = _roster.GetTroopSkillValues(troopId);
        var plan = _skillBudget.Compute(info.Level, troopSkills, config.SkillPointsPerLevel, MaxSkillValue);

        var heroId = _heroCommission.CreateCompanionFromTroop(troopId, plan, chosenName);
        if (string.IsNullOrEmpty(heroId))
        {
            // Race condition (troop id vanished from the roster between offer and completion, or
            // the adapter otherwise couldn't build the hero) — never partially apply: no roster
            // removal, no merit deduction, no promoted-hero record.
            _logger?.LogWarning($"[FieldCommission] promotion completion failed for troop {troopId} — merit and roster left untouched");
            return null;
        }

        // The adapter refuses to build a hero unless the troop is still in the roster, so a false
        // here means the roster changed underneath a completed promotion. Nothing can be rolled back
        // at this point — the hero exists — but it must not pass silently, because the symptom
        // players see is a companion who appeared without a soldier disappearing.
        if (!_roster.RemoveOneFromRoster(troopId))
            _logger?.LogWarning($"[FieldCommission] promoted {troopId} but the roster decrement failed — party count is now one too high");

        _merit.RecordPromotedHero(heroId);
        return heroId;
    }

    public void Reset() => IsShowingOffer = false;

    private void Close() => IsShowingOffer = false;
}
