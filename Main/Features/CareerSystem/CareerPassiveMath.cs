using System;

namespace TAOM.Features.CareerSystem;

// Pure, TaleWorlds-free arithmetic for the two career passives whose consumers are otherwise
// untestable entry points — the Ammo MissionBehavior.OnAgentBuild callback and the StealthBonus
// map-visibility model override. Keeping the math here makes it 100% unit-testable (ADR-008) and
// the call sites thin boundaries (gamemodels.md rule 4). The other passive math already lives in
// the testable CareerAgentStatService / CareerPassiveService.
public static class CareerPassiveMath
{
    /// <summary>
    /// New ammo amount for a ranged slot given a multiplicative <paramref name="bonus"/>
    /// (0.10 = +10% of the slot's modified max). Never shrinks below the current amount and
    /// clamps to <see cref="short.MaxValue"/> (the engine stores ammo as a short).
    /// </summary>
    public static int BoostAmmo(int modifiedMaxAmount, int currentAmount, float bonus)
    {
        if (bonus <= 0f) return currentAmount;
        int boosted = (int)Math.Round(modifiedMaxAmount * (1f + bonus));
        if (boosted > short.MaxValue) boosted = short.MaxValue;
        return boosted > currentAmount ? boosted : currentAmount;
    }

    /// <summary>
    /// Adjusts a [0,1] "how easily others spot this party" ratio by a StealthBonus. A positive
    /// bonus LOWERS the ratio (the party must be closer to be seen = harder to detect). Zero is a
    /// no-op so a hero without the passive keeps the vanilla ratio exactly.
    /// </summary>
    public static float ApplyStealthRatio(float baseRatio, float bonus)
        => bonus != 0f ? baseRatio * (1f - bonus) : baseRatio;
}
