using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomVolunteerModel : DefaultVolunteerModel
{
    private readonly IVolunteerTierService _volunteerTierService;
    private readonly IVolunteerRecruitmentService _recruitmentService;
    private readonly IVolunteerContextAdapter _contextAdapter;
    private readonly VolunteerProductionService _volunteerProduction;
    private readonly IRecruitmentAlignmentService _recruitmentAlignment;
    private readonly ICastleRecruitmentService _castleRecruitment;
    private readonly IPlayerControlQuery _players;

    // The query is resolved here, not as a constructor parameter: that would need an edit to SubModule.cs, where this
    // model is built.
    public TaomVolunteerModel(
        IVolunteerTierService volunteerTierService,
        IVolunteerRecruitmentService recruitmentService,
        IVolunteerContextAdapter contextAdapter,
        VolunteerProductionService volunteerProduction,
        IRecruitmentAlignmentService recruitmentAlignment,
        ICastleRecruitmentService castleRecruitment)
        : this(volunteerTierService, recruitmentService, contextAdapter, volunteerProduction, recruitmentAlignment,
            castleRecruitment, IoC.Resolve<IPlayerControlQuery>())
    {
    }

    internal TaomVolunteerModel(
        IVolunteerTierService volunteerTierService,
        IVolunteerRecruitmentService recruitmentService,
        IVolunteerContextAdapter contextAdapter,
        VolunteerProductionService volunteerProduction,
        IRecruitmentAlignmentService recruitmentAlignment,
        ICastleRecruitmentService castleRecruitment,
        IPlayerControlQuery players)
    {
        _castleRecruitment = castleRecruitment;
        _players = players;
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
    /// notable" signal, as when a negative relation and a war stack) blocks the whole source. We gate on the
    /// RECRUITER's kingdom alignment vs the SOURCE settlement's controlling-kingdom alignment — both
    /// keyed by kingdom StringId (the keys in alignment.json), the same disambiguation
    /// <c>TaomTargetScoreModel</c> uses (MapFaction.StringId, not Culture, so empire_w/empire_s split).
    /// Two decisions live in services: <see cref="ICastleRecruitmentService.BlocksAiRecruitAtCastle"/>
    /// (checked first, #918: an AI lord buying from a castle notable gets -1 while AI castle recruitment
    /// is off, on entry and hourly alike) and <see cref="IRecruitmentAlignmentService.IsRecruitmentBlocked"/>.
    /// The player's own hero is never blocked by the castle rule; companion-led clan parties count as AI
    /// lords. <c>Patch114_AiVolunteerEstimate_Postfix</c> (#887) is the twin that extracts the same keys for AI trip
    /// scoring; change both together. This is a boundary that extracts ids + falls through to base (gamemodels.md rule 4).
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
    /// settlement tick. <see cref="VolunteerProductionService"/> applies the per-culture
    /// respawn-rate feats keyed on the SETTLEMENT'S owning clan culture (matches
    /// <c>TaomSettlementMilitiaModel</c>) and then the War of the Ring volunteer effect of the
    /// owning clan's kingdom (#765). A settlement of the player's own clan keeps its feats but gets no
    /// war step, so its settlements' production is never changed by an effect (decision D4). In a co-op
    /// session every human player's clan counts (#1141): the daily tick runs on the server, where
    /// <c>Clan.PlayerClan</c> is the server's own clan. This body only extracts the owner's facts at the
    /// boundary (gamemodels.md rule 4).
    /// </summary>
    public override float GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)
        => _volunteerProduction.Compute(
            base.GetDailyVolunteerProductionProbability(hero, index, settlement),
            CultureFeatAdapter.FromOrNull(settlement?.OwnerClan?.Culture),
            settlement?.OwnerClan?.Kingdom?.StringId,
            PlayerClanCheck.IsAPlayersClan(settlement?.OwnerClan, _players));
}
