using System;
using TAOM.Core.Validation;

namespace TAOM.Features.SettlementEconomy;

public class SettlementEconomyService : ISettlementEconomyService
{
    // Vanilla DefaultSettlementEconomyModel.GetTownGoldChange constants (v1.4.6, verified):
    // round(0.25f * (10000f + Prosperity * 12f - Gold)). Used only for the defensive null-config path.
    private const float VanillaTownGoldBase = 10000f;
    private const float VanillaTownGoldPerProsperity = 12f;
    private const float VanillaTownGoldRegenRate = 0.25f;

    public int ComputeTownGoldChange(float prosperity, int currentGold, SettlementEconomyConfig config)
    {
        float baseGold = config?.TownGoldBase ?? VanillaTownGoldBase;
        float perProsperity = config?.TownGoldPerProsperity ?? VanillaTownGoldPerProsperity;
        float rate = config?.TownGoldRegenRate ?? VanillaTownGoldRegenRate;

        // The engine passes a raw float; NaN/Infinity would poison the whole computation.
        if (!FiniteFloatValidator.IsFinite(prosperity))
            prosperity = 0f;

        // Float arithmetic then (int)Math.Round matches TaleWorlds MathF.Round exactly
        // (banker's rounding via double promotion) — pinned by the midpoint test.
        float deficit = baseGold + prosperity * perProsperity - currentGold;
        return (int)Math.Round(rate * deficit);
    }
}
