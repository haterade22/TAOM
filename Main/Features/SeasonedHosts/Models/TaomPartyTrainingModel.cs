using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.SeasonedHosts.Models;

/// <summary>
/// Seasoned Hosts (#890, docs/features/seasoned-hosts.md): an AI lord party's troops train faster. Both XP members call base
/// first, then add one factor from the service. The daily member covers each troop of the party
/// (<c>MobilePartyTrainingBehavior.OnDailyTickParty</c>); the battle member covers the XP a troop earned in a battle
/// (<c>MapEventParty.CommitXpGain</c>). Registry: docs/reference/gamemodel-registry.md.
///
/// The daily member also carries the career TroopTraining passive (#769 E11): flat experience per troop per day for the
/// leader's own troops of the pip's class (<c>TroopClassMapper.FromCharacter</c>), keyed on the party's own LeaderHero.
/// It comes after the Seasoned Hosts factor because <c>ApplyFlat</c> divides out only the factors already on the number,
/// so the factor never scales the flat count. One model holds both because the engine has one PartyTrainingModel slot.
/// </summary>
public sealed class TaomPartyTrainingModel : DefaultPartyTrainingModel
{
    private readonly SeasonedHostsService _service;
    private readonly ISeasonedHostsAdapter _adapter;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyTrainingModel(SeasonedHostsService service, ISeasonedHostsAdapter adapter, ICareerPassiveService careerPassives)
    {
        _service = service;
        _adapter = adapter;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetEffectiveDailyExperience(MobileParty mobileParty, TroopRosterElement troop)
    {
        var result = WithBonus(base.GetEffectiveDailyExperience(mobileParty, troop), mobileParty);
        _careerPassives.ApplyFlat(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.TroopTraining, TroopClassMapper.FromCharacter(troop.Character));
        return result;
    }

    public override ExplainedNumber CalculateXpGainFromBattles(FlattenedTroopRosterElement troopRosterElement, PartyBase party) =>
        WithBonus(base.CalculateXpGainFromBattles(troopRosterElement, party), party.MobileParty);

    private ExplainedNumber WithBonus(ExplainedNumber xp, MobileParty party)
    {
        xp.AddFactor(_service.TrainingFactor(_adapter.Read(party)));
        return xp;
    }
}
