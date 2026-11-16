using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Features.HeroAbilities;

public interface IHeroAbilitiesConfigProvider
{
    HeroAbilitiesConfig GetConfig();
}
