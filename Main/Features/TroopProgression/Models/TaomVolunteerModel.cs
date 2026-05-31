using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomVolunteerModel : DefaultVolunteerModel
{
    private readonly IVolunteerTierService _volunteerTierService;
    private readonly IVolunteerRecruitmentService _recruitmentService;
    private readonly IVolunteerContextAdapter _contextAdapter;
    private readonly ICulturalFeatsService _feats;

    public TaomVolunteerModel(
        IVolunteerTierService volunteerTierService,
        IVolunteerRecruitmentService recruitmentService,
        IVolunteerContextAdapter contextAdapter,
        ICulturalFeatsService feats)
    {
        _volunteerTierService = volunteerTierService;
        _recruitmentService = recruitmentService;
        _contextAdapter = contextAdapter;
        _feats = feats;
    }

    public override int MaxVolunteerTier => _volunteerTierService.MaxVolunteerTier;

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
    /// settlement tick. We apply per-culture respawn-rate feats keyed on the SETTLEMENT'S
    /// owning clan culture (matches <c>TaomSettlementMilitiaModel</c>): a Mordor village
    /// produces +20% volunteers while Mordor owns it; conquest by another culture removes the
    /// bonus on the next daily tick. Clamped to [0,1] — vanilla's <c>MBRandom.RandomFloat &lt; p</c>
    /// check is robust to p&gt;1 but the clamp keeps the value semantically a probability.
    /// </summary>
    public override float GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)
    {
        float baseProb = base.GetDailyVolunteerProductionProbability(hero, index, settlement);
        var culture = CultureFeatAdapter.FromOrNull(settlement?.OwnerClan?.Culture);
        if (culture == null)
            return baseProb;
        var result = new ExplainedNumber(baseProb);
        _feats.ApplyVolunteerRespawnFeats(culture, ref result);
        return MathF.Clamp(result.ResultNumber, 0f, 1f);
    }
}
