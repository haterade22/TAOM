namespace TAOM.Features.TroopProgression;

public interface ITroopCostService
{
    int GetCharacterWage(int tier, bool isMounted, bool isMercenary);
    int GetTroopRecruitmentCost(int level, bool isMercenary);

    /// <summary>
    /// Floors a troop upgrade's XP cost so it can never be zero.
    /// </summary>
    /// <param name="baseCost">What vanilla's PartyTroopUpgradeModel charged for this edge.</param>
    /// <param name="targetLevel">The upgrade target's level; used only when the base cost is not
    /// positive, and clamped to a sane range so the result is always positive.</param>
    int GetUpgradeXpCost(int baseCost, int targetLevel);
}
