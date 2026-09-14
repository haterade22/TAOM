using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.PrisonerRecruitment.Models;

public class TaomPrisonerRecruitmentCalculationModel : DefaultPrisonerRecruitmentCalculationModel
{
    private readonly IPrisonerRecruitmentMoraleService _morale;

    public TaomPrisonerRecruitmentCalculationModel(IPrisonerRecruitmentMoraleService morale)
    {
        _morale = morale;
    }

    /// <summary>
    /// Waives the prisoner-recruitment morale penalty when the prisoner is of the recruiter's own
    /// faction or own alignment side — an Isengard captor loses nothing recruiting a Mordor,
    /// Gundabad or Dunland prisoner, since all serve Sauron. Vanilla charges -1 per troop (-2 per
    /// bandit) regardless of who the prisoner is.
    ///
    /// This one override covers all three engine consumers, which each resolve the model through
    /// Campaign.Current.Models: AI recruitment (RecruitPrisonersCampaignBehavior), the player party
    /// screen (PartyScreenLogic), and the UI cost label (PartyCharacterVM) — so no Harmony patch is
    /// needed and the displayed cost cannot desync from the applied one.
    ///
    /// Boundary only: extract ids, single service delegate, base fall-through (gamemodels.md rule 4).
    /// The prisoner's culture is the ONLY faction signal available — CharacterObject exposes no
    /// Clan/Kingdom/IFaction. Keep the static type as CharacterObject: its Culture property is
    /// declared `new`, shadowing BasicCharacterObject.Culture (which returns a BasicCultureObject).
    /// party.LeaderHero is null for garrisons, militia and caravans; those never waive.
    /// </summary>
    public override float GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)
    {
        var leader = party?.LeaderHero;

        return _morale.ShouldWaiveMoralePenalty(
                   leader?.Clan?.Kingdom?.StringId,
                   leader?.Culture?.StringId,
                   character?.Culture?.StringId,
                   leader == Hero.MainHero)
            ? 0f
            : base.GetPrisonerRecruitmentMoraleEffect(party, character, num);
    }
}
