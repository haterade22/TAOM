namespace TAOM.Features.StartupResources;

public interface IPlayerStartupGoldService
{
    void GrantPlayerStartupGold(string cultureId, string playerHeroId);
}
