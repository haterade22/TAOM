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

    // Career InfluenceGain (#769 E11). Additive: vanilla's answer first (0 outside a kingdom, which the factor keeps), then
    // the leader's factor, keyed owner-first like RenownGain because the two ship bundled. Callers (v1.5.5): only
    // MapEvent.cs:1620 at the end of a battle, once per winning party that has a LeaderHero; no null-argument call.
    public override ExplainedNumber CalculateInfluenceGain(
        PartyBase winnerParty,
        float influenceValueOfBattleForWinnerSide,
        float contributionShareOfWinnerParty,
        float influenceMultiplierForWinnerSide,
        bool includeDescriptions)
    {
        var result = base.CalculateInfluenceGain(
            winnerParty,
            influenceValueOfBattleForWinnerSide,
            contributionShareOfWinnerParty,
            influenceMultiplierForWinnerSide,
            includeDescriptions);
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(winnerParty), ref result, PassiveEffectType.InfluenceGain);
        return result;
    }

    // Career LootGold (#769 E11). Vanilla returns one share per winning party (contribution over the total); MapEvent pays
    // each party plundered * share, to the party's LeaderHero, with no renormalisation (CalculatePlunderedAndLostGoldAmounts,
    // v1.5.5 MapEvent.cs:1271-1278; the gold is minted, MapEventParty.CommitGoldChanges). So scaling a share scales that
    // party's pay alone. Keyed on the winner's own LeaderHero, not owner-first: the gold goes to the LeaderHero, and an
    // owner-first key would pay the player's bonus on a caravan he owns. Callers: only MapEvent.cs:1271, once per battle.
    public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootGoldChances(MBReadOnlyList<MapEventParty> winnerParties)
        => new MBList<KeyValuePair<MapEventParty, float>>(CareerLootShares.Scale(
            base.GetLootGoldChances(winnerParties), winner => winner.Party.LeaderHero?.StringId, _careerPassives));

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
