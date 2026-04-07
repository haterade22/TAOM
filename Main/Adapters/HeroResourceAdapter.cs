using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public class HeroResourceAdapter : IHeroResourceAdapter
{
    private readonly Hero _hero;

    public HeroResourceAdapter(Hero hero)
    {
        _hero = hero;
    }

    public string HeroId => _hero?.StringId;
    public string KingdomId => _hero?.Clan?.Kingdom?.StringId;
    public bool IsPlayerHero => _hero == Hero.MainHero;
    public int OwnedTownCount => _hero?.Clan?.Settlements?.Count(s => s.IsTown) ?? 0;
}
