using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.PrisonerRecruitment.Models;

public class TaomPrisonerRecruitmentCalculationModel : DefaultPrisonerRecruitmentCalculationModel
{
    private readonly IPrisonerRecruitmentMoraleService _morale;
    private readonly ICareerPassiveService _careerPassives;
    private readonly IDesertionRecruitGuard _desertionGuard;

    public TaomPrisonerRecruitmentCalculationModel(IPrisonerRecruitmentMoraleService morale, ICareerPassiveService careerPassives,
        IDesertionRecruitGuard desertionGuard)
    {
        _morale = morale;
        _careerPassives = careerPassives;
        _desertionGuard = desertionGuard;
    }

    /// <summary>
    /// A prisoner that alignment desertion would take from this party the next day is not recruitable: AI parties
    /// (vanilla <c>RecruitPrisonersCampaignBehavior</c>) stop paying morale for troops that leave again. Applies to
    /// lord parties and the main party only, under the same owner and location switches as the desertion itself.
    /// </summary>
    public override bool IsPrisonerRecruitable(PartyBase party, CharacterObject character, out int conformityNeeded)
        => base.IsPrisonerRecruitable(party, character, out conformityNeeded)
           && !_desertionGuard.WouldDesertFromParty(party, character);

    /// <summary>
    /// The count the party screen and the recruit ticks use: 0 for a prisoner that would desert, so the player's
    /// party screen offers no recruit either.
    /// </summary>
    public override int CalculateRecruitableNumber(PartyBase party, CharacterObject character)
        => _desertionGuard.WouldDesertFromParty(party, character) ? 0 : base.CalculateRecruitableNumber(party, character);

    /// <summary>
    /// Career PrisonerConversion (#769 E11): the prisoner XP a recruiting party earns per hour, vanilla's answer
    /// first (10 + Leadership * 0.05, the Leadership perks, and Promises for a bandit troop), then the leader's factor, keyed on the party's own
    /// LeaderHero because the engine reads Leadership off the same hero. The only caller is
    /// RecruitPrisonersCampaignBehavior.cs:100 (v1.5.5), which multiplies the rounded result by the hours elapsed and
    /// feeds one prisoner stack per tick (the main party hourly, an AI party daily, so 24 hours at once); it passes
    /// the recruiting party, never null.
    /// </summary>
    public override ExplainedNumber GetConformityChangePerHour(PartyBase party, CharacterObject troopToBoost)
    {
        var result = base.GetConformityChangePerHour(party, troopToBoost);
        _careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PrisonerConversion);
        return result;
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
                   FactionSides.OfHero(leader),
                   leader?.Culture?.StringId,
                   character?.Culture?.StringId,
                   leader == Hero.MainHero)
            ? 0f
            : base.GetPrisonerRecruitmentMoraleEffect(party, character, num);
    }
}
