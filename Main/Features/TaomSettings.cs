using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features;

public class TaomSettings : AttributeGlobalSettings<TaomSettings>
{
    public override string Id => "TAOM";
    public override string DisplayName => "TAOM - Tales From the Age of Men";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    // --- Encyclopedia ---

    [SettingPropertyGroup("Encyclopedia")]
    [SettingPropertyBool("Show All Characters", Order = 0,
        HintText = "Reveals all characters in the encyclopedia, including those not yet encountered. Equivalent to the 'campaign.toggle_information_restrictions' cheat.")]
    public bool ShowAllEncyclopediaCharacters { get; set; } = true;

    // --- Troop Weight ---

    [SettingPropertyGroup("Troop Weight")]
    [SettingPropertyBool("Enable Troop Weight", Order = 0,
        HintText = "Weighted party size — elite units consume more party capacity. Cave trolls (4x), elves (2x), warg riders (2x).")]
    public bool EnableTroopWeight { get; set; } = true;

    // --- War of the Ring ---

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyBool("Enable War of the Ring", Order = 0,
        HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
    public bool WarOfTheRingEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1,
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan.")]
    public int Phase1TriggerDay { get; set; } = 30;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2,
        HintText = "Days after campaign start when all hostile kingdoms go to war. Peace is blocked.")]
    public int Phase2TriggerDay { get; set; } = 45;

    [SettingPropertyGroup("War of the Ring/Test Mode")]
    [SettingPropertyBool("Enable Test Mode", Order = 0,
        HintText = "Uses short delays (2/5 days) for rapid testing. Overrides Phase 1/2 days.")]
    public bool TestMode { get; set; }

    // --- Battle Balance / Troop Power ---

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyBool("Enable Custom Troop Power", Order = 0,
        HintText = "Enables configurable T7-T10 troop power values for battle simulation.")]
    public bool EnableCustomTroopPower { get; set; } = true;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyBool("Override Vanilla Tiers (T1-T6)", Order = 1,
        HintText = "If enabled, battle_balance_config.json TierPower values replace the vanilla formula for T1-T6.")]
    public bool OverrideVanillaTierPower { get; set; } = false;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 7 Base Power", 2.0f, 6.0f, "#0.00", Order = 2,
        HintText = "Base simulation power for T7 troops (vanilla formula extrapolation = 3.06).")]
    public float Tier7Power { get; set; } = 2.91f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 8 Base Power", 2.0f, 7.0f, "#0.00", Order = 3,
        HintText = "Base simulation power for T8 troops (vanilla formula extrapolation = 3.60).")]
    public float Tier8Power { get; set; } = 3.26f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 9 Base Power", 2.0f, 8.0f, "#0.00", Order = 4,
        HintText = "Base simulation power for T9 troops (vanilla formula extrapolation = 4.18).")]
    public float Tier9Power { get; set; } = 3.61f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 10 Base Power", 2.0f, 9.0f, "#0.00", Order = 5,
        HintText = "Base simulation power for T10 troops (vanilla formula extrapolation = 4.80).")]
    public float Tier10Power { get; set; } = 3.96f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Hero Power Multiplier", 1.0f, 3.0f, "#0.0", Order = 6,
        HintText = "Multiplier applied to heroes in battle simulation. Vanilla = 1.5.")]
    public float HeroMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Mounted Power Multiplier", 1.0f, 2.0f, "#0.0", Order = 7,
        HintText = "Multiplier applied to mounted troops in battle simulation. Vanilla = 1.2.")]
    public float MountedMultiplier { get; set; } = 1.2f;

    // --- Battle Balance / Casualty Ratios ---

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyBool("Enable Custom Casualty Ratios", Order = 0,
        HintText = "Enables configurable wound/kill ratios for battle simulation.")]
    public bool EnableCustomCasualtyRatios { get; set; } = true;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyFloatingInteger("Player Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 1,
        HintText = "Blunt (wound-only) damage chance in player battles. Vanilla = 0.30.")]
    public float PlayerBluntDamageChance { get; set; } = 0.30f;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyFloatingInteger("AI Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 2,
        HintText = "Blunt damage chance in AI vs AI battles. Vanilla = 0.10.")]
    public float AIBluntDamageChance { get; set; } = 0.10f;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyBool("Enable Cultural Survival Bonuses", Order = 3,
        HintText = "Applies per-culture survival modifiers from battle_balance_config.json. Gondor +30%, Lothlorien +50%, Mordor -20%.")]
    public bool EnableCulturalSurvivalBonuses { get; set; } = true;

    // --- Siege Defense ---

    [SettingPropertyGroup("Siege Defense")]
    [SettingPropertyBool("Enable Siege Defense Events", Order = 0,
        HintText = "When enabled, you receive an event when a watched faction's settlement is besieged, with a timed window to help defend.")]
    public bool EnableSiegeDefenseEvents { get; set; } = true;

    [SettingPropertyGroup("Siege Defense")]
    [SettingPropertyInteger("Response Window (Days)", 1, 14, Order = 1,
        HintText = "Number of in-game days to travel to a besieged settlement before the event expires.")]
    public int SiegeDefenseResponseDays { get; set; } = 3;

    // --- AI Strategic Intelligence ---

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyBool("Enable AI Strategic Intelligence", Order = 0,
        HintText = "When enabled, AI armies stick to their current target rather than re-optimising every 3 hours. Reduces army thrashing and improves siege follow-through.")]
    public bool EnableArmyStrategicIntelligence { get; set; } = true;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Commitment Multiplier", 1.0f, 10.0f, "#0.0", Order = 1,
        HintText = "How strongly an army commits to its current target. 4.0 = the alternative must score 4x better before the army will divert. Vanilla implicit = 1.3.")]
    public float ArmyCommitmentMultiplier { get; set; } = 4.0f;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Priority List Boost", 1.0f, 5.0f, "#0.0", Order = 2,
        HintText = "Score multiplier applied to the first settlement in a faction's priority list. Decays linearly to 1.0 at the last entry. Affects Mordor, Isengard etc.")]
    public float ArmyPriorityBoost { get; set; } = 3.0f;
}
