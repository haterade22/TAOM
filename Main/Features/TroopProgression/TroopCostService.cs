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

    /// <summary>
    /// Vanilla's own high-tier XP factor. `DefaultPartyTroopUpgradeModel.GetXpCostForUpgrade`
    /// falls through to `1.333f * (level + 4)^2` for every tier above its hardcoded table, which
    /// under TAOM's MaxCharacterTier of 10 means tiers 8, 9 and 10. Reusing it here prices a
    /// lateral upgrade by the same rule that already prices its neighbours.
    /// </summary>
    private const float LateralUpgradeXpFactor = 1.333f;

    /// <summary>
    /// Upper bound on the level fed to the lateral formula. No TAOM troop exceeds 51, and the tier
    /// ladder tops out there anyway, so this only exists to keep `level + 4` away from int overflow:
    /// that addition is integer arithmetic, so a pathological level wraps negative and the (int)
    /// cast of the resulting float lands on int.MinValue. A guard whose whole job is to never
    /// return zero would then return the most negative int there is.
    /// </summary>
    private const int MaxLateralUpgradeLevel = 1000;

    // Crash bundle a7dc3a20. Vanilla sums a per-tier table over
    // `for (i = source.Tier + 1; i <= target.Tier; i++)`, so it returns 0 for any upgrade edge
    // whose target does not reach a higher tier bracket. `CampaignUIHelper.GetTroopXPTooltip`
    // then evaluates `troop.Xp % cost` unguarded and takes the game down; the AI upgrader and
    // `PartyBase.OnXpChanged` treat the same zero as "free" and "no XP worth keeping".
    // TAOM authors deliberate same-level lateral upgrades, so the fix prices them.
    public int GetUpgradeXpCost(int baseCost, int targetLevel)
    {
        if (baseCost > 0)
            return baseCost;

        int level = targetLevel < 1 ? 1
                  : targetLevel > MaxLateralUpgradeLevel ? MaxLateralUpgradeLevel
                  : targetLevel;
        float span = level + 4;

        return (int)(LateralUpgradeXpFactor * span * span);
    }
}
