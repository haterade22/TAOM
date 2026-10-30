using System;

namespace TAOM.Features.BattleBalance;

/// <summary>Pure arithmetic of the survival roll's cultural adjustment; no engine types, so it is unit tested directly. The career
/// TroopSurvival scale is <c>ICareerPassiveService.ScaleByPassive</c>, which floors at zero.</summary>
internal static class SurvivalChanceMath
{
    /// <summary>Scales the death chance by (1 - bonus), clamped to 0..1. A zero bonus returns vanilla's value untouched.</summary>
    internal static float ApplyCulturalSurvivalBonus(float vanillaSurvival, float culturalBonus)
    {
        if (culturalBonus == 0f)
            return vanillaSurvival;

        float deathChance = 1f - vanillaSurvival;
        float newDeathChance = deathChance * (1f - culturalBonus);
        float result = 1f - newDeathChance;
        return Math.Max(0f, Math.Min(1f, result));
    }
}
