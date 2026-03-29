using System.Collections.Generic;

namespace TAOM.Features.BattleBalance;

public class BattleBalanceConfig
{
    public TroopPowerSection TroopPower { get; set; } = new();
    public CasualtyRatiosSection CasualtyRatios { get; set; } = new();

    public class TroopPowerSection
    {
        public Dictionary<string, float> TierPower { get; set; } = new()
        {
            ["T0"] = 0.40f, ["T1"] = 0.66f, ["T2"] = 0.96f, ["T3"] = 1.30f,
            ["T4"] = 1.68f, ["T5"] = 2.10f, ["T6"] = 2.56f, ["T7"] = 2.91f,
            ["T8"] = 3.26f, ["T9"] = 3.61f, ["T10"] = 3.96f
        };

        public float GetTierPower(int tier) =>
            TierPower.TryGetValue($"T{tier}", out var v) ? v : (2f + tier) * (10f + tier) * 0.02f;
    }

    public class CasualtyRatiosSection
    {
        public bool EnableCulturalSurvivalBonuses { get; set; } = true;
        // bonus > 0 = more survival, bonus < 0 = more death
        // Formula: newDeathChance = vanillaDeathChance * (1 - bonus)
        public Dictionary<string, float> CulturalSurvivalBonuses { get; set; } = new()
        {
            ["gondor"] = 0.3f,    ["rohan"] = 0.2f,      ["lothlorien"] = 0.5f,
            ["erebor"] = 0.3f,    ["rivendell"] = 0.4f,
            ["mordor"] = -0.2f,   ["gundabad"] = -0.1f,  ["dol_guldur"] = -0.1f
        };

        public float GetCulturalSurvivalBonus(string cultureId) =>
            CulturalSurvivalBonuses.TryGetValue(cultureId, out var v) ? v : 0f;
    }
}
