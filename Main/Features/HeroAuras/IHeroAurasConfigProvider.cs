using TAOM.Features.HeroAuras.Domain;

namespace TAOM.Features.HeroAuras;

public interface IHeroAurasConfigProvider
{
    HeroAurasConfig GetConfig();
}
