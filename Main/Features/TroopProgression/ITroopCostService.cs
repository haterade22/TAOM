namespace TAOM.Features.TroopProgression;

public interface ITroopCostService
{
    int GetCharacterWage(int tier, bool isMounted, bool isMercenary);
    int GetTroopRecruitmentCost(int level, bool isMercenary);
}
