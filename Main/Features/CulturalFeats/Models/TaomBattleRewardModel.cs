using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CreatureBandits.Hooks;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBattleRewardModel : DefaultBattleRewardModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomBattleRewardModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateRenownGain(
        PartyBase winnerParty,
        float renownValueOfBattleForWinnerSide,
        float contributionShareOfWinnerParty,
        float renownMultiplierForWinnerSide,
        bool includeDescriptions)
    {
        var result = base.CalculateRenownGain(
            winnerParty,
            renownValueOfBattleForWinnerSide,
            contributionShareOfWinnerParty,
            renownMultiplierForWinnerSide,
            includeDescriptions);
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper. Replaces the prior
        // `winnerParty.Owner?.Culture ?? winnerParty.Culture`: winnerParty.Culture is `MapFaction.Culture`
        // and NREs when MapFaction is null, and the old order skipped LeaderHero.Culture (Codex review 43).
        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty), ref result);
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(winnerParty), ref result, PassiveEffectType.RenownGain);
        return result;
    }

    // Creature Bandits (#692, #694): a spider or a bandit troll is not a captive. MapEvent asks this before moving a
    // defeated troop to the winner's prisoners (v1.5.3 MapEvent.cs:1855), so neither reaches a roster to be ransomed
    // or recruited.
    public override bool CanTroopBeTakenPrisoner(CharacterObject troop)
        => !CreatureBanditAgents.RefusesPrisoner(troop?.StringId) && base.CanTroopBeTakenPrisoner(troop);

    // Creature Bandits (#694): a brood or troll band never takes in freed prisoners, so it stays spiders or trolls only.
    public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootPrisonerChances(
        MBReadOnlyList<MapEventParty> winnerParties, TroopRosterElement prisonerElement)
        => CreatureBanditAgents.WithoutCreatureBandWinners(base.GetLootPrisonerChances(winnerParties, prisonerElement));
}
