using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.SettlementFood;

public class SettlementFoodService : ISettlementFoodService
{
    private static readonly TextObject AdjustmentText =
        new TextObject("{=taom_settlement_food_adjustment}Settlement food (TAOM)");

    // Vanilla DefaultSettlementFoodModel constants the knobs are deltas against.
    private const float VanillaTownBaseFood = 15f;
    private const float VanillaCastleBaseFood = 10f;
    private const float VanillaVillageMultiplier = 6f;

    public float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled)
    {
        if (!enabled || snapshot == null || config == null)
            return 0f;

        float delta = 0f;

        // Garrison raw-count correction (always — the troop-weight inflation is a bug regardless of
        // siege). Base subtracted weighted/divisor; we want raw/divisor, so add back the over-count.
        // Uses the SAME divisor the model's NumberOfMenOnGarrisonToEatOneFood override fed to base.
        int garrisonDivisor = config.GarrisonFoodDivisor > 0 ? config.GarrisonFoodDivisor : 20;
        int overCount = snapshot.WeightedGarrisonCount - snapshot.RawGarrisonCount;
        if (overCount > 0)
            delta += overCount / (float)garrisonDivisor;

        // Production knobs are siege-gated: vanilla zeroes all village/lands production under siege,
        // and we must not undermine the siege-starvation mechanic.
        if (!snapshot.IsUnderSiege)
        {
            float vanillaBase = snapshot.IsTown ? VanillaTownBaseFood : VanillaCastleBaseFood;
            float configBase = snapshot.IsTown ? config.TownBaseFood : config.CastleBaseFood;
            delta += configBase - vanillaBase;

            float multiplierDelta = config.VillageFoodMultiplier - VanillaVillageMultiplier;
            if (multiplierDelta != 0f && snapshot.NormalVillageHearthLevels != null)
            {
                foreach (var hearthLevel in snapshot.NormalVillageHearthLevels)
                    delta += (hearthLevel + 1) * multiplierDelta;
            }

            delta += config.FlatFoodBonus;
        }

        return delta;
    }

    public void ApplyFoodAdjustment(
        TownFoodSnapshot snapshot,
        SettlementFoodConfig config,
        bool enabled,
        ref ExplainedNumber result,
        bool includeDescriptions)
    {
        float delta = ComputeFoodDelta(snapshot, config, enabled);
        if (delta == 0f)
            return;

        result.Add(delta, includeDescriptions ? AdjustmentText : null);
    }
}
