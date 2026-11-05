using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomVolunteerModel : DefaultVolunteerModel
{
    private readonly IVolunteerTierService _volunteerTierService;
    private readonly IVolunteerRecruitmentService _recruitmentService;
    private readonly IVolunteerContextAdapter _contextAdapter;
    private readonly ICulturalFeatsService _feats;
    private readonly IRecruitmentAlignmentService _recruitmentAlignment;
    private readonly ICastleRecruitmentService _castleRecruitment;

    public TaomVolunteerModel(
        IVolunteerTierService volunteerTierService,
        IVolunteerRecruitmentService recruitmentService,
        IVolunteerContextAdapter contextAdapter,
        ICulturalFeatsService feats,
        IRecruitmentAlignmentService recruitmentAlignment,
        ICastleRecruitmentService castleRecruitment)
    {
        _volunteerTierService = volunteerTierService;
        _recruitmentService = recruitmentService;
        _contextAdapter = contextAdapter;
        _feats = feats;
        _recruitmentAlignment = recruitmentAlignment;
        _castleRecruitment = castleRecruitment;
    }

    public override int MaxVolunteerTier => _volunteerTierService.MaxVolunteerTier;

    /// <summary>
    /// Alignment-gated recruitment. Both the player recruit UI and AI lords clamp their recruitable
    /// volunteer slots to this index; returning -1 (the engine's own "recruit nothing from this
    /// notable" signal, as when a negative relation and a war stack) blocks the whole source. We gate on the
    /// RECRUITER's kingdom alignment vs the SOURCE settlement's controlling-kingdom alignment — both
    /// keyed by kingdom StringId (the keys in alignment.json), the same disambiguation
    /// <c>TaomTargetScoreModel</c> uses (MapFaction.StringId, not Culture, so empire_w/empire_s split).
    /// Two decisions live in services: <see cref="ICastleRecruitmentService.BlocksAiRecruitAtCastle"/>
    /// (checked first, #918: an AI lord buying from a castle notable gets -1 while AI castle recruitment
    /// is off, on entry and hourly alike) and <see cref="IRecruitmentAlignmentService.IsRecruitmentBlocked"/>.
    /// The player's own hero is never blocked by the castle rule; companion-led clan parties count as AI
    /// lords. This is a boundary that extracts ids + falls through to base (gamemodels.md rule 4).
    /// </summary>
    public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)
    {
        var sellerSettlement = sellerHero?.CurrentSettlement;
        var isPlayer = buyerHero == Hero.MainHero;
        return _castleRecruitment.BlocksAiRecruitAtCastle(sellerInCastle: sellerSettlement?.IsCastle == true, buyerIsPlayer: isPlayer)
               || _recruitmentAlignment.IsRecruitmentBlocked(buyerHero?.Clan?.Kingdom?.StringId, sellerSettlement?.MapFaction?.StringId, isPlayer)
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
