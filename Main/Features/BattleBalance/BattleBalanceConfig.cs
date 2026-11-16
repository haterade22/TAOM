using System.Collections.Generic;

namespace TAOM.Features.BattleBalance;

public class BattleBalanceConfig
{
    public TroopPowerSection TroopPower { get; set; } = new();
    public CasualtyRatiosSection CasualtyRatios { get; set; } = new();
    public AutoResolveSection AutoResolve { get; set; } = new();
    public SiegeDefenceSection SiegeDefence { get; set; } = new();

    /// <summary>Auto-resolve siege defence by culture (#1283).</summary>
    public class SiegeDefenceSection
    {
        /// <summary>
        /// Culture id to a factor, in [0, 1], added to the defenders' battle advantage in an auto-resolved siege of a
        /// fortification whose realm has that culture. The engine gives the attackers -0.1 in every siege; 0.1 gives
        /// the defenders +10%. Blue Craig stands alone in the far west against its neighbours.
        /// </summary>
        public Dictionary<string, float> DefenderAdvantageBonuses { get; set; } = new()
        {
            ["bluecraig"] = 0.1f
        };

        public float GetDefenderAdvantageBonus(string cultureId) =>
            cultureId != null && DefenderAdvantageBonuses.TryGetValue(cultureId, out var v) ? v : 0f;
    }

    /// <summary>Auto-resolve tuning (#1283). See <see cref="AutoResolveQualityMath"/>.</summary>
    public class AutoResolveSection
    {
        /// <summary>
        /// The exponent of the striker-to-struck power ratio in a simulated strike, in [0, 1]. Vanilla is 0.7, which makes a
        /// side's strength N^1.6 x P^1.4; 0.5 makes it N^1.6 x P^1.0, so numbers outweigh quality. 0 ignores quality.
        /// </summary>
        public float PowerRatioExponent { get; set; } = 0.5f;
    }

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
            ["gondor"] = 0.3f,    ["vlandia"] = 0.2f,    ["lothlorien"] = 0.5f,
            ["erebor"] = 0.3f,    ["rivendell"] = 0.4f,  ["lindon"] = 0.4f,
            ["mirkwood"] = 0.4f,
            ["arthedain"] = 0.3f, ["cardolan"] = 0.3f,  ["rhudaur"] = 0.2f,
            ["eredluin"] = 0.3f, ["bluecraig"] = 0.3f,
            ["anduinvale"] = 0.1f, ["angaladh"] = 0.1f,
            ["dorwinion"] = 0.1f, ["southrhovanion"] = 0.2f,
            ["gundabad"] = -0.1f,  ["dolguldur"] = -0.1f
        };

        public float GetCulturalSurvivalBonus(string cultureId) =>
            CulturalSurvivalBonuses.TryGetValue(cultureId, out var v) ? v : 0f;
    }
}
