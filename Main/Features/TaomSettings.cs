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
        HintText = "Currently DISABLED — the native MinHook detours (covers_head hand-morph freeze + hair/beard cloth physics) are parked in code and this toggle has no effect for now: the hooks never load and engine rendering is vanilla regardless of this setting. The hook targets remain authored + verified against Bannerlord v1.4.6's TaleWorlds.Native.dll; re-enabling is a code change (uncomment the install branch in SubModule.cs + flip this default back to true).")]
    public bool EnableNativeSkinFixes { get; set; } = false;

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
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan. Default 30.")]
    public int Phase1TriggerDay { get; set; } = 30;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2,
        HintText = "Days after campaign start when all hostile kingdoms go to war and peace between hostile tiers is blocked. Default 44.")]
    public int Phase2TriggerDay { get; set; } = 44;

    [SettingPropertyGroup("War of the Ring/Test Mode")]
    [SettingPropertyBool("Enable Test Mode", Order = 0,
        HintText = "Uses the short delays from war_of_the_ring.json (1/3 days) for rapid testing. Overrides Phase 1/2 days.")]
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

    // --- Battle Tactics / Siege Prop Diagnostics ---
    // Diagnostic only, no gameplay effect. The engine reports NOTHING when a rock pile or ammo
    // barrel is unusable, so this turns that silence into one log line per prop.

    [SettingPropertyGroup("Battle Tactics/Siege Prop Diagnostics", GroupOrder = 23)]
    [SettingPropertyBool("Enable Siege Prop Diagnostics", Order = 0,
        HintText = "Log why rock piles and arrow barrels can or cannot be used in the current mission. Diagnostic only — changes no gameplay. Off by default.")]
    public bool EnableSiegePropDiagnostics { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Siege Prop Diagnostics")]
    [SettingPropertyBool("Siege Prop Diagnostics Verbose", Order = 1,
        HintText = "Log every prop, including working ones. Off = log only props that are unusable, plus the summary.")]
    public bool SiegePropDiagnosticsVerbose { get; set; } = false;

    // --- Battle Tactics / Auto-Resolve Diagnostics ---
    // Diagnostic only, no gameplay effect. Simulated-battle strength reads ONLY troop tier, so
    // before changing that we need to know what real mid-campaign armies look like. Party
    // templates cannot answer it — they only seed a lord's party at spawn.
    //
    // ON by default, unlike the per-event diagnostics: this emits one line per completed battle,
    // not one per hit, and the question is always asked about a session that already happened.
    // The AutoResolveDiagnosticsSettingsProvider fallback must match this default; a test pins both.

    [SettingPropertyGroup("Battle Tactics/Auto-Resolve Diagnostics", GroupOrder = 24)]
    // RequireRestart = false is load-bearing, not cosmetic: MCM's BaseSettingPropertyAttribute
    // defaults it to TRUE, so omitting it tells the player a restart is needed for a toggle that
    // is read on every MapEventEnded and takes effect immediately. Same convention as the sibling
    // diagnostics toggles below.
    [SettingPropertyBool("Log Auto-Resolved Battles", Order = 0, RequireRestart = false,
        HintText = "Master switch. Write one record per completed map battle (composition, sizes, casualties, outcome) to the TAOM log for balance analysis. Off means the feature does nothing at all — no per-battle capture, no cost. Diagnostic only, changes no gameplay. On by default.")]
    public bool LogAutoResolvedBattles { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Auto-Resolve Diagnostics")]
    [SettingPropertyBool("Log Troop Census", Order = 1, RequireRestart = false,
        HintText = "Once per session, dump every troop type's engine-side tier, power, formation class and hit points. This verifies the offline analysis against the running game rather than trusting it — but it is one line per character (~8,300, roughly half the entire log) and the answer only changes when troop data or the balance config does. Capture it once, then leave it off. Requires the master switch above. Off by default.")]
    public bool LogAutoResolveTroopCensus { get; set; } = false;

    // --- Enlistment ---
    // Master switch for the whole serve-as-a-soldier feature. OFF removes the enlist dialog line,
    // so no new service can start. Turning it off MID-SERVICE does not simply freeze the feature:
    // the enlisted player is parked hidden and inactive next to their commander, and the code that
    // un-hides them is the same code this switch disables — so a plain "stop running" would strand
    // them invisible on the map with no menu and no way out. The reconciler therefore performs one
    // honourable discharge on the next tick and hands the player back properly.

    [SettingPropertyGroup("Enlistment", GroupOrder = 41)]
    [SettingPropertyBool("Enable Enlistment", Order = 0, RequireRestart = false,
        HintText = "Serve as a soldier in a lord's party — enlist, take a rank, draw wages, follow the column into battle. Turn OFF to remove the feature: the option to enlist disappears, and if you are already serving you are released honourably (you keep your pay and your gear) rather than being left parked on the map. Takes effect immediately; no restart. Default ON.")]
    public bool EnableEnlistment { get; set; } = true;

    // --- Enlistment / Diagnostics ---
    // Diagnostic only, no gameplay effect. ON by default while the service loop is being diagnosed:
    // with it on, [EnlistDiag] was 4,856 of 6,001 lines (81%) of a 32-minute log — 3,674 of them from
    // one per-map-event line — so turn it off for a quieter log once enlistment is not the subject.
    // Genuine faults (park/sync/restore failure, a stranded PlayerEncounter, a discharge that leaves
    // the player unable to start encounters) are NOT gated and log regardless of this setting.
    //
    // The gated lines emit at INFO, which FileLogger flushes synchronously, so the trace survives a
    // hard native CTD. Measured cost ~0.11 ms/flush (rca-battleload-agentbuild-2026-08-03: 1,287
    // stamps = 145 ms), i.e. ~0.5 s across a 32-minute session. The default here MUST match
    // EnlistmentDiagnosticsSettingsProvider.ResolveEnabled's `?? false` fallback; a test pins each.
    //
    // FLIPPED OFF 2026-08-09, when #375 closed on a field-verified session. The trace shipped ON
    // because the service loop was under active diagnosis, and it earned that: it is what found the
    // battle-join defect (#406), the enlisted-general defect (#424) and the duty exposure (#428).
    // That work is done. Measured on the session that closed #375: 950 of 3,452 log lines were
    // enlistment, and FIVE gated message shapes accounted for 851 of them — TICK (202), SYNC ok
    // (199), PARK ok (23), RESTORE ok (12) and the per-map-event line (450). The 52 ungated INFO
    // sites produced the other ~99, and those are the ones worth keeping: oath, joins, duties,
    // promotion, rewards, discharge. A player now carries the events and none of the trace.

    [SettingPropertyGroup("Enlistment/Diagnostics", GroupOrder = 42)]
    [SettingPropertyBool("Enable Enlistment Diagnostics", Order = 0, RequireRestart = false,
        HintText = "Log the routine enlistment trace ([EnlistDiag] TICK, SYNC ok, PARK ok, RESTORE ok) to the TAOM debug log. OFF by default: it is roughly ten times the volume of the ordinary enlistment log and exists for diagnosing the service loop, not for playing. Turn it ON before reproducing an enlistment problem you intend to report. Real faults are always logged regardless of this setting, so [EnlistDiag] lines will still appear when it is off. Takes effect immediately; no restart. Default OFF.")]
    public bool EnableEnlistmentDiagnostics { get; set; } = false;

    // --- Battlefield Promotions (Field Commission) ---
    // Every default here MUST equal its counterpart in
    // Main/_Module/ModuleData/field_commission/field_commission_config.json — a player without MCM
    // reads the JSON, a player with MCM at its shipped default reads these literals, and the two must
    // describe the same game. FieldCommissionSettingsProviderTests.CompiledMcmDefaults_MatchShippedJsonDefaults
    // fails the build if they drift.
    //
    // Turning the master switch OFF is fully inert and fully reversible: no merit accrues, no offer is
    // queued or shown, and already-banked merit stays in the save. Companions already promoted are
    // ordinary companions and are unaffected — nothing is taken back.

    [SettingPropertyGroup("Battlefield Promotions", GroupOrder = 43)]
    [SettingPropertyBool("Enable Battlefield Promotions", Order = 0, RequireRestart = false,
        HintText = "Soldiers who do the killing in a hard-won battle can be promoted into named companions. Turn OFF to stop it entirely: no merit is earned, no promotion is ever offered. Companions you already promoted stay exactly as they are, and banked merit is kept in case you turn it back on. Takes effect immediately; no restart. Default ON.")]
    public bool EnableFieldCommission { get; set; } = true;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Offers Per Battle", 1, 20, Order = 1, RequireRestart = false,
        HintText = "The most promotion prompts a single won battle may raise. Each prompt pauses the game, so a large number after a large battle means a long queue of dialogs. Merit above the cap is not lost — it carries to the next battle. Default: 2.")]
    public int FieldCommissionMaxOffersPerBattle { get; set; } = 2;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyFloatingInteger("Fair-Fight Ratio", 0.1f, 3.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "How outnumbered you must be for a battle to count. Your party's healthy troops divided by the whole enemy side must come in UNDER this. 1.00 = only fights where you are outnumbered; higher = easier fights still earn merit. Default: 1.30.")]
    public float FieldCommissionRatioThreshold { get; set; } = 1.3f;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Merit Per Kill", 1, 10, Order = 3, RequireRestart = false,
        HintText = "Merit a troop type banks for each enemy its soldiers kill in a qualifying battle. Higher = faster promotions. Default: 1.")]
    public int FieldCommissionMeritPerKill { get; set; } = 1;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Merit To Promote", 1, 100, Order = 4, RequireRestart = false,
        HintText = "Merit a troop type must bank before it earns a promotion offer. Note merit is pooled per troop TYPE, not per soldier — a large stack of one type reaches the bar faster than a small one. Higher = rarer promotions. Default: 32.")]
    public int FieldCommissionMeritThreshold { get; set; } = 32;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Companions Over The Limit", 0, 10, Order = 5, RequireRestart = false,
        HintText = "How many promoted companions you may take beyond your clan tier's companion limit. 0 = the limit is strict and an offer waits until you have room. Default: 0.")]
    public int FieldCommissionRetainerAllowance { get; set; } = 0;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyBool("Promotion Diagnostics", Order = 6, RequireRestart = false,
        HintText = "Write a detailed [FieldCommission] trace to the TAOM debug log — which battles counted, which troops earned merit, which offers were raised, and every completed promotion. Turn this on before reproducing a promotion problem so the log you send answers it. Real faults are logged regardless of this setting. Off by default.")]
    public bool EnableFieldCommissionDiagnostics { get; set; } = false;

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

    // --- Prisoner Recruitment ---

    [SettingPropertyGroup("World/Prisoner Recruitment", GroupOrder = 39)]
    [SettingPropertyBool("Waive Same-Side Prisoner Morale Cost", Order = 0,
        HintText = "When enabled (default), recruiting a prisoner of your own faction or your own side of the war costs no morale — an Isengard captor loses nothing taking on Mordor, Gundabad or Dunland troops, since all serve Sauron. Alignment comes from execution/alignment.json. Neutral factions (Khand, Umbar) only waive their own culture, not each other. When off, prisoner morale is vanilla (-1 per troop, -2 per bandit).")]
    public bool EnablePrisonerRecruitmentWaiver { get; set; } = true;

    [SettingPropertyGroup("World/Prisoner Recruitment")]
    [SettingPropertyBool("Apply To Player", Order = 1,
        HintText = "When ON (default), YOU pay no morale for same-side prisoners. When OFF, you pay the vanilla cost (AI lords still get the waiver if 'Apply To AI Lords' is on).")]
    public bool EnablePrisonerRecruitmentWaiverPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Prisoner Recruitment")]
    [SettingPropertyBool("Apply To AI Lords", Order = 2,
        HintText = "When ON (default), AI lords also pay no morale for same-side prisoners, so allied factions absorb each other's captured troops more readily. When OFF, only you benefit (if 'Apply To Player' is on).")]
    public bool EnablePrisonerRecruitmentWaiverAi { get; set; } = true;

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

    // --- Aura of Dread ---

    [SettingPropertyGroup("Aura of Dread", GroupOrder = 34)]
    [SettingPropertyBool("Enable Aura of Dread", Order = 0,
        HintText = "Nazgul and Sauron drain the morale of nearby enemy troops, breaking formations that stand too close for too long. Takes effect immediately, but morale already drained is not restored when you switch it off.")]
    public bool EnableDreadAura { get; set; } = true;

    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyFloatingInteger("Dread Radius", 4f, 30f, "#0", Order = 1,
        HintText = "Metres over which a wraith projects dread. Full strength within the inner radius from dread_aura_config.json (default 4m), falling to nothing at this distance. Default 12. Values above 30 are refused because the engine's proximity search degrades to a full agent scan.")]
    public float DreadAuraRadius { get; set; } = 12f;

    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyFloatingInteger("Morale Drained Per Second", 0f, 20f, "#0.0", Order = 2,
        HintText = "Morale per second drained at full strength, before the target's tier/hero resistance and its racial resistance. At the default 5.0 a tier-3 human breaks after about 28 seconds of contact, a tier-6 elite about 49, and an elven hero never. Below about 1.0 the engine's own morale recovery outpaces the drain and nothing ever routs.")]
    public float DreadAuraMoralePerSecond { get; set; } = 5f;

    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyBool("Affects Your Own Troops", Order = 3,
        HintText = "When off, troops on your team are immune and dread only touches the AI. Leave it on and an enemy wraith can rout your soldiers, and routed troops are LOST from your party, not merely shaken. Your own character is always immune either way: the engine gives player-controlled agents no morale component at all.")]
    public bool DreadAuraAffectsPlayerTroops { get; set; } = true;

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

    // --- Kingdom Politics / Fief Grants ---
    //
    // #458. When a kingdom takes a town or castle it holds an election for the new owner, and
    // vanilla's ballot cannot change the result: every candidate backs itself at roughly 40x what it
    // gives a rival, so all three finalists tie and the highest-merit clan wins outright. These knobs
    // move that merit score, plus the King's Vote that lets a rich ruler overrule the council.
    // Weights are read live per election, so no restart and no new campaign. The master toggle is
    // applied when a decision is CREATED, so it does not retrofit an already-pending election.

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("Enable Fief Grant Rebalance", Order = 0, RequireRestart = false,
        HintText = "Rebalances who gets a captured town or castle. Off = exact vanilla, where the ruling clan's +60 merit and its unlimited King's Vote override let it take fief after fief. Does not change starting ownership.")]
    public bool EnableFiefGrantRebalance { get; set; } = true;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Capturer Bonus", 1.0f, 5.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Merit multiplier for the clan that actually stormed the place. Vanilla gives it a flat +30, less than the ruling clan's +60, so the conqueror routinely loses its own siege. 1.00 = no bonus. Default: 2.50.")]
    public float FiefGrantCapturerBonus { get; set; } = 2.5f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Landless Clan Bonus", 1.0f, 5.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Merit multiplier for a clan that holds no town or castle at all. Keeps poor clans able to field parties instead of withering and leaving the kingdom. 1.00 = no bonus. Default: 2.00.")]
    public float FiefGrantLandlessBonus { get; set; } = 2.0f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Concentration Penalty", 0.0f, 1.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "How hard each fief a clan already holds counts against its next claim, as 1/(1 + fiefs x penalty). Vanilla only divides by the VALUE it holds, so a clan sitting on many cheap castles is barely damped. 0.00 = off. Default: 0.35.")]
    public float FiefGrantConcentrationPenalty { get; set; } = 0.35f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Culture Match Bonus", 1.0f, 3.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "Merit multiplier when the clan's culture matches the settlement's. Vanilla has no equivalent, which is how an orc clan ends up holding an elven city. 1.00 = off. Default: 1.50.")]
    public float FiefGrantCultureMatchBonus { get; set; } = 1.5f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Culture Mismatch Penalty", 0.1f, 1.0f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Merit multiplier when it does not match. Lower means foreign clans rarely win a settlement of another culture. 1.00 = off. Default: 0.60.")]
    public float FiefGrantCultureMismatchPenalty { get; set; } = 0.6f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Ruling Clan Factor", 0.1f, 2.0f, "#0.00", Order = 6, RequireRestart = false,
        HintText = "Merit multiplier for the king's own clan, which vanilla already hands a flat +60 on top of its tier and strength. Below 1.00 damps the crown's claim. 1.00 = vanilla. Default: 0.75.")]
    public float FiefGrantRulingClanFactor { get; set; } = 0.75f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("King's Vote Fief Share Cap", 0.0f, 1.0f, "#0.00", Order = 7, RequireRestart = false,
        HintText = "Share of the kingdom's towns and castles above which the ruling clan loses its right to overrule the council. Vanilla lets a king with enough influence override every grant, and his pick is always himself. 1.00 = vanilla. Default: 0.34.")]
    public float FiefGrantKingsVoteFiefShareCap { get; set; } = 0.34f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("Apply Penalties To Your Clan", Order = 8, RequireRestart = false,
        HintText = "On = your clan is scored like any other, including the concentration and culture-mismatch penalties. Off = you keep the capturer, landless and culture-match bonuses but are never damped for what you already hold. Default: off.")]
    public bool FiefGrantApplyPenaltiesToPlayerClan { get; set; } = false;

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
