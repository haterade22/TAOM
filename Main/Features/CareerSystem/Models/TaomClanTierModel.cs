using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomClanTierModel : DefaultClanTierModel
{
    private readonly ICareerPassiveService _passiveService;

    public TaomClanTierModel(ICareerPassiveService passiveService)
    {
        _passiveService = passiveService;
    }

    public override int GetCompanionLimit(Clan clan)
    {
        var baseLimit = base.GetCompanionLimit(clan);

        var leader = clan?.Leader;
        if (leader == null) return baseLimit;
        if (_passiveService == null) return baseLimit;

        var bonus = _passiveService.GetPassiveMagnitude(leader.StringId, PassiveEffectType.CompanionLimit);
        return baseLimit + (int)bonus;
    }
}
