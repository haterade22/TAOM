using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

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

            // Hinterland: production scaled by prosperity. Vanilla reads prosperity ONLY as a
            // consumer (Prosperity/divisor) against flat production, so every fief above roughly
            // (production × divisor) prosperity is guaranteed to starve, and one that grows during
            // play starves later even if it broke even at start. Feeding prosperity back into
            // production makes the balance hold at any size. The provider keeps the rate strictly
            // below 1/ProsperityFoodDivisor so net food still FALLS as prosperity rises, preserving
            // vanilla's self-limiter.
            //
            // Prosperity is ENGINE-sourced, so it is gated as a POSITIVE requirement rather than by
            // an early-exit: every NaN comparison is false, so `if (bad) skip` would let NaN through.
            // Town.Prosperity's setter only floors at 0 (`if (_prosperity < 0f)`), which NaN passes,
            // so a NaN is storable. It would poison this ExplainedNumber, and Town.DailyTick's
            // `FoodStocks += FoodChange` clamps (`< 0f`, `> cap`) are BOTH false for NaN, leaving
            // FoodStocks permanently NaN in a [SaveableProperty]. See csharp-architecture.md
            // "Engine-Float Decision Gates".
            if (FiniteFloatValidator.IsFinite(snapshot.Prosperity))
                delta += snapshot.Prosperity * config.HinterlandFoodPerProsperity;
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

        // Positive requirement, not `if (delta == 0f) return;`: NaN == 0f is false, so a bare
        // zero-check forwards a poisoned delta into the engine's ExplainedNumber. This is the last
        // gate before TAOM's number reaches vanilla, so it refuses anything non-finite outright
        // regardless of which input produced it.
        if (!FiniteFloatValidator.IsFinite(delta) || delta == 0f)
            return;

        result.Add(delta, includeDescriptions ? AdjustmentText : null);
    }
}
