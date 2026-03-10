namespace TAOM.Features.TroopProgression;

public class TroopCostService : ITroopCostService
{
    private const float MountedWageMultiplier = 1.3f;
    private const float MercenaryWageMultiplier = 1.5f;
    private const int MercenaryRecruitMultiplier = 2;

    public int GetCharacterWage(int tier, bool isMounted, bool isMercenary)
    {
        int baseWage = tier switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            3 => 5,
            4 => 8,
            5 => 12,
            6 => 15,
            7 => 18,
            8 => 20,
            9 => 25,
            10 => 30,
            _ => 57
        };

        float wage = baseWage;

        if (isMercenary)
            wage *= MercenaryWageMultiplier;

        if (isMounted)
            wage *= MountedWageMultiplier;

        return (int)wage;
    }

    public int GetTroopRecruitmentCost(int level, bool isMercenary)
    {
        int baseCost = level switch
        {
            1 => 10,
            <= 6 => 20,
            <= 11 => 50,
            <= 16 => 200,
            <= 21 => 400,
            <= 26 => 600,
            <= 31 => 1000,
            <= 36 => 1500,
            <= 41 => 2100,
            <= 46 => 2800,
            <= 51 => 3600,
            _ => 4000
        };

        if (isMercenary)
            baseCost *= MercenaryRecruitMultiplier;

        return baseCost;
    }
}
