using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.Core;
using TaleWorlds.Library;

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

    // --- Settlement Food ---

    [SettingPropertyGroup("Settlement Food")]
    [SettingPropertyBool("Enable Settlement Food Tuning", Order = 0,
        HintText = "Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
    public bool EnableSettlementFoodTuning { get; set; } = true;

    // --- Settlement Economy ---

    [SettingPropertyGroup("Settlement Economy")]
    [SettingPropertyBool("Enable Settlement Economy Tuning", Order = 0,
        HintText = "Regenerates town market gold toward a higher target (base 25000 vs vanilla 10000) so drained town markets recover — tunable in settlement_economy/settlement_economy_config.json (base, gold per prosperity, daily regen rate). Applies to existing saves immediately. Off = vanilla engine gold math. Config edits need an app restart.")]
    public bool EnableSettlementEconomyTuning { get; set; } = true;

    // --- Caravan Trade ---

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyBool("Enable Caravan Trade Overhaul", Order = 0,
        HintText = "Makes AI/player caravans range beyond the local town cluster instead of shuttling between very-close towns, trade across the endless Free-vs-Evil war (per War Trade Policy below), and carry fuller baskets. Off = exact vanilla caravan behavior. Advanced curve knobs live in caravan_trade/caravan_trade_config.json; config edits need an app restart.")]
    public bool EnableCaravanTrade { get; set; } = true;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyBool("Apply To Player Caravans", Order = 1,
        HintText = "When on, your OWN caravans also range further, trade cross-war, and buy fuller baskets (they may enter contested territory and risk attack). Off scopes the selection re-weight + basket diversity off your clan's caravans. Note: the wider distance ceiling is engine-global (shared by all caravans) and the war-trade gate scopes at your whole faction, so those two levers aren't fully isolated to non-player caravans — but with this off your caravans still route by vanilla's nearest-first selection.")]
    public bool CaravanTradeApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyFloatingInteger("Caravan Range Multiplier", 1.0f, 4.0f, "#0.0", Order = 2,
        HintText = "How much further caravans range past the vanilla distance ceiling. 1.0 = vanilla reach; higher = they visit more distant, more profitable markets. Default: 1.6.")]
    public float CaravanRangeMultiplier { get; set; } = 1.6f;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyDropdown("War Trade Policy", Order = 3,
        HintText = "Which towns caravans may trade with despite the war. Vanilla = war blocks trade (the cause of the shuttle). Same Side + Neutral = trade with same-alignment or neutral factions but not the enemy side (default, lore-friendly). Ignore War = trade anywhere non-besieged.")]
    public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
        new[] { "Vanilla (war blocks)", "Same Side + Neutral", "Ignore War" }, 1);

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyFloatingInteger("Basket Diversity Floor", 0.0f, 1.0f, "#0.00", Order = 4,
        HintText = "Raises poor caravans' buying power so they stock several goods instead of one. 0 = vanilla (poor caravans buy a single item); higher = fuller baskets. Default: 0.35.")]
    public float CaravanBudgetDiversityFloor { get; set; } = 0.35f;

    // --- Native Skin Fixes ---

    [SettingPropertyGroup("Native Skin Fixes")]
    [SettingPropertyBool("Enable Native Skin Fixes", Order = 0,
        HintText = "Installs the native MinHook detours that fix engine rendering bugs TaleWorlds won't: the covers_head hand-morph freeze (jazz-hands under closed helms) + hair/beard cloth physics. ON by default — all 7 hook targets are authored + verified against Bannerlord v1.4.6's TaleWorlds.Native.dll (RTTI-anchored disassembly + interior byte-triangulation; each pattern single-matches its expected address). Turn OFF to fully disable the native hooks (vanilla rendering) if you ever hit instability. Requires an app restart to take effect.")]
    public bool EnableNativeSkinFixes { get; set; } = true;

    // --- Castle Recruitment ---

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyBool("Enable Castle Recruitment", Order = 0,
        HintText = "When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
    public bool EnableCastleRecruitment { get; set; } = true;

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyBool("AI Recruits From Castles", Order = 1,
        HintText = "When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
    public bool EnableCastleRecruitmentAi { get; set; } = true;

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyInteger("Notables Per Castle", 1, 5, Order = 2,
        HintText = "How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
    public int CastleNotablesPerCastle { get; set; } = 3;

    // --- Elite Emissary ---

    [SettingPropertyGroup("Elite Emissary")]
    [SettingPropertyBool("Enable Elite Emissary", Order = 0,
        HintText = "At a faction's key settlements (capitals), speak with the faction emissary to buy that faction's elite troops for its special resource (Castar, War Spoils, Gems...). Conquering a settlement flips its offerings to the new owner.")]
    public bool EnableEliteEmissary { get; set; } = true;

    [SettingPropertyGroup("Elite Emissary")]
    [SettingPropertyBool("Hide Emissary Without Resource", Order = 1,
        HintText = "When on, the emissary option is hidden at settlements whose owner faction has no special resource. When off, the option still appears but is disabled with an explanatory hint.")]
    public bool HideEmissaryWhenNoResource { get; set; } = true;

    // --- Culture Conversion ---

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Enable Culture Conversion", Order = 0,
        HintText = "When enabled, a town/castle (and its villages) conquered by a different culture gradually adopts the new owner's culture — producing their troops, militia, and identity. Disabling stops NEW conversions; already-converted settlements stay converted.")]
    public bool EnableCultureConversion { get; set; } = true;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyInteger("Days To Convert", 1, 365, Order = 1,
        HintText = "Days the new owner must hold a cross-culture fief before it converts. Lower = faster cultural takeover. Default: 1 (near-instant on capture).")]
    public int CultureConversionHoldDays { get; set; } = 1;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Require Stable Loyalty", Order = 2,
        HintText = "When enabled, a conquered fief only converts once its loyalty is high enough (configured in culture_conversion_config.json), so a city in unrest never flips. Default: off.")]
    public bool CultureConversionRequireStableLoyalty { get; set; } = false;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Replace Notables On Conversion", Order = 3,
        HintText = "When enabled, conversion also replaces the settlement's notables with ones of the new culture (a Mordor-held Gondor town gets orc notables). Their workshops, alleys and caravans transfer to the newcomers; your relations with the old notables do not. Default: on.")]
    public bool CultureConversionReplaceNotables { get; set; } = true;

    // --- War of the Ring ---

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyBool("Enable War of the Ring", Order = 0,
        HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
    public bool WarOfTheRingEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1,
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan. Default 2.")]
    public int Phase1TriggerDay { get; set; } = 2;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2,
        HintText = "Days after campaign start when all hostile kingdoms go to war and peace between hostile tiers is blocked. Default 14.")]
    public int Phase2TriggerDay { get; set; } = 14;

    [SettingPropertyGroup("War of the Ring/Test Mode")]
    [SettingPropertyBool("Enable Test Mode", Order = 0,
        HintText = "Uses short delays (2/5 days) for rapid testing. Overrides Phase 1/2 days.")]
    public bool TestMode { get; set; }

    // --- War of the Ring / Momentum (#327) ---

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Enable Momentum Tracking", Order = 0,
        HintText = "Tracks Evil vs Good war progress from battles, sieges, raids, armies and relative strength. Reaching the victory threshold ENDS the war. Default: on.")]
    public bool MomentumEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Show Map Meter", Order = 1,
        HintText = "Shows the on-map War of the Ring slider once the full war begins. Click it for the detailed breakdown. Default: on.")]
    public bool ShowWarOfTheRingMapMeter { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Enable Victory (End the War)", Order = 6,
        HintText = "When OFF (default) the War of the Ring is endless — momentum is tracked but no side ever wins. Turn ON to let a side win at the Victory Threshold (or by eliminating the other), which ends the war and makes peace. Default: off (endless).")]
    public bool MomentumVictoryEnabled { get; set; } = false;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyInteger("Victory Threshold", 100, 2000, Order = 2,
        HintText = "Momentum lead one side needs to win the war outright. Default 500.")]
    public int MomentumVictoryThreshold { get; set; } = 500;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyFloatingInteger("Player Participation Multiplier", 1.0f, 3.0f, "#0.00", Order = 3,
        HintText = "Momentum gains are multiplied by this when your party takes part in the event. Default 1.5.")]
    public float MomentumParticipationMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Require Player Participation For Victory", Order = 4,
        HintText = "When on, the war cannot end until you have fought in enough momentum events yourself. Default: on.")]
    public bool MomentumRequirePlayerForVictory { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyInteger("Minimum Player Events For Victory", 0, 20, Order = 5,
        HintText = "How many battles/sieges you must take part in before either side can win. Default 5.")]
    public int MomentumMinPlayerEvents { get; set; } = 5;

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

    // --- Graphics / Shader Precompilation ---

    [SettingPropertyGroup("Graphics/Shader Precompilation", GroupOrder = 15)]
    [SettingPropertyBool("Enable Shader Precompilation", Order = 0,
        HintText = "Master toggle for the main-menu 'Pre-compile Shaders' option. When off, the option is hidden so no NEW walk can be started (a walk already in progress finishes — it is not aborted mid-flight). Takes effect immediately, no relaunch. Default: on.")]
    public bool EnableShaderPrecompilation { get; set; } = true;

    [SettingPropertyGroup("Graphics/Shader Precompilation")]
    [SettingPropertyBool("Include Scene Passes", Order = 1,
        HintText = "When on, the walk also loads each TAOM battle/siege/village scene to pre-compile its terrain + atmosphere shaders. These scene loads are the part that can hard-crash some GPUs (pbr_terrain, #287). Turn OFF to run only the safe all-characters pass (compiles troop/equipment shaders, never crashes) if pre-compile crashes for you. Default: on.")]
    public bool EnableScenePassPrecompilation { get; set; } = true;

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

    // --- Fief Management ---

    [SettingPropertyGroup("Fief Management", GroupOrder = 26)]
    [SettingPropertyBool("Enable Fief Management", Order = 0,
        HintText = "Master toggle. When off, the F6 hotkey is inert and the carousel options are disabled. Effective immediately at runtime. Default: true.")]
    public bool EnableFiefManagement { get; set; } = true;

    [SettingPropertyGroup("Fief Management")]
    [SettingPropertyBool("Allow Remote Building Queue", Order = 1,
        HintText = "When on, you can manage any owned fief from anywhere via F6. When off, the Manage option is disabled unless you are physically at the selected fief. Default: true.")]
    public bool AllowRemoteBuildingQueue { get; set; } = true;

    [SettingPropertyGroup("Fief Management")]
    [SettingPropertyBool("Fief Management Debug Mode", Order = 2,
        HintText = "Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent.")]
    public bool FiefManagementDebug { get; set; } = false;

    // --- Inventory / Quick Actions ---

    [SettingPropertyGroup("Inventory/Quick Actions", GroupOrder = 30)]
    [SettingPropertyBool("Enable Quick Actions", Order = 0,
        HintText = "Master toggle. When off, inventory 'Sell All' uses vanilla. When on, it opens a 4-option menu.")]
    public bool EnableQuickActions { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions")]
    [SettingPropertyBool("Enable Inventory Search", Order = 1,
        HintText = "Inventory search box visibility; persists per save and reconciles to MCM each campaign frame.")]
    public bool EnableInventorySearch { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged", GroupOrder = 30)]
    [SettingPropertyDropdown("Damage Threshold Preset", Order = 0,
        HintText = "Items at or below this damage level are sold. Pristine = unused (sentinel). Default: Moderate (-20%).")]
    public Dropdown<string> DamagedQualityDropdown { get; set; } = new Dropdown<string>(
        new[] { "Pristine", "Slight (-10%)", "Moderate (-20%)", "Heavy (-40%)" }, 2);

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyFloatingInteger("Custom Damage Threshold", -1.0f, 0.0f, "#0.00", Order = 1,
        HintText = "Custom threshold. Only used when 'Use Custom Threshold' is on. Default: -0.20.")]
    public float DamagedThreshold { get; set; } = -0.2f;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyBool("Use Custom Threshold", Order = 2, HintText = "Toggle dropdown vs custom value above.")]
    public bool UseCustomThreshold { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyBool("Sell Damaged Equipped", Order = 3, HintText = "Include items currently equipped on heroes.")]
    public bool SellDamagedEquipped { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyBool("Exclude Damaged Horses", Order = 4, HintText = "Skip horses/mounts when selling damaged. Default: true.")]
    public bool ExcludeDamagedHorses { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value", GroupOrder = 31)]
    [SettingPropertyInteger("Low Value Threshold (denars)", 1, 10000, Order = 0,
        HintText = "Items at or below this denars value are sold. Default: 100.")]
    public int LowValueThreshold { get; set; } = 100;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Sell Low Value Equipped", Order = 1, HintText = "Include items currently equipped. Default: false.")]
    public bool SellLowValueEquipped { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Food", Order = 2, HintText = "Skip food items. Default: true.")]
    public bool ExcludeLowValueFood { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Horses", Order = 3, HintText = "Skip horses/mounts. Default: true.")]
    public bool ExcludeLowValueHorses { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Trade Goods", Order = 4, HintText = "Skip trade goods. Default: false.")]
    public bool ExcludeLowValueTradeGoods { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc", GroupOrder = 32)]
    [SettingPropertyBool("Show Confirmation Dialog", Order = 0, HintText = "Ask for confirmation before bulk-selling. Default: true.")]
    public bool QuickActionsShowConfirmation { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
    [SettingPropertyBool("Play Sounds", Order = 1, HintText = "Play 'event:/ui/transfer' chime after each batch action. Default: true.")]
    public bool QuickActionsPlaySounds { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
    [SettingPropertyBool("Quick Actions Debug Mode", Order = 2, HintText = "Show diagnostic [QuickActions] HUD messages. Off = file log only.")]
    public bool QuickActionsDebug { get; set; } = false;

    // --- Inventory / Equipment Presets ---

    [SettingPropertyGroup("Inventory/Equipment Presets", GroupOrder = 33)]
    [SettingPropertyBool("Enable Equipment Presets", Order = 0,
        HintText = "Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
    public bool EnableEquipmentPresets { get; set; } = true;

    [SettingPropertyGroup("Inventory/Equipment Presets")]
    [SettingPropertyInteger("Max Presets Per Character", 1, 20, Order = 1,
        HintText = "Maximum saved presets per hero. Default: 10.")]
    public int MaxPresetsPerCharacter { get; set; } = 10;

    [SettingPropertyGroup("Inventory/Equipment Presets")]
    [SettingPropertyBool("Equipment Presets Debug Mode", Order = 2,
        HintText = "Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
    public bool EquipPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Smart Cavalry ---

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry", GroupOrder = 22)]
    [SettingPropertyBool("Enable Smart Cavalry AI", Order = 0,
        HintText = "Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior. Default OFF while war-elephant interaction is being tuned.")]
    public bool EnableSmartCavalryAI { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyBool("Enable Friendly Collision Avoidance", Order = 1,
        HintText = "When charging, cavalry will reroute around friendly infantry on the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
    public bool SmartCavalryAvoidFriendlies { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Charge Formation Strictness", 0.0f, 1.0f, "#0.00", Order = 2,
        HintText = "How tightly the cavalry line must form before charging AND before reform completes. 0 = launch immediately; 1 = wait until every unit is in perfect line. Default 0.7.")]
    public float SmartCavalryChargeStrictness { get; set; } = 0.7f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Reform Distance After Charge", 10f, 80f, "#0", Order = 3,
        HintText = "Meters past the target before cavalry reforms a new line. Larger = wider passthrough sweep. Default 25.")]
    public float SmartCavalryReformDistance { get; set; } = 25f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Charge Line Spacing Multiplier", 0.8f, 3.0f, "#0.0", Order = 4,
        HintText = "Multiplier on default unit spacing during line formation. 1.0 = vanilla. 1.2 (default) = slightly wider line for cleaner charge.")]
    public float SmartCavalryLineSpacing { get; set; } = 1.2f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyBool("Smart Cavalry Debug Mode", Order = 5,
        HintText = "Show diagnostic [SmartCavalryAI] state-transition messages on the in-game HUD. Off = file log only.")]
    public bool SmartCavalryDebug { get; set; } = false;

    // --- Battle Tactics / Companion Roles ---
    // GroupOrder 22 was originally planned but SmartCavalryAI parallel port consumed it.
    // CompanionTactics settings live at GroupOrder 27/28/29.

    [SettingPropertyGroup("Battle Tactics/Companion Roles", GroupOrder = 27)]
    [SettingPropertyBool("Enable Companion Role Tooltips", Order = 0,
        HintText = "Append detected combat role (e.g., [BOW], [INF]) to companion/troop tooltips on the party screen.")]
    public bool EnableCompanionRoleTooltips { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Companion Roles")]
    [SettingPropertyBool("Enable OOB Role Display", Order = 1,
        HintText = "Show role indicators on hero items in the Order of Battle screen.")]
    public bool EnableOOBRoleDisplay { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Companion Roles")]
    [SettingPropertyBool("Companion Roles Debug Mode", Order = 2,
        HintText = "Show diagnostic [CompanionRoles] messages on the in-game HUD.")]
    public bool CompanionRolesDebug { get; set; } = false;

    // --- Battle Tactics / Formation Presets ---

    [SettingPropertyGroup("Battle Tactics/Formation Presets", GroupOrder = 28)]
    [SettingPropertyBool("Enable Formation Presets", Order = 0,
        HintText = "Save/load named OOB hero-to-formation assignments per campaign. Work-in-progress (loading a preset is not yet wired) — off by default; opt in to try it.")]
    public bool EnableFormationPresets { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Formation Presets")]
    [SettingPropertyInteger("Max Formation Presets", 1, 20, Order = 1,
        HintText = "Maximum saved formation presets per campaign. Save attempts beyond this limit are refused with a warning. Default: 10.")]
    public int MaxFormationPresets { get; set; } = 10;

    [SettingPropertyGroup("Battle Tactics/Formation Presets")]
    [SettingPropertyBool("Formation Presets Debug Mode", Order = 2,
        HintText = "Show diagnostic [FormationPresets] messages.")]
    public bool FormationPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Battle Action Bar ---

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 29)]
    [SettingPropertyBool("Enable Battle Action Bar", Order = 0,
        HintText = "Show contextual action bar during field battles (1-9 hotkeys for stance toggles). Stances are display-only — they record state but do not change formation behavior.")]
    public bool EnableBattleActionBar { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Cancel Stance On Move", Order = 1,
        HintText = "Auto-clear stance when the formation receives a movement order.")]
    public bool CancelStanceOnMove { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Enable Volley Fire", Order = 2,
        HintText = "Include 'Volley Fire' as a ranged action option.")]
    public bool EnableVolleyFire { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Battle Action Bar Debug Mode", Order = 3,
        HintText = "Show diagnostic [BattleActionBar] messages.")]
    public bool BattleActionBarDebug { get; set; } = false;

    // --- Map UI / Settlement Nameplates ---

    // --- World / Bandit Scaling ---

    [SettingPropertyGroup("World/Bandit Scaling", GroupOrder = 35)]
    [SettingPropertyBool("Enable Bandit Scaling", Order = 0,
        HintText = "Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below.")]
    public bool EnableBanditScaling { get; set; } = true;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Density Curve", 0.0f, 5.0f, "#0.0", Order = 1,
        HintText = "Multiplier on hideout count + parties-per-hideout at PlayerProgress=1.0. Curve: 1 + curve * progress. 0 = vanilla density throughout. 1.5 (default) = up to 2.5x density in endgame.")]
    public float BanditDensityCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Party Size Curve", 0.0f, 5.0f, "#0.0", Order = 2,
        HintText = "Multiplier on bandit party troop counts at PlayerProgress=1.0. Vanilla already scales 0.4 -> 1.2; this is a final multiplier on top. 1.5 (default) = up to 2.5x bandit party sizes in endgame.")]
    public float BanditPartySizeCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Boss Fight Curve", 0.0f, 5.0f, "#0.0", Order = 3,
        HintText = "Multiplier on first-fight + boss-fight troop counts inside hideouts at PlayerProgress=1.0. 1.5 (default) = up to 2.5x bandits per hideout assault in endgame.")]
    public float BanditBossFightCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Max Hideouts Per Faction Cap", 1, 100, Order = 4,
        HintText = "Hard cap on hideouts per bandit faction regardless of scaling curve. Vanilla = 9. Default: 100 (effectively the physical hideout count per faction).")]
    public int BanditMaxHideoutsPerFaction { get; set; } = 100;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Max Parties Per Hideout Cap", 1, 20, Order = 5,
        HintText = "Hard cap on bandit parties per hideout regardless of scaling curve. Vanilla = 3. Default: 3.")]
    public int BanditMaxPartiesPerHideout { get; set; } = 3;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Initial Hideouts Per Faction", 1, 30, Order = 6,
        HintText = "Hideouts each bandit faction starts with on a new campaign. Vanilla = 7. Default: 14. Higher = denser early game (the world settles toward the steady-state max as you clear them).")]
    public int BanditInitialHideoutsPerFaction { get; set; } = 14;

    // --- World / Recruitment Alignment ---

    [SettingPropertyGroup("World/Recruitment Alignment", GroupOrder = 36)]
    [SettingPropertyBool("Enable Recruitment Alignment Block", Order = 0,
        HintText = "When enabled, a recruiter cannot recruit volunteers at a settlement controlled by an opposed-alignment kingdom (Free vs Evil). Alignment comes from execution/alignment.json, keyed by the kingdom you serve. Neutral factions (Umbar etc.) never block. When off, recruitment is vanilla.")]
    public bool EnableAlignmentRecruitment { get; set; } = true;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Only Good Rejects Evil", Order = 1,
        HintText = "When ON, only a Free-aligned recruiter is blocked from Evil-controlled settlements; Evil recruiters may recruit anywhere. When OFF (default), the block is symmetric — Free and Evil each refuse the other.")]
    public bool AlignmentRecruitmentGoodRejectsEvilOnly { get; set; } = false;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Apply To Player", Order = 2,
        HintText = "When ON (default), YOU are blocked from recruiting in opposed-alignment settlements. When OFF, you may recruit anyone regardless of alignment (AI lords are still gated if 'Apply To AI Lords' is on). The master 'Enable Recruitment Alignment Block' toggle off disables the whole feature for everyone.")]
    public bool EnableAlignmentRecruitmentPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Apply To AI Lords", Order = 3,
        HintText = "When ON (default), AI lords are also blocked from recruiting in opposed-alignment settlements. When OFF, AI recruits freely (you are still gated if 'Apply To Player' is on).")]
    public bool EnableAlignmentRecruitmentAi { get; set; } = true;

    // --- Naval Travel ---

    [SettingPropertyGroup("World/Naval Travel", GroupOrder = 37)]
    [SettingPropertyBool("Enable Naval Travel", Order = 0,
        HintText = "Currently DISABLED — the map's navmesh isn't set up for naval travel yet, so this feature is parked in code and this toggle has no effect for now. (When re-enabled: parties sail across water on the campaign map — the engine's native naval travel, unlocked without the Naval DLC.)")]
    public bool EnableNavalTravel { get; set; } = false;

    [SettingPropertyGroup("World/Naval Travel")]
    [SettingPropertyBool("Apply To Player", Order = 1,
        HintText = "When ON (default), YOUR party can sail. When OFF, you stay land-bound (AI still sails if 'Apply To AI Lords' is on). The master 'Enable Naval Travel' toggle off disables sailing for everyone.")]
    public bool NavalTravelApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Naval Travel")]
    [SettingPropertyBool("Apply To AI Lords", Order = 2,
        HintText = "When ON (default), AI lords' parties can also sail. When OFF, only the player sails and AI stays on land — the conservative option if AI naval routing looks odd.")]
    public bool NavalTravelApplyToAi { get; set; } = true;

    // --- World / Alignment Desertion ---

    [SettingPropertyGroup("World/Alignment Desertion", GroupOrder = 38)]
    [SettingPropertyBool("Enable Alignment Desertion", Order = 0,
        HintText = "When enabled, troops whose culture is opposed in alignment to their lord (Free vs Evil) desert each day — an Evil lord sheds Good troops and a Good lord sheds Evil troops. Alignment comes from execution/alignment.json. Neutral cultures (Umbar etc.) never desert. When off, vanilla.")]
    public bool EnableAlignmentDesertion { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyFloatingInteger("Daily Desertion Rate", 0f, 1f, "#0%", Order = 1,
        HintText = "Fraction of each opposed-alignment troop type that deserts per day (minimum 1 per type). Default 0.50 = half per day. Higher clears mixed parties/garrisons faster.")]
    public float AlignmentDesertionRate { get; set; } = 0.5f;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Player", Order = 2,
        HintText = "When ON (default), YOUR party and garrisons shed opposed-alignment troops. When OFF, you may keep mixed-alignment troops (AI is still affected if 'Apply To AI Lords' is on).")]
    public bool EnableAlignmentDesertionPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To AI Lords", Order = 3,
        HintText = "When ON (default), AI lords' parties and garrisons also shed opposed-alignment troops. When OFF, only the player is affected (if 'Apply To Player' is on).")]
    public bool EnableAlignmentDesertionAi { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Mobile Parties", Order = 4,
        HintText = "When ON (default), lords' field parties (including army members) shed opposed troops. When OFF, only garrisons are affected (if 'Apply To Garrisons' is on).")]
    public bool EnableAlignmentDesertionParties { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Garrisons", Order = 5,
        HintText = "When ON (default), settlement garrisons shed opposed troops (e.g. a conquered fief's old-culture defenders leave). When OFF, only mobile parties are affected (if 'Apply To Mobile Parties' is on).")]
    public bool EnableAlignmentDesertionGarrisons { get; set; } = true;

    // --- Combat Mechanics (CombatMechanics feature — crush-through, cleave, charge knockdown) ---

    [SettingPropertyGroup("Combat Mechanics", GroupOrder = 24)]
    [SettingPropertyBool("Enable Combat Mechanics", Order = 0,
        HintText = "Master toggle for the combat feel pack: skill-based crush-through, monster/orc crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers. When off, everything below is inert and combat behaves exactly as before this feature.")]
    public bool EnableCombatMechanics { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Skill-Based Crush-Through", Order = 1,
        HintText = "High-energy swings from an attacker who greatly outskills the defender have a chance to crush through the block (chance grows with skill gap and swing momentum; off-angle swings are penalized). Tuning constants live in combat_mechanics_config.json.")]
    public bool EnableSkillCrushThrough { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Monster Crush-Through", Order = 2,
        HintText = "Troll, mumakil, war elephant and giant spider melee attacks can only be blocked with a SHIELD — a bare-weapon block is crushed through automatically.")]
    public bool EnableMonsterCrushThrough { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Orc Shield Crush-Through", Order = 3,
        HintText = "AI orc/uruk swings can crush through even SHIELD blocks (energy- and skill-gated) — turtling against an orc horde is no longer safe. Never applies to the player's own attacks.")]
    public bool EnableOrcShieldCrush { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Creature Cleave", Order = 4,
        HintText = "Troll and mumakil swings slice through a victim and carry 30% momentum into the next — one swing can hit multiple soldiers, including through shield blocks that take damage (a zero-damage block still stops the swing).")]
    public bool EnableCreatureCleave { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Creature Stagger Immunity", Order = 5,
        HintText = "Big creatures shrug off hits below a per-creature damage threshold — no flinch-locking a troll with arrows. Thresholds live in combat_mechanics_config.json.")]
    public bool EnableCreatureUnstoppable { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Weight-Based Charge Knockdown", Order = 6,
        HintText = "Mount charges knock down based on relative weight (charger + rider vs victim), charge speed and the victim's race resistance: a mumakil bowls over infantry, a horse can't floor a troll, dwarves stand their ground.")]
    public bool EnableChargeKnockdown { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Shield Penetration", Order = 7,
        HintText = "Missiles from config-listed weapon classes/items (default: javelins) can penetrate shields and damage the soldier behind. Includes a shield-damage correction for a native underestimation bug.")]
    public bool EnableShieldPenetration { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Race Combat Modifiers", Order = 8,
        HintText = "Per-race combat flavor from combat_mechanics_config.json: dwarves resist knockdown and stagger, elves crush through better and strike true from any angle, orcs swing with brute momentum. When off, all races use neutral values.")]
    public bool EnableRaceCombatModifiers { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Crush-Through Max Chance", 0f, 1f, "#0%", Order = 9,
        HintText = "Ceiling for the skill-based crush-through chance, reached at a huge skill gap with a full-momentum swing. Default 50%.")]
    public float CrushThroughMaxChance { get; set; } = 0.5f;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyInteger("Auto-Knockdown Weight Ratio", 2, 30, Order = 10,
        HintText = "Charger-to-victim weight ratio at which a charge ALWAYS knocks the victim down regardless of resistance (mumakil vs man is ~125). Default 8. Lower = heavy cavalry flattens infantry more often. Values below the neutral weight ratio (default 6 = ordinary horse+rider vs man) are treated as the neutral ratio so every plain horse charge doesn't auto-floor.")]
    public int ChargeAutoKnockdownWeightRatio { get; set; } = 8;

    // --- Map UI / Settlement Nameplates ---

    [SettingPropertyGroup("Map UI/Settlement Nameplates", GroupOrder = 40)]
    [SettingPropertyBool("Enable Settlement Nameplate Fade", Order = 0,
        HintText = "Fade settlement nameplates with camera distance. When off, all nameplates display at full visibility regardless of distance (vanilla).")]
    public bool EnableNameplateFade { get; set; } = true;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Fade Start Distance", 5f, 500f, "#0", Order = 1,
        HintText = "Camera distance at which fade begins. Nameplates closer than this stay fully opaque. Default 80.")]
    public float NameplateFadeNearDistance { get; set; } = 80f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Fade End Distance", 10f, 1000f, "#0", Order = 2,
        HintText = "Camera distance at which fade completes. Nameplates farther than this are fully hidden. Must be greater than Fade Start Distance. Default 200.")]
    public float NameplateFadeFarDistance { get; set; } = 200f;

    // --- Map UI / Party Icons ---

    [SettingPropertyGroup("Map UI/Party Icons", GroupOrder = 41)]
    [SettingPropertyFloatingInteger("Map Figure Scale", 0.05f, 1.0f, "#0.00", Order = 0,
        HintText = "Size of party-icon figures and their mounts on the campaign map. Vanilla = 0.30; default 0.15 = half (makes parties feel smaller relative to settlements). Applies on the next icon rebuild after changing.")]
    public float MapFigureScale { get; set; } = 0.15f;

    // --- Map Tools / Distance Cache Rebuild ---
    //
    // Rebuilds Modules/TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin
    // from the live campaign's map scene. The vanilla editor's ComputeAndSave button does the
    // same thing but takes ~108 hours on TAOM's 863-settlement map. Our parallel + smoke-test +
    // checkpoint pipeline brings that to ~30 min (full) or ~30s (incremental, 1-5 settlements
    // moved). Output file replaces the live cache; previous file is preserved as ".prev".
    // Reload the save (or start a new campaign) after the rebuild completes to pick up the
    // new distances.

    [SettingPropertyGroup("Map Tools/Distance Cache Rebuild", GroupOrder = 100)]
    [SettingPropertyButton("Rebuild Settlement Distance Cache",
        RequireRestart = false,
        Content = "Rebuild Now",
        HintText = "Spawns a 10-30 minute background task that recomputes the settlement distance cache against the live map scene. Requires an active campaign. Game stays playable but pathfinding queries during the rebuild may be inconsistent — best run from main menu after loading a save.")]
    public System.Action RebuildDistanceCacheAction { get; set; } = static () =>
    {
        // MCMv5 invokes this delegate directly with no exception handling around the call site.
        // Without the try/catch, an IoC failure (container not yet configured, missing service
        // registration) or constructor throw is silently swallowed by Bannerlord's UI frame —
        // the user clicks the button and nothing visible happens. Surface every failure mode.
        try
        {
            var service = TAOM.IoC.Resolve<TAOM.Features.EditorCacheRebuild.IRuntimeCacheRebuildService>();
            service.Trigger();
        }
        catch (System.Exception ex)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"[TAOM] Cache rebuild FAILED to start: {ex.GetType().Name}: {ex.Message}. See rgl_log_*.txt for details.",
                Colors.Red));
        }
    };
}
