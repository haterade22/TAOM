using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomClanTierModel : DefaultClanTierModel
{
    private ICareerPassiveService _passiveService;

    private ICareerPassiveService PassiveService =>
        _passiveService ??= IoC.Resolve<ICareerPassiveService>();

    public override int GetCompanionLimit(Clan clan)
    {
        var baseLimit = base.GetCompanionLimit(clan);

        var leader = clan?.Leader;
        if (leader == null) return baseLimit;

        var svc = PassiveService;
        if (svc == null) return baseLimit;

        var bonus = svc.GetPassiveMagnitude(leader.StringId, PassiveEffectType.CompanionLimit);
        return baseLimit + (int)bonus;
    }
}
