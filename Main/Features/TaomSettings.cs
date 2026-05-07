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

    [SettingPropertyFloatingInteger("Evil Faction Aggression Scale", 0.5f, 3.0f, "#0.0", Order = 3,
        HintText = "Global multiplier applied to all per-faction strength inflation values from army_targeting.json. 1.0 = use JSON defaults. Raise to make evil factions siege even when outnumbered.")]
    [SettingPropertyGroup("AI Strategic Intelligence")]
    public float EvilFactionAggressionScale { get; set; } = 1.0f;

    [SettingPropertyFloatingInteger("Long-Range Priority Boost Scale", 1.0f, 5.0f, "#0.0", Order = 4,
        HintText = "Global multiplier applied to per-faction distance compensation values from army_targeting.json. 1.0 = use JSON defaults. Raise if priority-list targets are still being ignored due to map distance.")]
    [SettingPropertyGroup("AI Strategic Intelligence")]
    public float LongRangePriorityBoostScale { get; set; } = 1.0f;

    [SettingPropertyFloatingInteger("Border Proximity Floor", 0.0f, 1.0f, "#0.00", Order = 5,
        HintText = "Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range. 0 = vanilla (may ignore distant priority targets entirely). 0.15 = allow long-range priority targets to be scored.")]
    [SettingPropertyGroup("AI Strategic Intelligence")]
    public float ArmyBorderProximityFloor { get; set; } = 0.15f;

    // --- Time Acceleration ---

    [SettingPropertyGroup("Time Acceleration", GroupOrder = 10)]
    [SettingPropertyInteger("Fast Forward Multiplier", 1, 128, Order = 0,
        HintText = "Speed multiplier applied when pressing the fast-forward button (Space). Default: 4.")]
    public int FastForwardMultiplier { get; set; } = 4;

    [SettingPropertyGroup("Time Acceleration")]
    [SettingPropertyInteger("Extra Fast Forward Multiplier", 1, 128, Order = 1,
        HintText = "Speed multiplier applied with the extra fast-forward button (E). Default: 8.")]
    public int ExtraFastForwardMultiplier { get; set; } = 8;

    [SettingPropertyGroup("Time Acceleration")]
    [SettingPropertyInteger("Turbo Multiplier (Ctrl+Space)", 1, 128, Order = 2,
        HintText = "Speed multiplier while holding Ctrl+Space. Releases back to prior speed on key-up. Default: 16.")]
    public int CtrlSpaceMultiplier { get; set; } = 16;

    // --- Battle Tactics / Siege Dismount ---

    [SettingPropertyGroup("Battle Tactics/Siege Dismount", GroupOrder = 20)]
    [SettingPropertyBool("Enable Siege Dismount", Order = 0,
        HintText = "Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
    public bool EnableSiegeDismount { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
    [SettingPropertyInteger("Siege Mount Behavior (0=Vanilla, 1=Reserved, 2=ToInventory, 3=AutoRemount)", 0, 3, Order = 1,
        HintText = "0 = Vanilla (no change). 1 = RESERVED (currently equivalent to Vanilla — full implementation deferred; would spawn the horse on the map separately). 2 = Mount moves to inventory for siege duration; player must re-equip manually after. 3 = Mount moves to inventory and is auto-restored after siege ends. Default: 3.")]
    public int SiegeMountBehavior { get; set; } = 3;

    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
    [SettingPropertyBool("Siege Dismount Debug Mode", Order = 2,
        HintText = "Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
    public bool SiegeDismountDebug { get; set; } = false;

    // --- Messengers ---

    [SettingPropertyGroup("Messengers", GroupOrder = 25)]
    [SettingPropertyBool("Enable Messengers", Order = 0,
        HintText = "Send paid messengers to heroes you have already met. They travel for several days and trigger a conversation on arrival. Disable to remove the encyclopedia button and dialog hook.")]
    public bool EnableMessengers { get; set; } = true;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyInteger("Gold Cost", 10, 500, Order = 1,
        HintText = "Denar cost to dispatch one messenger.")]
    public int MessengerGoldCost { get; set; } = 50;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyInteger("Travel Days", 1, 10, Order = 2,
        HintText = "In-game days a messenger spends in transit before arriving at the target. Speed scales to map size.")]
    public int MessengerTravelDays { get; set; } = 3;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyBool("Enable Accidents", Order = 3,
        HintText = "Random ambush chance during travel. The base hourly probability lives in messenger_config.json (default 0.2%).")]
    public bool MessengerAccidents { get; set; } = true;

    // --- Battle Tactics / Mixed Formations ---

    [SettingPropertyGroup("Battle Tactics/Mixed Formations", GroupOrder = 21)]
    [SettingPropertyBool("Enable Mixed Formations", Order = 0,
        HintText = "Master toggle. When off, formations use vanilla positioning. When on, formations with mixed melee + ranged units are reordered per the chosen layout while holding position.")]
    public bool EnableMixedFormations { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyInteger("Default Layout (0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard)", 0, 3, Order = 1,
        HintText = "Default layout auto-applied to mixed-class formations (>=5 minority units AND >=20% minority share AND >=10 total units). 0=Infantry front + Ranged back. 1=Ranged front + Infantry back. 2=Ranged on the wings, Infantry in the center. 3=Checkerboard. Default: 0.")]
    public int MixedFormationsDefaultLayout { get; set; } = 0;

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyText("Cycle Layout Hotkey", Order = 2,
        HintText = "Bannerlord InputKey name. Pressing this while a formation is selected cycles its layout to the next; pressing while no formation is selected cycles all formations. Default: L.")]
    public string MixedFormationsCycleHotkey { get; set; } = "L";

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyBool("Mixed Formations Debug Mode", Order = 3,
        HintText = "Show diagnostic [MixedFormations] messages on the in-game HUD. Off = file log only.")]
    public bool MixedFormationsDebug { get; set; } = false;
}
