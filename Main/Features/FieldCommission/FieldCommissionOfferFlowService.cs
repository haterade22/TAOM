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
        _presenter.ShowPromotionOffer(offer.TroopName, () => OnAccepted(offer), () => Close());
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

        var config = _configProvider.GetConfig();
        var info = _roster.GetTroopInfo(offer.TroopId);
        var troopSkills = _roster.GetTroopSkillValues(offer.TroopId);
        var plan = _skillBudget.Compute(info.Level, troopSkills, config.SkillPointsPerLevel, MaxSkillValue);

        var heroId = _heroCommission.CreateCompanionFromTroop(offer.TroopId, plan, name);
        if (string.IsNullOrEmpty(heroId))
        {
            // Race condition (troop id vanished from the roster between offer and completion, or
            // the adapter otherwise couldn't build the hero) — never partially apply: no roster
            // removal, no merit deduction, no promoted-hero record.
            _logger?.LogWarning($"[FieldCommission] promotion completion failed for troop {offer.TroopId} — merit and roster left untouched");
            Close();
            return;
        }

        _roster.RemoveOneFromRoster(offer.TroopId);
        _merit.CompleteOffer(offer.TroopId);
        _merit.RecordPromotedHero(heroId);
        Close();
    }

    private void Close() => IsShowingOffer = false;
}
