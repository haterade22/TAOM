using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomVolunteerModel : DefaultVolunteerModel
{
    private readonly IVolunteerTierService _volunteerTierService;
    private readonly IVolunteerRecruitmentService _recruitmentService;
    private readonly IVolunteerContextAdapter _contextAdapter;
    private readonly VolunteerProductionService _volunteerProduction;
    private readonly IRecruitmentAlignmentService _recruitmentAlignment;

    public TaomVolunteerModel(
        IVolunteerTierService volunteerTierService,
        IVolunteerRecruitmentService recruitmentService,
        IVolunteerContextAdapter contextAdapter,
        VolunteerProductionService volunteerProduction,
        IRecruitmentAlignmentService recruitmentAlignment)
    {
        _volunteerTierService = volunteerTierService;
        _recruitmentService = recruitmentService;
        _contextAdapter = contextAdapter;
        _volunteerProduction = volunteerProduction;
        _recruitmentAlignment = recruitmentAlignment;
    }

    public override int MaxVolunteerTier => _volunteerTierService.MaxVolunteerTier;

    /// <summary>
    /// Alignment-gated recruitment. Both the player recruit UI and AI lords clamp their recruitable
    /// volunteer slots to this index; returning -1 (the engine's own "recruit nothing from this
    /// notable" signal, as it does for negative relation) blocks the whole source. We gate on the
    /// RECRUITER's kingdom alignment vs the SOURCE settlement's controlling-kingdom alignment — both
    /// keyed by kingdom StringId (the keys in alignment.json), the same disambiguation
    /// <c>TaomTargetScoreModel</c> uses (MapFaction.StringId, not Culture, so empire_w/empire_s split).
    /// Decision lives entirely in <see cref="IRecruitmentAlignmentService"/>; this is a boundary that
    /// extracts ids + falls through to base (gamemodels.md rule 4).
    /// </summary>
    public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)
    {
        var recruiterKingdomId = buyerHero?.Clan?.Kingdom?.StringId;
        var sourceKingdomId = sellerHero?.CurrentSettlement?.MapFaction?.StringId;
        var isPlayer = buyerHero == Hero.MainHero;
        return _recruitmentAlignment.IsRecruitmentBlocked(recruiterKingdomId, sourceKingdomId, isPlayer)
            ? -1
            : base.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation);
    }

    public override CharacterObject GetBasicVolunteer(Hero sellerHero)
    {
        var context = _contextAdapter.GetContext(sellerHero);
        var troopId = _recruitmentService.GetVolunteerTroopId(context);

        if (troopId != null)
        {
            var character = _contextAdapter.ResolveCharacter(troopId);
            if (character != null)
                return character;
        }

        return base.GetBasicVolunteer(sellerHero);
    }

    /// <summary>
    /// Vanilla returns a per-notable per-slot probability used by
    /// <c>RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement</c> on the daily
    /// settlement tick. <see cref="VolunteerProductionService"/> applies the per-culture
    /// respawn-rate feats keyed on the SETTLEMENT'S owning clan culture (matches
    /// <c>TaomSettlementMilitiaModel</c>) and then the War of the Ring volunteer effect of the
    /// owning clan's kingdom (#765). A settlement of the player's own clan keeps its feats but gets no
    /// war step, so its settlements' production is never changed by an effect (decision D4). This body
    /// only extracts the owner's facts at the boundary (gamemodels.md rule 4); the only engine caller
    /// runs inside a campaign, so <c>Clan.PlayerClan</c> is safe to read here.
    /// </summary>
    public override float GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)
        => _volunteerProduction.Compute(
            base.GetDailyVolunteerProductionProbability(hero, index, settlement),
            CultureFeatAdapter.FromOrNull(settlement?.OwnerClan?.Culture),
            settlement?.OwnerClan?.Kingdom?.StringId,
            settlement?.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan);
}
