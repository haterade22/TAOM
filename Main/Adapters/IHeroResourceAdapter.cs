namespace TAOM.Adapters;

public interface IHeroResourceAdapter
{
    string HeroId { get; }
    string KingdomId { get; }
    bool IsPlayerHero { get; }
    int OwnedTownCount { get; }
}
