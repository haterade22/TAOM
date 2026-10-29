using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TAOM.Features.AiPartySize;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features;

public class TaomSettings : AttributeGlobalSettings<TaomSettings>
{
    // Hides MCM's GlobalSettings<T>.Instance: a dedicated server, where MCM is not loaded, reads
    // taom-server-settings.json (ServerSettingsSource); a client gets MCM's own. Name the base explicitly:
    // a bare Instance here would recurse.
    public static new TaomSettings? Instance =>
        ServerSettings.ServerSettingsSource.Get<TaomSettings>() ?? GlobalSettings<TaomSettings>.Instance;

    public override string Id => "TAOM";
    public override string DisplayName => "TAOM - Tales From the Age of Men";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    // --- Encyclopedia ---

    [SettingPropertyGroup("Encyclopedia")]
    [SettingPropertyBool("Show All Characters", Order = 0, RequireRestart = false,
        HintText = "Reveals all characters in the encyclopedia, including those not yet encountered. Equivalent to the 'campaign.toggle_information_restrictions' cheat.")]
    public bool ShowAllEncyclopediaCharacters { get; set; } = true;

    // --- Troop Weight ---

    [SettingPropertyGroup("Troop Weight")]
    // RequireRestart = false is load-bearing here, exactly as it is for the AI Party Size group.
    // Every gate reads TaomSettings.Instance live, so flipping this DOES stop the six display patches
    // and the penalty immediately. What does not stop is the engine: PartyBase.PartySizeLimit caches
    // the already-deflated number keyed on MemberRoster.VersionNo (PartyBase.cs:343-355, v1.4.8), and
    // changing a setting does not bump that counter. So the counts revert while the enforced cap stays
    // reduced, which reads as "the toggle does nothing". AiPartySizeSettingsWatcher already flushes
    // exactly this (it sweeps MobileParty.All calling MemberRoster.UpdateVersion(), is wired
    // unconditionally in SubModule.cs, and fires on ANY SaveTriggered, so it covers this toggle too),
    // but MCM's BaseSettingPropertyAttribute defaults RequireRestart to TRUE, and with it true the only
    // path reaching SaveSettings also quits the game, so SaveTriggered never arrives mid-session and the
    // watcher never runs. Player-reported 2026-09-06.
    [SettingPropertyBool("Enable Troop Weight", Order = 0, RequireRestart = false,
        HintText = "Weighted party size: elite units consume more party capacity. Elites 2x, level 41+ elites 3x, warg riders 2x, cave trolls 4x, mumakil 10x.")]
    public bool EnableTroopWeight { get; set; } = true;

    // --- AI Party Size ---

    // GroupOrder matters more than it looks: MCM sorts top-level groups by GroupOrder DESCENDING
    // (MCM.Abstractions.CollectionExtensions.SortDefault), then by name descending. Without an
    // explicit order this group sat at 0 and, starting with "A", fell to the very bottom of that
    // bucket: 24th of 32 groups, about 150 rows down, which is why it read as missing entirely.
    // 44 places it just under Supply Lines (45). Note the asymmetry with Order on the properties
    // below, which MCM sorts ASCENDING.
    [SettingPropertyGroup("AI Party Size", GroupOrder = 44)]
    [SettingPropertyBool("Enable AI Party Scaling", Order = 0, RequireRestart = false,
        HintText = "Lets AI lord parties HOLD the roster their party template spawns them with. Without this they spawn at 500-3000 and are trimmed back to the vanilla 50-150 cap within a day. Also relieves AI food and wage pressure, because those drive a second, morale-based desertion path that ignores the cap entirely. Off restores the pre-feature behaviour. Note that is not the same as raw vanilla: the Troop Weight elite tax has its own toggle and still deflates a heavy party's limit when it is on.")]
    public bool EnableAiPartyScaling { get; set; } = true;

    // Renamed from AiLordPartySizeFactor (#1017): its default went 5 to 1 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 5.
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyFloatingInteger("AI Lord Party Size Multiplier", 1.0f, 20.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "Multiplies an AI lord's party size limit. Preserves clan-tier progression, so a tier-4 lord still outgrows a tier-1. 1.0 = vanilla, which is the default: this feature ships neutral and you opt in. Raise this and the Flat Bonus together: the bonus is added after the multiplier, so moving one alone shifts the total by less than you expect. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiLordPartyScale { get; set; } = AiPartySizeService.DefaultLordFactor;

    // Renamed from AiLordPartySizeFlatBonus (#1017): its default went 150 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 150.
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyFloatingInteger("AI Lord Party Size Flat Bonus", 0.0f, 2000.0f, "#0", Order = 2, RequireRestart = false,
        HintText = "Added to an AI lord's party size limit AFTER the multiplier, so it is worth exactly this many men. Exists because template spawn size does not scale with clan tier: under a multiplier alone, low-tier lords still shed. Default: 0. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiLordPartyFlatBonus { get; set; } = AiPartySizeService.DefaultLordFlatBonus;

    // Renamed from AiGarrisonSizeFactor (#1017): its default went 3 to 1 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 3.
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyFloatingInteger("Garrison Size Multiplier", 1.0f, 10.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "Multiplies every settlement garrison's size limit, player-owned included. Siege balance, not an AI handicap: lords fielding thousands would walk over garrisons still capped near vanilla's 200. Raising this also gives lords more room to donate troops on entering a friendly fief. 1.0 = vanilla, which is the default. IMPORTANT: raise this only alongside the AI Lord multiplier above. Vanilla refuses to even target a settlement when the attacker is under twice the defender's strength, and that strength counts garrison AND militia, so a high garrison multiplier with vanilla-sized lord parties stops AI sieges happening at all. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiGarrisonScale { get; set; } = AiPartySizeService.DefaultGarrisonFactor;

    // Renamed from AiFoodConsumptionRelief (#1017): its default went 0.9 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 0.9.
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyFloatingInteger("AI Food Relief", 0.0f, 0.95f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "Fraction of an AI lord party's food consumption waived. A large party cannot buy 30 days of food from any town, so it starves permanently, and starvation is -30 morale which opens vanilla's morale-desertion path. 0 = vanilla, which is the default. Only worth raising if you have raised the AI Lord multiplier: at vanilla party sizes nothing starves. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiPartyFoodRelief { get; set; } = AiPartySizeService.DefaultFoodRelief;

    // Renamed from AiWageRelief (#1017): its default went 0.9 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 0.9.
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyFloatingInteger("AI Wage Relief", 0.0f, 0.95f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Fraction of an AI lord party's wage bill waived. AI clan wage budgets are set from clan gold and a large party blows past them, pinning unpaid wages at -20 morale and opening the same desertion path. 0 = vanilla, which is the default. Only worth raising if you have raised the AI Lord multiplier. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiPartyWageRelief { get; set; } = AiPartySizeService.DefaultWageRelief;

    // Resolved by SelectedIndex, so the option ORDER here is load-bearing and must stay in step with
    // PlayerClanScalingMode. Only party size crosses to the player: the food and wage relief stay
    // AI-only regardless of this setting, because neither pressure they compensate for can occur for
    // a player clan (ClanVariablesCampaignBehavior guards `clan != Clan.PlayerClan` before setting the
    // wage cap, and BuyFoodInternal early-returns on IsMainParty).
    [SettingPropertyGroup("AI Party Size")]
    [SettingPropertyDropdown("Apply Party Size To Player Clan", Order = 6, RequireRestart = false,
        HintText = "Whether YOUR clan's lord parties get the same size scaling as AI lords. 'Taken-over lords only' (default) applies it when you started as an existing lord via Player Switcher, whose clan was given AI-sized rosters at world generation and would otherwise shed thousands of men. Detected from the player clan's id, so it survives save/load. 'Always' also applies it to an ordinary campaign start. 'Never' is the pre-2026-09-01 behaviour. Food and wage relief are never granted to your clan either way.")]
    public Dropdown<string> AiPlayerClanPartyScaling { get; set; } = new Dropdown<string>(
        new[] { "Never", "Taken-over lords only", "Always" }, (int)AiPartySizeService.DefaultPlayerClanScaling);

    // --- Settlement Food ---

    [SettingPropertyGroup("Settlement Food")]
    [SettingPropertyBool("Enable Settlement Food Tuning", Order = 0, RequireRestart = false,
        HintText = "Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
    public bool EnableSettlementFoodTuning { get; set; } = true;

    // --- Settlement Economy ---

    [SettingPropertyGroup("Settlement Economy")]
    [SettingPropertyBool("Enable Settlement Economy Tuning", Order = 0, RequireRestart = false,
        HintText = "Regenerates town market gold toward a higher target (base 25000 vs vanilla 10000) so drained town markets recover — tunable in settlement_economy/settlement_economy_config.json (base, gold per prosperity, daily regen rate). Applies to existing saves immediately. Off = vanilla engine gold math. Config edits need an app restart.")]
    public bool EnableSettlementEconomyTuning { get; set; } = true;

    // --- Caravan Trade ---

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyBool("Enable Caravan Trade Overhaul", Order = 0, RequireRestart = false,
        HintText = "Makes AI/player caravans range beyond the local town cluster instead of shuttling between very-close towns, trade across the endless Free-vs-Evil war (per War Trade Policy below), and carry fuller baskets. Off = exact vanilla caravan behavior. Advanced curve knobs live in caravan_trade/caravan_trade_config.json; config edits need an app restart.")]
    public bool EnableCaravanTrade { get; set; } = true;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyBool("Apply To Player Caravans", Order = 1, RequireRestart = false,
        HintText = "When on, your OWN caravans also range further, trade cross-war, and buy fuller baskets (they may enter contested territory and risk attack). Off scopes the selection re-weight + basket diversity off your clan's caravans. Note: the wider distance ceiling is engine-global (shared by all caravans) and the war-trade gate scopes at your whole faction, so those two levers aren't fully isolated to non-player caravans — but with this off your caravans still route by vanilla's nearest-first selection.")]
    public bool CaravanTradeApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyFloatingInteger("Caravan Range Multiplier", 1.0f, 4.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "How much further caravans range past the vanilla distance ceiling. 1.0 = vanilla reach; higher = they visit more distant, more profitable markets. Default: 1.6.")]
    public float CaravanRangeMultiplier { get; set; } = 1.6f;

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyDropdown("War Trade Policy", Order = 3, RequireRestart = false,
        HintText = "Which towns caravans may trade with despite the war. Vanilla = war blocks trade (the cause of the shuttle). Same Side + Neutral = trade with same-alignment or neutral factions but not the enemy side (default, lore-friendly). Ignore War = trade anywhere non-besieged.")]
    public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
        new[] { "Vanilla (war blocks)", "Same Side + Neutral", "Ignore War" }, 1);

    [SettingPropertyGroup("Caravan Trade")]
    [SettingPropertyFloatingInteger("Basket Diversity Floor", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "Raises poor caravans' buying power so they stock several goods instead of one. 0 = vanilla (poor caravans buy a single item); higher = fuller baskets. Default: 0.35.")]
    public float CaravanBudgetDiversityFloor { get; set; } = 0.35f;

    // --- Castle Recruitment ---

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyBool("Enable Castle Recruitment", Order = 0, RequireRestart = false,
        HintText = "When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
    public bool EnableCastleRecruitment { get; set; } = true;

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyBool("AI Recruits From Castles", Order = 1, RequireRestart = false,
        HintText = "When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
    public bool EnableCastleRecruitmentAi { get; set; } = true;

    [SettingPropertyGroup("Castle Recruitment")]
    [SettingPropertyInteger("Notables Per Castle", 1, 5, Order = 2, RequireRestart = false,
        HintText = "How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
    public int CastleNotablesPerCastle { get; set; } = 3;

    // --- Elite Emissary ---

    [SettingPropertyGroup("Elite Emissary")]
    [SettingPropertyBool("Enable Elite Emissary", Order = 0, RequireRestart = false,
        HintText = "At a faction's key settlements (capitals), speak with the faction emissary to buy that faction's elite troops for its special resource (Castar, War Spoils, Gems...). Conquering a settlement flips its offerings to the new owner.")]
    public bool EnableEliteEmissary { get; set; } = true;

    [SettingPropertyGroup("Elite Emissary")]
    [SettingPropertyBool("Hide Emissary Without Resource", Order = 1, RequireRestart = false,
        HintText = "When on, the emissary option is hidden at settlements whose owner faction has no special resource. When off, the option still appears but is disabled with an explanatory hint.")]
    public bool HideEmissaryWhenNoResource { get; set; } = true;

    // --- Tournaments ---

    [SettingPropertyGroup("Tournaments")]
    [SettingPropertyInteger("Max Bet Per Round", 0, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxBetSetting, Order = 0, RequireRestart = false,
        HintText = "The most you may bet in each tournament round. 0 = unlimited (up to your purse). Roguery's Deep Pockets doubles any other value. Vanilla is 150. Default: 0.")]
    public int TournamentMaxBetPerRound { get; set; } = 0;

    [SettingPropertyGroup("Tournaments")]
    [SettingPropertyFloatingInteger("Renown Multiplier", 0.0f, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxMultiplier, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Scales the renown a tournament winner earns (base: vanilla's 3 plus 1 per lord or hero in the field, times the winner's culture factor). 1.00 = as designed. Applies to every winner. Default: 1.00.")]
    public float TournamentRenownMultiplier { get; set; } = 1.0f;

    [SettingPropertyGroup("Tournaments")]
    [SettingPropertyFloatingInteger("Influence Multiplier", 0.0f, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxMultiplier, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Scales the influence a tournament winner earns in a town of their own kingdom (base: 2 plus 1 per 4 lords or heroes in the field, times the winner's culture factor). 1.00 = as designed. Default: 1.00.")]
    public float TournamentInfluenceMultiplier { get; set; } = 1.0f;

    [SettingPropertyGroup("Tournaments")]
    [SettingPropertyBool("Keep Enemy Sides Out", Order = 3, RequireRestart = false,
        HintText = "A Free People's tournament seats no lord, wanderer or troop of the Shadow, and a tournament of the Shadow seats none of the Free Peoples; each is replaced by one of the town's own troops. A town's side is its owner's, so a captured town follows its conqueror, and a town held by a neutral realm bars nobody. Neutral lords and wanderers, the troops the town itself recruits, you and your clan always compete. Fewer lords in the field means a lesser prize. Default: on.")]
    public bool TournamentKeepEnemySidesOut { get; set; } = true;

    [SettingPropertyGroup("Tournaments")]
    [SettingPropertyBool("Tidings of the Lists", Order = 4, RequireRestart = false,
        HintText = "Adds an option to a town's arena menu: for a small fee the arena master names the nearest towns holding a tournament now that are not at war with your realm. It marks nothing on the map. When off, the option is hidden. Single-player only. Default: on.")]
    public bool EnableTidingsOfTheLists { get; set; } = true;

    // --- Armour Acquisition ---

    [SettingPropertyGroup("Armour Acquisition")]
    [SettingPropertyBool("Enable Armour Acquisition", Order = 0, RequireRestart = false,
        HintText = "Markets and battle loot hand out light and medium armour freely. Heavy, elite and lord pieces are sold only where a town's armoury, its Barracks, is high enough; lord kit can also be forged there or earned piece by piece on the lord's gear ladder, whose last rung awards the named hero weapons; they are never sold or looted. Visit a town's armoury to upgrade a piece with metals. Takes effect from the next game load.")]
    public bool EnableArmourAcquisition { get; set; } = true;

    [SettingPropertyGroup("Armour Acquisition")]
    [SettingPropertyBool("Lord's Harness After Battle", Order = 1, RequireRestart = false,
        HintText = "After a battle your side wins against an enemy lord, you may rarely find his household's lord harness among the spoils. Needs Armour Acquisition on.")]
    public bool EnableArmourLordEvent { get; set; } = true;

    [SettingPropertyGroup("Armour Acquisition")]
    [SettingPropertyBool("Visiting Master Armourers", Order = 2, RequireRestart = false,
        HintText = "Now and then a master armourer visits a town for a week, raising its armoury one level. Needs Armour Acquisition on.")]
    public bool EnableVisitingArmourer { get; set; } = true;

    // --- Culture Conversion ---

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Enable Culture Conversion", Order = 0, RequireRestart = false,
        HintText = "When enabled, a town/castle (and its villages) conquered by a different culture gradually adopts the new owner's culture — producing their troops, militia, and identity. Disabling stops NEW conversions; already-converted settlements stay converted.")]
    public bool EnableCultureConversion { get; set; } = true;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyInteger("Days To Convert", 1, 365, Order = 1, RequireRestart = false,
        HintText = "Days the new owner must hold a cross-culture fief before it converts. Lower = faster cultural takeover. Default: 1 (near-instant on capture).")]
    public int CultureConversionHoldDays { get; set; } = 1;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Require Stable Loyalty", Order = 2, RequireRestart = false,
        HintText = "When enabled, a conquered fief only converts once its loyalty is high enough (configured in culture_conversion_config.json), so a city in unrest never flips. Default: off.")]
    public bool CultureConversionRequireStableLoyalty { get; set; } = false;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Replace Notables On Conversion", Order = 3, RequireRestart = false,
        HintText = "When enabled, conversion also replaces the settlement's notables with ones of the new culture (a Mordor-held Gondor town gets orc notables). Their workshops, alleys and caravans transfer to the newcomers; your relations with the old notables do not. Default: on.")]
    public bool CultureConversionReplaceNotables { get; set; } = true;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Replace Garrison On Conversion", Order = 4, RequireRestart = false,
        HintText = "When enabled, conversion re-mans the fief's garrison with the new culture's troops, matching tier, battlefield role and head count (a Gondor tier-4 archer becomes a Mordor tier-4 archer). Without this a fief taken by gift, barter or a king's decision keeps its old culture's garrison forever. Default: on.")]
    public bool CultureConversionReplaceGarrison { get; set; } = true;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Replace Militia On Conversion", Order = 5, RequireRestart = false,
        HintText = "When enabled, conversion also swaps the settlement's and its villages' existing militia to the new culture's militia troops. New militia already spawn in the new culture once the fief converts; this makes the standing militia catch up the same day instead of over many. Default: on.")]
    public bool CultureConversionReplaceMilitia { get; set; } = true;

    [SettingPropertyGroup("Culture Conversion")]
    [SettingPropertyBool("Replace Garrison In Your Own Fiefs", Order = 6, RequireRestart = false,
        HintText = "When disabled, the two swaps above never touch a fief your clan owns, so a garrison you stacked by hand survives the conversion. AI fiefs still swap, and your fief still changes culture. Default: on.")]
    public bool CultureConversionReplaceGarrisonPlayerFiefs { get; set; } = true;

    // --- War of the Ring ---

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyBool("Enable War of the Ring", Order = 0, RequireRestart = false,
        HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
    public bool WarOfTheRingEnabled { get; set; } = true;

    // Renamed from Phase1TriggerDay (#1017): its default went 2 to 30 in v2.0.15 (e273cd23f) under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 2.
    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1, RequireRestart = false,
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan. Default 30. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int WarPhase1TriggerDay { get; set; } = 30;

    // Renamed from Phase2TriggerDay (#1017): its default went 14 to 44 in v2.0.15 (e273cd23f) under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 14.
    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2, RequireRestart = false,
        HintText = "Days after campaign start when all hostile kingdoms go to war and peace between hostile tiers is blocked. Default 44. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int WarPhase2TriggerDay { get; set; } = 44;

    [SettingPropertyGroup("War of the Ring/Test Mode")]
    [SettingPropertyBool("Enable Test Mode", Order = 0, RequireRestart = false,
        HintText = "Uses the short delays from war_of_the_ring.json (1/3 days) for rapid testing. Overrides Phase 1/2 days.")]
    public bool TestMode { get; set; }

    // --- War of the Ring / Momentum (#327) ---

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Enable Momentum Tracking", Order = 0, RequireRestart = false,
        HintText = "Tracks Evil vs Good war progress from battles, sieges, raids, armies and relative strength. Reaching the victory threshold ENDS the war. Default: on.")]
    public bool MomentumEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Show Map Meter", Order = 1, RequireRestart = false,
        HintText = "Shows the on-map War of the Ring slider once the full war begins. Click it for the detailed breakdown. Default: on.")]
    public bool ShowWarOfTheRingMapMeter { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Enable Victory (End the War)", Order = 6, RequireRestart = false,
        HintText = "When OFF (default) the War of the Ring is endless — momentum is tracked but no side ever wins. Turn ON to let a side win at the Victory Threshold (or by eliminating the other), which ends the war and makes peace. Default: off (endless).")]
    public bool MomentumVictoryEnabled { get; set; } = false;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyInteger("Victory Threshold", 100, 2000, Order = 2, RequireRestart = false,
        HintText = "Momentum lead one side needs to win the war outright. Default 500.")]
    public int MomentumVictoryThreshold { get; set; } = 500;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyFloatingInteger("Player Participation Multiplier", 1.0f, 3.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "Momentum gains are multiplied by this when your party takes part in the event. Default 1.5.")]
    public float MomentumParticipationMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyBool("Require Player Participation For Victory", Order = 4, RequireRestart = false,
        HintText = "When on, the war cannot end until you have fought in enough momentum events yourself. Default: on.")]
    public bool MomentumRequirePlayerForVictory { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Momentum")]
    [SettingPropertyInteger("Minimum Player Events For Victory", 0, 20, Order = 5, RequireRestart = false,
        HintText = "How many battles/sieges you must take part in before either side can win. Default 5.")]
    public int MomentumMinPlayerEvents { get; set; } = 5;

    // --- War of the Ring / Rally (#765) ---

    [SettingPropertyGroup("War of the Ring/Rally")]
    [SettingPropertyBool("Enable Rally", Order = 0, RequireRestart = false,
        HintText = "Gives AI kingdoms that have lost a large share of their towns and castles a small, temporary boost: a higher daily volunteer roll in their towns, castles and villages (empty recruit slots fill faster and waiting recruits are promoted sooner) and faster escapes for their captured lords. Never applies to a kingdom you rule, or to your own clan's settlements and lords; its lords in your prisons escape faster too. Takes effect at the next daily tick; turning it off removes every rally effect. Default on.")]
    public bool WarRallyEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Rally")]
    [SettingPropertyFloatingInteger("War Effect Strength", 0.0f, 2.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Scales every War of the Ring catch-up and event effect (a higher daily volunteer roll, which fills recruit slots faster and promotes waiting recruits sooner; faster escapes of captured lords). 0 turns them off, 1 is the authored size, 2 doubles it. Default 1.")]
    public float WarEffectStrength { get; set; } = 1.0f;

    // --- War of the Ring / Eye of Sauron (#765 M8) ---
    // Defaults live in EyeOfSauronSettingsProvider, the single source; the provider re-validates every value.

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyBool("Enable the Hunt", Order = 0, RequireRestart = false,
        HintText = "When on, great deeds against the enemy draw a hunter: a Free player is hunted by one of the Nine with a host of Mordor (of Dol Guldur for its four wraiths), an Evil player by Glorfindel with a host of the Eldar. Only while your kingdom is at war with the hunter's. Single-player only. Default: on.")]
    public bool EyeOfSauronEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyFloatingInteger("Deed Threshold", EyeOfSauron.EyeOfSauronSettingsProvider.MinThreshold, EyeOfSauron.EyeOfSauronSettingsProvider.MaxThreshold, "#0.0", Order = 1, RequireRestart = false,
        HintText = "Deed score that draws a hunt. A victory over an enemy kingdom in the field, a village raid or a forced levy scores 1, a sally or a battle outside a siege 2, a siege assault 3. Default 10.")]
    public float EyeThreshold { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultThreshold;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyFloatingInteger("Deed Half-Life (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinHalfLifeDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxHalfLifeDays, "#0", Order = 2, RequireRestart = false,
        HintText = "Days for the deed score to fall by half. Default 30.")]
    public float EyeHalfLifeDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultHalfLifeDays;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyInteger("Cooldown (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinCooldownDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxCooldownDays, Order = 3, RequireRestart = false,
        HintText = "Days after a hunt ends before another can start. Default 60.")]
    public int EyeCooldownDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultCooldownDays;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyInteger("Hunt Timeout (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinHuntTimeoutDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxHuntTimeoutDays, Order = 4, RequireRestart = false,
        HintText = "Days a hunt lasts before the hunter gives up. Default 20.")]
    public int HuntTimeoutDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultHuntTimeoutDays;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyFloatingInteger("Hunter Speed Multiplier", EyeOfSauron.EyeOfSauronSettingsProvider.MinSpeedMultiplier, EyeOfSauron.EyeOfSauronSettingsProvider.MaxSpeedMultiplier, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Once the hunter is within one and a half times your seeing range, it moves at least this many times your speed. Default 1.3.")]
    public float HunterSpeedMultiplier { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultSpeedMultiplier;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyFloatingInteger("Hunter Strength Ratio", EyeOfSauron.EyeOfSauronSettingsProvider.MinStrengthRatio, EyeOfSauron.EyeOfSauronSettingsProvider.MaxStrengthRatio, "#0.00", Order = 6, RequireRestart = false,
        HintText = "The hunter's host is filled to this many times your strength (your army's, when you lead one). Default 1.3.")]
    public float HunterStrengthRatio { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultStrengthRatio;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyInteger("Hunter Max Troops", EyeOfSauron.EyeOfSauronSettingsProvider.MinMaxTroops, EyeOfSauron.EyeOfSauronSettingsProvider.MaxMaxTroops, Order = 7, RequireRestart = false,
        HintText = "The most men the hunter's party holds after its host is topped up. Default 250.")]
    public int HunterMaxTroops { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultMaxTroops;

    [SettingPropertyGroup("War of the Ring/Eye of Sauron")]
    [SettingPropertyFloatingInteger("Hunter Step Cap", EyeOfSauron.EyeOfSauronSettingsProvider.MinStepCap, EyeOfSauron.EyeOfSauronSettingsProvider.MaxStepCap, "#0.0", Order = 8, RequireRestart = false,
        HintText = "The farthest the hunter closes in one hour while out of your sight, in map units. Default 15.")]
    public float HunterStepCap { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultStepCap;

    // --- War of the Ring / War Chronicle (#765) ---

    [SettingPropertyGroup("War of the Ring/War Chronicle")]
    [SettingPropertyBool("Enable Chronicle", Order = 0, RequireRestart = false,
        HintText = "Runs the War Chronicle: lore events of the War of the Ring at set points, and the great sieges (Helm's Deep, Osgiliath, Minas Tirith and others) as timed windows whose result gives both sides small, temporary effects. Off stops new events and clears the army focus; the saved progress is kept. Default on.")]
    public bool WarChronicleEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/War Chronicle")]
    [SettingPropertyBool("Show Announcements", Order = 1, RequireRestart = false,
        HintText = "Shows a popup on the campaign map when a chronicle event happens, a siege window opens, or a siege is decided. Off keeps the events running silently. Default on.")]
    public bool WarChronicleAnnouncements { get; set; } = true;

    [SettingPropertyGroup("War of the Ring/War Chronicle")]
    [SettingPropertyFloatingInteger("Army Focus Multiplier", TAOM.Features.WarChronicle.WarChronicleSettingsProvider.ArmyFocusMin, TAOM.Features.WarChronicle.WarChronicleSettingsProvider.ArmyFocusMax, "#0.00", Order = 2, RequireRestart = false,
        HintText = "During a siege window, how much more the attackers' armies want to besiege the target and the defender's armies want to defend it once it is attacked. 1 turns the focus off. Needs AI Strategic Intelligence on. Default 1.4.")]
    public float WarChronicleArmyFocus { get; set; } = 1.4f;

    // --- Battle Balance / Troop Power ---

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyBool("Enable Custom Troop Power", Order = 0, RequireRestart = false,
        HintText = "Enables configurable T7-T10 troop power values for battle simulation.")]
    public bool EnableCustomTroopPower { get; set; } = true;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyBool("Override Vanilla Tiers (T1-T6)", Order = 1, RequireRestart = false,
        HintText = "If enabled, battle_balance_config.json TierPower values replace the vanilla formula for T1-T6.")]
    public bool OverrideVanillaTierPower { get; set; } = false;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 7 Base Power", 2.0f, 6.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Base simulation power for T7 troops (vanilla formula extrapolation = 3.06).")]
    public float Tier7Power { get; set; } = 2.91f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 8 Base Power", 2.0f, 7.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "Base simulation power for T8 troops (vanilla formula extrapolation = 3.60).")]
    public float Tier8Power { get; set; } = 3.26f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 9 Base Power", 2.0f, 8.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "Base simulation power for T9 troops (vanilla formula extrapolation = 4.18).")]
    public float Tier9Power { get; set; } = 3.61f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Tier 10 Base Power", 2.0f, 9.0f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Base simulation power for T10 troops (vanilla formula extrapolation = 4.80).")]
    public float Tier10Power { get; set; } = 3.96f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Hero Power Multiplier", 1.0f, 3.0f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "Multiplier applied to heroes in battle simulation. Vanilla = 1.5.")]
    public float HeroMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("Battle Balance/Troop Power")]
    [SettingPropertyFloatingInteger("Mounted Power Multiplier", 1.0f, 2.0f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "Multiplier applied to mounted troops in battle simulation. Vanilla = 1.2.")]
    public float MountedMultiplier { get; set; } = 1.2f;

    // --- Battle Balance / Casualty Ratios ---

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyBool("Enable Custom Casualty Ratios", Order = 0, RequireRestart = false,
        HintText = "Enables configurable wound/kill ratios for battle simulation.")]
    public bool EnableCustomCasualtyRatios { get; set; } = true;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyFloatingInteger("Player Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Blunt (wound-only) damage chance in player battles. Vanilla = 0.30.")]
    public float PlayerBluntDamageChance { get; set; } = 0.30f;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyFloatingInteger("AI Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Blunt damage chance in AI vs AI battles. Vanilla = 0.10.")]
    public float AIBluntDamageChance { get; set; } = 0.10f;

    [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
    [SettingPropertyBool("Enable Cultural Survival Bonuses", Order = 3, RequireRestart = false,
        HintText = "Applies per-culture survival modifiers from battle_balance_config.json. Gondor +30%, Lothlorien +50%, Mordor -20%.")]
    public bool EnableCulturalSurvivalBonuses { get; set; } = true;

    // --- Siege Defense ---

    [SettingPropertyGroup("Siege Defense")]
    [SettingPropertyBool("Enable Siege Defense Events", Order = 0, RequireRestart = false,
        HintText = "When enabled, you receive an event when a watched faction's settlement is besieged, with a timed window to help defend.")]
    public bool EnableSiegeDefenseEvents { get; set; } = true;

    [SettingPropertyGroup("Siege Defense")]
    [SettingPropertyInteger("Response Window (Days)", 1, 14, Order = 1, RequireRestart = false,
        HintText = "Number of in-game days to travel to a besieged settlement before the event expires.")]
    public int SiegeDefenseResponseDays { get; set; } = 3;

    // --- AI Strategic Intelligence ---

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyBool("Enable AI Strategic Intelligence", Order = 0, RequireRestart = false,
        HintText = "When enabled, AI armies stick to their current target rather than re-optimising every 3 hours. Reduces army thrashing and improves siege follow-through.")]
    public bool EnableArmyStrategicIntelligence { get; set; } = true;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Commitment Multiplier", 1.0f, 10.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "How strongly an army commits to its current target. 4.0 = the alternative must score 4x better before the army will divert. Vanilla implicit = 1.3.")]
    public float ArmyCommitmentMultiplier { get; set; } = 4.0f;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Priority List Boost", 1.0f, 5.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "Score multiplier applied to the first settlement in a faction's priority list. Decays linearly to 1.0 at the last entry. Affects Mordor, Isengard etc.")]
    public float ArmyPriorityBoost { get; set; } = 3.0f;

    [SettingPropertyFloatingInteger("Evil Faction Aggression Scale", 0.5f, 3.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "Global multiplier applied to all per-faction strength inflation values from army_targeting.json. 1.0 = use JSON defaults. Raise to make evil factions siege even when outnumbered.")]
    [SettingPropertyGroup("AI Strategic Intelligence")]
    public float EvilFactionAggressionScale { get; set; } = 1.0f;

    [SettingPropertyFloatingInteger("Border Proximity Floor", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range, and only for targets inside the march radius below. 0 = vanilla.")]
    [SettingPropertyGroup("AI Strategic Intelligence")]
    public float ArmyBorderProximityFloor { get; set; } = 0.15f;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyBool("Enable War Theaters", Order = 5, RequireRestart = false,
        HintText = "When enabled, kingdoms favour enemies on their own front. Gondor prefers Mordor and Isengard over far northern targets. Weighting only: no war is ever forbidden.")]
    public bool EnableWarTheaters { get; set; } = true;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Border Rescue Radius", 1.0f, 20.0f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "How far, in average town gaps, a priority-list target may be and still have its unreachable verdict overturned by the Border Proximity Floor above. Not a general distance penalty: the game already applies one of its own. 3.2 clears the widest real front on the map.")]
    public float ArmyBorderRescueRadius { get; set; } = 3.2f;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("Home Defence Priority", 1.0f, 5.0f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "Score multiplier on defending one of your own settlements. Raise it if AI kingdoms keep besieging abroad while their own fiefs fall. 1.0 = vanilla.")]
    public float ArmyDefenderPriority { get; set; } = 1.6f;

    [SettingPropertyGroup("AI Strategic Intelligence")]
    [SettingPropertyBool("Good armies hold captured forts", Order = 8, RequireRestart = false,
        HintText = "When an AI army of a Free realm takes a town or castle by siege, it stays inside for about 22 hours (12 to 30: longer for a Calculating lord, shorter for a Valorous one) instead of the 10 the game gives. It leaves early if the fortress is besieged, its cohesion drops below 40 or it has less than a day of food, but holds are not saved: after loading a save a running hold cannot end early and runs to its end. Evil realms and (by default) Neutral ones keep rolling on. Single-player only. Needs AI Strategic Intelligence on. Default: on.")]
    public bool HoldCapturedForts { get; set; } = true;

    // --- Time Acceleration ---

    // The keys themselves are NOT configured here. They are native rebindable game keys, so they
    // live in Options > Keybindings > Campaign Map (see TaomTimeControlHotKeyCategory). MCM v5 has
    // no keybind widget at all, which is why these three settings tune only the multipliers.

    [SettingPropertyGroup("Time Acceleration", GroupOrder = 10)]
    [SettingPropertyInteger("Fast Forward Multiplier", 1, 128, Order = 0, RequireRestart = false,
        HintText = "Speed multiplier for fast-forward: the map bar fast-forward button, and the fast-forward key (Space by default; rebind under Options > Keybindings > Campaign Map). Default: 4.")]
    public int FastForwardMultiplier { get; set; } = 4;

    [SettingPropertyGroup("Time Acceleration")]
    [SettingPropertyInteger("Extra Fast Forward Multiplier", 1, 128, Order = 1, RequireRestart = false,
        HintText = "Speed multiplier for extra fast-forward: the map bar Extra Fast Forward button (the fourth time button), and the extra fast-forward key (E by default, which is also Rotate Camera Right; rebind either under Options > Keybindings > Campaign Map). Never below the Fast Forward Multiplier. Default: 8.")]
    public int ExtraFastForwardMultiplier { get; set; } = 8;

    [SettingPropertyGroup("Time Acceleration")]
    [SettingPropertyInteger("Turbo Multiplier (hold Ctrl)", 1, 128, Order = 2, RequireRestart = false,
        HintText = "Speed multiplier while holding Ctrl and the turbo key (Space by default; rebind under Options > Keybindings > Campaign Map). Releases back to prior speed on key-up. Default: 16.")]
    public int CtrlSpaceMultiplier { get; set; } = 16;

    // --- Graphics / Shader Precompilation ---
    //
    // Re-enabled 2026-09-11 (#560) after the 2026-08-20 park. The scene-pass toggle was RENAMED rather
    // than defaulted off: TaomSettings persists as json2 and MCM loads that file over the compiled
    // default, so every install that launched TAOM while the old attribute was live (2026-06-25 to
    // 2026-08-20) had EnableScenePassPrecompilation = true on disk, and a changed default would never
    // have reached it. MCM's json2 reader fills only the properties the class currently declares, so the
    // orphaned key is ignored on load and dropped by the next save. Rule for the next time: rename,
    // never flip, a persisted MCM default.
    //
    // PARKED again 2026-09-25 with the main-menu option (SubModule.cs): both attribute stacks are
    // commented out so the group does not show. The PROPERTIES stay: ShaderPrecompileRunner.Begin()
    // reads the scene-pass one, and SettingsFingerprintTests pins the reflected-property count.
    // While parked, MCM neither loads nor saves them (its json2 converter handles only properties
    // carrying a SettingProperty value attribute). The first Mod Options save that changes any TAOM
    // setting drops their stored values, and un-parking then starts both at compiled defaults; an
    // install that saves nothing meanwhile gets its stored values back.
    // Un-parking = uncomment the attributes.

    // [SettingPropertyGroup("Graphics/Shader Precompilation", GroupOrder = 15)]
    // [SettingPropertyBool("Enable Shader Precompilation", Order = 0, RequireRestart = false,
    //     HintText = "Master toggle for the main-menu 'Pre-compile Shaders' option. When off, the option is hidden so no NEW walk can be started (a walk already in progress finishes; it is not aborted mid-flight). Takes effect immediately, no relaunch. Default: on.")]
    public bool EnableShaderPrecompilation { get; set; } = true;

    // [SettingPropertyGroup("Graphics/Shader Precompilation")]
    // [SettingPropertyBool("Include Scene Passes (can crash some GPUs)", Order = 1, RequireRestart = false,
    //     HintText = "Off by default. When on, the walk also loads each TAOM battle, siege and village scene to pre-compile its terrain and atmosphere shaders. These scene loads hard-crash some GPUs (pbr_terrain, #287) and are not needed for the troop and equipment pass. A scene that crashes is auto-skipped on the next run. Default: off.")]
    public bool EnableShaderPrecompileScenePasses { get; set; } = false;

    // --- Battle Tactics / Siege Dismount ---

    [SettingPropertyGroup("Battle Tactics/Siege Dismount", GroupOrder = 20)]
    [SettingPropertyBool("Enable Siege Dismount", Order = 0, RequireRestart = false,
        HintText = "Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
    public bool EnableSiegeDismount { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
    [SettingPropertyInteger("Siege Mount Behavior (0=Vanilla, 1=Reserved, 2=ToInventory, 3=AutoRemount)", 0, 3, Order = 1, RequireRestart = false,
        HintText = "0 = Vanilla (no change). 1 = RESERVED (currently equivalent to Vanilla — full implementation deferred; would spawn the horse on the map separately). 2 = Mount moves to inventory for siege duration; player must re-equip manually after. 3 = Mount moves to inventory and is auto-restored after siege ends. Default: 3.")]
    public int SiegeMountBehavior { get; set; } = 3;

    [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
    [SettingPropertyBool("Siege Dismount Debug Mode", Order = 2, RequireRestart = false,
        HintText = "Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
    public bool SiegeDismountDebug { get; set; } = false;

    // --- Battle Tactics / Siege Forces ---
    // The wall-battle troop picker (#734). The toggle gates only the OFFER of the screen, read at each assault, so
    // RequireRestart = false: nothing is applied once at start. A defender who presses Escape mid-battle gets
    // ExitResult.SurrenderSiege (BattleEndLogic.TryExit, v1.5.3 :255-258), and CampaignSiegeStateHandler.OnSurrenderMission
    // sets PlayerSurrender, which stops the siege component continuing the battle: the surrender takes the whole side.
    // GroupOrder 25 is free among the Battle Tactics subgroups (19-24, 27-29, 52 and 56 are taken); it is the group's
    // first property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("Battle Tactics/Siege Forces", GroupOrder = 25)]
    [SettingPropertyBool("Choose Troops Before Wall Battles", Order = 0, RequireRestart = false,
        HintText = "Before an assault, or before defending a siege, open the 'Manage Troops' screen the lord's hall fight uses and choose who fights. By default, trolls start unticked; tick them to bring them. Troops you leave out take no part in the battle and suffer no losses in it. Takes effect from the next assault. As defender, pressing Escape in the battle surrenders the siege and everyone with it, reserves included. When off, every troop fights as in vanilla. Default: on.")]
    public bool EnableSiegeTroopPicker { get; set; } = true;

    // --- Battle Tactics / Creature Siege Role ---
    // Trolls in a wall battle (#735). Read ONCE per battle at the mission's AfterStart, so a change applies from the next
    // battle and RequireRestart = false: nothing is applied at start-up and nothing is half-applied mid-battle. The
    // face exclusion it adds to a creature is never removed, which is why a mid-battle toggle is not live.
    // GroupOrder 26 is free among the Battle Tactics subgroups (19-25, 27-29, 52 and 56 are taken); it is the group's first
    // property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("Battle Tactics/Creature Siege Role", GroupOrder = 26)]
    [SettingPropertyBool("Trolls Stay Off Siege Ladders, Towers and Walls", Order = 0, RequireRestart = false,
        HintText = "In a wall battle, trolls never use siege ladders, towers or siege engines. Attacking trolls break the gate down instead, and defending trolls hold the ground behind it and stay off the walls while the AI commands them; a formation you give orders to obeys them. Takes effect from the next battle. When off, trolls behave as in vanilla. Default: on.")]
    public bool EnableCreatureSiegeRole { get; set; } = true;

    // --- Battle Tactics / Staged Siege (docs/features/staged-siege.md, #1054) ---
    // Read ONCE per battle when the mission behavior decides whether to arm, so a change applies from the next battle and
    // RequireRestart = false. Ships OFF: turning the default on later needs a renamed property, because MCM keeps a saved
    // value over a changed default (the ShaderPrecompilation trap; check_mcm_default_flips.py). GroupOrder 81 is free
    // (80 is Warden's Tally; it is the group's first property, so it carries the order).

    [SettingPropertyGroup("Battle Tactics/Staged Siege", GroupOrder = 81)]
    [SettingPropertyBool("{=taom_staged_siege_mcm_enable}Staged Sieges (Capture Points)", Order = 0, RequireRestart = false,
        HintText = "{=taom_staged_siege_mcm_enable_hint}In a castle assault on a scene with recorded capture points, the attackers must take points A to D in order: outside the walls, the gatehouse, the courtyard and an inner point. Each captured point brings fresh troops from both armies. As the attacker, taking D sends the defenders back to the lord's hall, and taking the hall's last room (point E) wins the castle; as the defender, losing D loses the castle. Scenes without recorded points stay vanilla, and in a staged siege the Reinforcement Wave Count option does not apply. Takes effect from the next battle. Default: off.")]
    public bool EnableStagedSiege { get; set; } = false;

    // --- Battle Tactics / Siege Prop Diagnostics ---
    // Diagnostic only, no gameplay effect. The engine reports NOTHING when a rock pile or ammo
    // barrel is unusable, so this turns that silence into one log line per prop.

    [SettingPropertyGroup("Battle Tactics/Siege Prop Diagnostics", GroupOrder = 23)]
    [SettingPropertyBool("Enable Siege Prop Diagnostics", Order = 0, RequireRestart = false,
        HintText = "Log why rock piles and arrow barrels can or cannot be used in the current mission. Diagnostic only — changes no gameplay. Off by default.")]
    public bool EnableSiegePropDiagnostics { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Siege Prop Diagnostics")]
    [SettingPropertyBool("Siege Prop Diagnostics Verbose", Order = 1, RequireRestart = false,
        HintText = "Log every prop, including working ones. Off = log only props that are unusable, plus the summary.")]
    public bool SiegePropDiagnosticsVerbose { get; set; } = false;

    // --- Battle Tactics / Crew Platform Diagnostics ---
    // Instrumentation only (#627): gates the crew-platform decision, layout, status and summary lines on BOTH the
    // war elephant's howdah and the mumakil's war tower, never behaviour. On by default while the platforms are
    // tested (Mike, 2026-09-19); HowdahDiagnosticsSettingsProvider falls back to the same value and a test pins both.
    // The PROPERTY NAME stays EnableHowdahDiagnostics although the label widened: MCM's json2 persists per property,
    // so renaming it would hand every existing install a fresh default (CLAUDE.md, the ShaderPrecompilation trap).
    // RequireRestart = false: the platforms read it every sample.
    [SettingPropertyGroup("Battle Tactics/Crew Platform Diagnostics", GroupOrder = 19)]
    [SettingPropertyBool("Log Crew Platform Diagnostics", Order = 0, RequireRestart = false,
        HintText = "Write the war elephant howdah's and the mumakil war tower's diagnostics to the TAOM log: why a rider did or did not get a howdah, each platform's layout and floor clearance over its beast, a status line every 5 seconds per platform (beast speed against its walking speed, platform drift, how many archers are seated and how many can reach a target) and a battle summary. Diagnostic only, changes no gameplay. On by default while the platforms are being tested.")]
    public bool EnableHowdahDiagnostics { get; set; } = true;

    // --- Enlistment ---
    // Master switch for the whole serve-as-a-soldier feature. OFF removes the enlist dialog line,
    // so no new service can start. Turning it off MID-SERVICE does not simply freeze the feature:
    // the enlisted player is parked hidden and inactive next to their commander, and the code that
    // un-hides them is the same code this switch disables — so a plain "stop running" would strand
    // them invisible on the map with no menu and no way out. The reconciler therefore performs one
    // honourable discharge and hands the player back properly: on the next hourly tick, or, when the
    // player is in a battle, once that battle has ended (#1021).

    [SettingPropertyGroup("Enlistment", GroupOrder = 41)]
    [SettingPropertyBool("Enable Enlistment", Order = 0, RequireRestart = false,
        HintText = "Serve as a soldier in a lord's party — enlist, take a rank, draw wages, follow the column into battle. Turn OFF to remove the feature: the option to enlist disappears, and if you are already serving you are released honourably (you keep your pay and your gear) rather than being left parked on the map. Takes effect within three in-game hours (in a battle, once it ends); no restart. Default ON.")]
    public bool EnableEnlistment { get; set; } = true;

    // --- Enlistment / Diagnostics ---
    // Diagnostic only, no gameplay effect. OFF by default (#840): with it on, [EnlistDiag] was 4,856 of
    // 6,001 lines (81%) of a 32-minute log — 3,674 of them from one per-map-event line — so switch it on
    // only to record a trace for a bug report.
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
    // Measured on the session that closed #375: 950 of 3,452 log lines were enlistment, and FIVE
    // gated message shapes accounted for 851 of them — TICK (202), SYNC ok (199), PARK ok (23),
    // RESTORE ok (12) and the per-map-event line (450). The 52 ungated INFO sites produced the other
    // ~99, and those are the ones worth keeping: oath, joins, duties, promotion, rewards, discharge.
    //
    // FLIPPED BACK ON 2026-08-28, TEMPORARILY, for the #520 in-game smoke, and then never flipped back:
    // it shipped ON in every release from v2.0.24 (2026-09-01) through v2.0.35 (2026-10-19). Back OFF in #840, under a NEW property name (EnableEnlistmentTrace,
    // renamed from EnableEnlistmentDiagnostics).
    //
    // WHY IT WAS RENAMED, NOT FLIPPED. A COMPILED DEFAULT DOES NOT REACH AN EXISTING INSTALL. MCM writes
    // Configs/ModSettings/Global/TAOM/TAOM.json, every property in it, only when a player confirms a change on
    // TAOM's settings page, and loads that file over the defaults from then on. So every player who changed any
    // TAOM setting while the default was on (v2.0.24 to v2.0.35) has `true` saved under the old name, and a flipped default
    // would never have reached them (a file written before 2026-08-28 kept false). MCM keeps a saved value per
    // property name, so a new name is what gives them the new default (the orphaned old key is ignored on load
    // and dropped by the next save). The revert touches four literals: this default, the provider's `??`, and
    // the two tests that name the posture (EnlistmentDiagnosticsSettingsProviderTests). Once players save the new key,
    // a later change of this default needs another rename too (trap index: Persisted MCM defaults).

    [SettingPropertyGroup("Enlistment", GroupOrder = 41)]
    [SettingPropertyBool("Offer Leave When The Column Halts", Order = 10, RequireRestart = false,
        HintText = "When your company stops at a town, castle or village, ask whether you want a few hours to yourself. The popup exists because the wait menu runs at full speed: a lord's stop is a few campaign hours, which is about two real seconds, and the 'Take leave in the settlement' menu option appears and vanishes inside it. Turn this off and that menu option still works. Takes effect immediately; no restart. Default ON.")]
    public bool OfferLeaveOnArrival { get; set; } = true;

    [SettingPropertyGroup("Enlistment/Diagnostics", GroupOrder = 42)]
    [SettingPropertyBool("Enable Enlistment Trace", Order = 0, RequireRestart = false,
        HintText = "Off by default: turn it on to record the routine enlistment trace ([EnlistDiag] TICK, SYNC ok, PARK ok, RESTORE ok) in the TAOM debug log when you are reproducing an enlistment problem for a bug report. It is roughly ten times the volume of the ordinary enlistment log, so turn it off again afterwards. Real faults are always logged regardless of this setting, so [EnlistDiag] lines still appear for them when it is off. Takes effect immediately; no restart.")]
    public bool EnableEnlistmentTrace { get; set; } = false;

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

    // Renamed from FieldCommissionMaxOffersPerBattle (#1017): its default went 1 to 2 in v2.0.21 (14160f0a4) under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 1.
    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Offers Per Battle", 1, 20, Order = 1, RequireRestart = false,
        HintText = "The most promotion prompts a single won battle may raise. Each prompt pauses the game, so a large number after a large battle means a long queue of dialogs. Merit above the cap is not lost — it carries to the next battle. Default: 2. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int FieldCommissionOffersPerBattle { get; set; } = 2;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyFloatingInteger("Fair-Fight Ratio", 0.1f, 3.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "How outnumbered you must be for a battle to count. Your party's healthy troops divided by the whole enemy side must come in UNDER this. 1.00 = only fights where you are outnumbered; higher = easier fights still earn merit. Default: 1.30.")]
    public float FieldCommissionRatioThreshold { get; set; } = 1.3f;

    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Merit Per Kill", 1, 10, Order = 3, RequireRestart = false,
        HintText = "Merit a troop type banks for each enemy its soldiers kill in a qualifying battle. Higher = faster promotions. Default: 1.")]
    public int FieldCommissionMeritPerKill { get; set; } = 1;

    // Renamed from FieldCommissionMeritThreshold (#1017): its default went 8 to 32 in v2.0.21 (14160f0a4) under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 8.
    [SettingPropertyGroup("Battlefield Promotions")]
    [SettingPropertyInteger("Merit To Promote", 1, 100, Order = 4, RequireRestart = false,
        HintText = "Merit a troop type must bank before it earns a promotion offer. Note merit is pooled per troop TYPE, not per soldier — a large stack of one type reaches the bar faster than a small one. Higher = rarer promotions. Default: 32. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int FieldCommissionMeritToPromote { get; set; } = 32;

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
    [SettingPropertyBool("Enable Messengers", Order = 0, RequireRestart = false,
        HintText = "Send paid messengers to heroes. The price grows with distance. They travel for several days and trigger a conversation on arrival. Disable to grey out the encyclopedia button and stop the dialog line.")]
    public bool EnableMessengers { get; set; } = true;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyInteger("Base Gold Cost", 10, 500, Order = 1, RequireRestart = false,
        HintText = "Denar price of the shortest trip. The price grows with the distance to the target, and is higher again when the target is at war with you, when you have never met them, and when they sit in a besieged town or castle. A stranger costs more but is never refused. The per-day rate and the multipliers live in messenger_config.json.")]
    public int MessengerGoldCost { get; set; } = 50;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyInteger("Travel Days", 1, 10, Order = 2, RequireRestart = false,
        HintText = "In-game days a messenger spends in transit before arriving at the target. Speed scales to map size. The days also scale the price.")]
    public int MessengerTravelDays { get; set; } = 3;

    [SettingPropertyGroup("Messengers")]
    [SettingPropertyBool("Enable Accidents", Order = 3, RequireRestart = false,
        HintText = "Random ambush chance during travel. The base hourly probability lives in messenger_config.json (default 0.2%).")]
    public bool MessengerAccidents { get; set; } = true;

    // --- Battle Tactics / Mixed Formations ---

    [SettingPropertyGroup("Battle Tactics/Mixed Formations", GroupOrder = 21)]
    [SettingPropertyBool("Enable Mixed Formations", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, formations use vanilla positioning. When on, formations with mixed melee + ranged units are reordered per the chosen layout while holding position.")]
    public bool EnableMixedFormations { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyInteger("Default Layout (0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard)", 0, 3, Order = 1, RequireRestart = false,
        HintText = "Default layout auto-applied to mixed-class formations (>=5 minority units AND >=20% minority share AND >=10 total units). 0=Infantry front + Ranged back. 1=Ranged front + Infantry back. 2=Ranged on the wings, Infantry in the center. 3=Checkerboard. Default: 0.")]
    public int MixedFormationsDefaultLayout { get; set; } = 0;

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyText("Cycle Layout Hotkey", Order = 2, RequireRestart = false,
        HintText = "Bannerlord InputKey name. Pressing this while a formation is selected cycles its layout to the next; pressing while no formation is selected cycles all formations. Default: L.")]
    public string MixedFormationsCycleHotkey { get; set; } = "L";

    [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
    [SettingPropertyBool("Mixed Formations Debug Mode", Order = 3, RequireRestart = false,
        HintText = "Show diagnostic [MixedFormations] messages on the in-game HUD. Off = file log only.")]
    public bool MixedFormationsDebug { get; set; } = false;

    // --- Fief Management ---

    [SettingPropertyGroup("Fief Management", GroupOrder = 26)]
    [SettingPropertyBool("Enable Fief Management", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, the F6 hotkey and the Fiefs button on the map bar are inert and the carousel options are disabled. Effective immediately at runtime. Default: true.")]
    public bool EnableFiefManagement { get; set; } = true;

    [SettingPropertyGroup("Fief Management")]
    [SettingPropertyBool("Allow Remote Building Queue", Order = 1, RequireRestart = false,
        HintText = "When on, you can manage any owned fief from anywhere via F6 or the Fiefs button on the map bar. When off, the Manage option is disabled unless you are physically at the selected fief. Default: true.")]
    public bool AllowRemoteBuildingQueue { get; set; } = true;

    [SettingPropertyGroup("Fief Management")]
    [SettingPropertyBool("Fief Management Debug Mode", Order = 2, RequireRestart = false,
        HintText = "Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent.")]
    public bool FiefManagementDebug { get; set; } = false;

    // The group's first property carries GroupOrder; 0 sorts it above the parent's own settings (MCM ignores a GroupOrder on a later one).
    [SettingPropertyGroup("Fief Management/Postern Gate", GroupOrder = 0)]
    [SettingPropertyBool("Postern Gate Cuts Siege Losses", Order = 0, RequireRestart = false,
        HintText = "A Postern Gate (a Hidden Door, Culvert, Sally Port, Hidden Way or Bolt-Hole, by culture) in a besieged fief cuts the troops lost breaking into or out of the siege there: by default 50, 67 and 75 percent by building level (postern_gate.json). When off, the building still exists but the cut does not apply. Takes effect at the next break-in or break-out. Default: on.")]
    public bool EnablePosternGate { get; set; } = true;

    // GroupOrder 1 sorts it just after the Postern Gate group; the group's first property carries it.
    [SettingPropertyGroup("Fief Management/From the Ashes", GroupOrder = 1)]
    [SettingPropertyBool("From the Ashes", Order = 0, RequireRestart = false,
        HintText = "Lets your company rebuild a looted village of your own realm from the village menu, and lend its labour to a construction project in a fief you own from the keep menu (from_the_ashes.json holds the hours, labour and materials). When off, those two options are hidden; a job already under way still finishes or stops normally. Single-player only. Default: on.")]
    public bool EnableFromTheAshes { get; set; } = true;

    // GroupOrder 2 sorts it after the Postern Gate (0) and From the Ashes (1) groups; the group's first property carries it.
    [SettingPropertyGroup("Fief Management/Reeve's Ledger", GroupOrder = 2)]
    [SettingPropertyBool("Reeve's Ledger", Order = 0, RequireRestart = false,
        HintText = "On the town management window: the fief's name as the title, a Profit row with a daily breakdown, a Patrol row for towns, and tooltips on the stash and prison icons. When off, the title, rows and icons are vanilla; the step buttons have their own switch. Applies the next time the window opens. Default: on.")]
    public bool EnableReevesLedger { get; set; } = true;

    [SettingPropertyGroup("Fief Management/Reeve's Ledger")]
    [SettingPropertyBool("Reserve Step Buttons", Order = 1, RequireRestart = false,
        HintText = "Adds a button beside the reserve's plus button to put gold into the reserve in one click, and one to take a step back out, refunded in full with no penalty. Single-player only. Applies the next time the window opens. Default: on.")]
    public bool EnableReserveSteps { get; set; } = true;

    // GroupOrder 3 sorts it after the Postern Gate (0), From the Ashes (1) and Reeve's Ledger (2) groups; the group's first property carries it.
    [SettingPropertyGroup("Fief Management/Namegiving", GroupOrder = 3)]
    [SettingPropertyBool("Namegiving", Order = 0, RequireRestart = false,
        HintText = "Lets the ruler of your realm rename its towns, castles and villages from the Fiefs tab of the kingdom screen for influence, and lets four fiefs take an older or darker name while the matching side holds them. A name you chose is never overwritten. Single-player only. Switching it off gives those fiefs their map names back; a name you chose stays. The button follows the next time the kingdom screen opens; the older names change at the next capture, load or change of realm. Default: on.")]
    public bool EnableNamegiving { get; set; } = true;

    // GroupOrder 5 sorts it after the Postern Gate (0), From the Ashes (1), Reeve's Ledger (2), Namegiving (3) and Keep Wardens (4) groups; the group's first property carries it.
    [SettingPropertyGroup("Fief Management/Watch-Silver", GroupOrder = 5)]
    [SettingPropertyBool("Watch-Silver", Order = 0, RequireRestart = false,
        HintText = "Lets you talk to the governor of a town or castle your clan owns, inside that fief, and pay silver to the watch to raise its loyalty. Each offer shows its price, and there is a ceiling on how far silver can raise loyalty. When off, the line is hidden. Single-player only. Default: on.")]
    public bool EnableWatchSilver { get; set; } = true;

    // --- Character Screen / Loremaster's Line (#877) ---

    // GroupOrder 44 orders the "Loremaster's Line" subgroup inside "Character Screen"; the group's first property carries it.
    [SettingPropertyGroup("Character Screen/Loremaster's Line", GroupOrder = 44)]
    [SettingPropertyBool("{=taom_ll_mcm_enable}Loremaster's Line", Order = 0, RequireRestart = false,
        HintText = "{=taom_ll_mcm_enable_hint}On the character screen, marks the level past which the selected skill learns at its slowest: a red arrow and a red band to the end of the bar; hover the band for the details. Also adds a line to the learning limit tooltip. Applies the next time the character screen opens. Default: on.")]
    public bool EnableLoremastersLine { get; set; } = true;

    // GroupOrder 4 sorts it after the Namegiving group (3); the group's first property carries it.
    [SettingPropertyGroup("Fief Management/Keep Wardens", GroupOrder = 4)]
    [SettingPropertyBool("{=taom_keep_wardens_label}Castle Patrols", Order = 0, RequireRestart = false,
        HintText = "{=taom_keep_wardens_hint}A castle with a built Guard House fields one patrol party of its owner's culture at a time, as a town does: stronger at each Guard House level. A lost patrol is replaced after 10, 8 or 6 days. While a castle's patrol is alive, the castle also gains security each day: 1, 1.5 or 2 by Guard House level (both in castle_patrols.json). When off, no new castle patrol is queued, and the game's daily check removes the castle patrols already out. Default: on.")]
    public bool EnableCastlePatrols { get; set; } = true;

    [SettingPropertyGroup("Fief Management/Keep Wardens")]
    [SettingPropertyBool("{=taom_keep_wardens_build_label}Guard House Build Priority", Order = 1, RequireRestart = false,
        HintText = "{=taom_keep_wardens_build_hint}The AI builds a Guard House in its towns and castles ahead of other buildings: always while it has none, and about 60 percent of the time while it can still be upgraded (guardHousePriorityChance in castle_patrols.json). Your own fiefs are never touched. Default: on.")]
    public bool EnableGuardHouseBuildPriority { get; set; } = true;

    // --- Inventory / Quick Actions ---

    [SettingPropertyGroup("Inventory/Quick Actions", GroupOrder = 30)]
    [SettingPropertyBool("Enable Quick Actions", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, inventory 'Sell All' uses vanilla. When on, it opens a 4-option menu.")]
    public bool EnableQuickActions { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions")]
    [SettingPropertyBool("Enable Inventory Search", Order = 1, RequireRestart = false,
        HintText = "Inventory search box visibility; persists per save and reconciles to MCM each campaign frame.")]
    public bool EnableInventorySearch { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions")]
    [SettingPropertyBool("{=taom_tm_mcm_enable}Highlight better items", Order = 2, RequireRestart = false,
        HintText = "{=taom_tm_mcm_enable_hint}Tint inventory items that beat the selected character's gear: green for an upgrade, yellow for a sidegrade (more total armour, less in its own slot).")]
    public bool EnableTelcharsMark { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions")]
    [SettingPropertyBool("{=taom_tm_mcm_filter_default}Show only better items by default", Order = 3, RequireRestart = false,
        HintText = "{=taom_tm_mcm_filter_default_hint}Tick \"Show only better items\" each time the inventory opens.")]
    public bool TelcharsMarkShowOnlyUpgradesByDefault { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged", GroupOrder = 30)]
    [SettingPropertyDropdown("Damage Threshold Preset", Order = 0, RequireRestart = false,
        HintText = "Items at or below this damage level are sold. Pristine = unused (sentinel). Default: Moderate (-20%).")]
    public Dropdown<string> DamagedQualityDropdown { get; set; } = new Dropdown<string>(
        new[] { "Pristine", "Slight (-10%)", "Moderate (-20%)", "Heavy (-40%)" }, 2);

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyFloatingInteger("Custom Damage Threshold", -1.0f, 0.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Custom threshold. Only used when 'Use Custom Threshold' is on. Default: -0.20.")]
    public float DamagedThreshold { get; set; } = -0.2f;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyBool("Use Custom Threshold", Order = 2, RequireRestart = false, HintText = "Toggle dropdown vs custom value above.")]
    public bool UseCustomThreshold { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
    [SettingPropertyBool("Exclude Damaged Horses", Order = 4, RequireRestart = false, HintText = "Skip horses/mounts when selling damaged. Default: true.")]
    public bool ExcludeDamagedHorses { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value", GroupOrder = 31)]
    [SettingPropertyInteger("Low Value Threshold (denars)", 1, 10000, Order = 0, RequireRestart = false,
        HintText = "Items at or below this denars value are sold. Default: 100.")]
    public int LowValueThreshold { get; set; } = 100;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Food", Order = 2, RequireRestart = false, HintText = "Skip food items. Default: true.")]
    public bool ExcludeLowValueFood { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Horses", Order = 3, RequireRestart = false, HintText = "Skip horses/mounts. Default: true.")]
    public bool ExcludeLowValueHorses { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
    [SettingPropertyBool("Exclude Low Value Trade Goods", Order = 4, RequireRestart = false, HintText = "Skip trade goods. Default: false.")]
    public bool ExcludeLowValueTradeGoods { get; set; } = false;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc", GroupOrder = 32)]
    [SettingPropertyBool("Show Confirmation Dialog", Order = 0, RequireRestart = false, HintText = "Ask for confirmation before bulk-selling. Default: true.")]
    public bool QuickActionsShowConfirmation { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
    [SettingPropertyBool("Play Sounds", Order = 1, RequireRestart = false, HintText = "Play 'event:/ui/transfer' chime after each batch action. Default: true.")]
    public bool QuickActionsPlaySounds { get; set; } = true;

    [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
    [SettingPropertyBool("Quick Actions Debug Mode", Order = 2, RequireRestart = false, HintText = "Show diagnostic [QuickActions] HUD messages. Off = file log only.")]
    public bool QuickActionsDebug { get; set; } = false;

    // --- Inventory / Equipment Presets ---

    [SettingPropertyGroup("Inventory/Equipment Presets", GroupOrder = 33)]
    [SettingPropertyBool("Enable Equipment Presets", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
    public bool EnableEquipmentPresets { get; set; } = true;

    [SettingPropertyGroup("Inventory/Equipment Presets")]
    [SettingPropertyInteger("Max Presets Per Character", 1, 20, Order = 1, RequireRestart = false,
        HintText = "Maximum saved presets per hero. Default: 10.")]
    public int MaxPresetsPerCharacter { get; set; } = 10;

    [SettingPropertyGroup("Inventory/Equipment Presets")]
    [SettingPropertyBool("Equipment Presets Debug Mode", Order = 2, RequireRestart = false,
        HintText = "Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
    public bool EquipPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Smart Cavalry ---

    // Renamed from EnableSmartCavalryAI (#1017): it was switched off in v2.0.7 (625396dde) while war-elephant
    // interaction is tuned, but a saved "on" survived under the same name.
    [SettingPropertyGroup("Battle Tactics/Smart Cavalry", GroupOrder = 22)]
    [SettingPropertyBool("Enable Smart Cavalry AI", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior. Default OFF while war-elephant interaction is being tuned. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public bool EnableSmartCavalryCharge { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyBool("Enable Friendly Collision Avoidance", Order = 1, RequireRestart = false,
        HintText = "When charging, cavalry will reroute around friendly infantry on the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
    public bool SmartCavalryAvoidFriendlies { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Charge Formation Strictness", 0.0f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "How close riders must be to their slots before the line charges, and before a reform counts as done: the average rider may be 10 m off at 0, 4.4 m at the 0.7 default, 2 m at 1. Max Line-Up Seconds launches the charge regardless when it runs out.")]
    public float SmartCavalryChargeStrictness { get; set; } = 0.7f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Reform Distance After Charge", 10f, 80f, "#0", Order = 3, RequireRestart = false,
        HintText = "Meters past the target before cavalry reforms a new line. Larger = wider passthrough sweep. Default 25.")]
    public float SmartCavalryReformDistance { get; set; } = 25f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Charge Line Spacing Multiplier", 0.8f, 3.0f, "#0.0", Order = 4, RequireRestart = false,
        HintText = "Multiplier on default unit spacing during line formation. 1.0 = vanilla. 1.2 (default) = slightly wider line for cleaner charge.")]
    public float SmartCavalryLineSpacing { get; set; } = 1.2f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyFloatingInteger("Max Line-Up Seconds", 1f, 15f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "Longest the cavalry spend lining up before they charge anyway, and before they re-charge after a reform. Lower is snappier with a looser line. Default 4.")]
    public float SmartCavalryMaxLineUpSeconds { get; set; } = 4f;

    [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
    [SettingPropertyBool("Smart Cavalry Debug Mode", Order = 6, RequireRestart = false,
        HintText = "Also write every intercepted charge order and the state it produced to the TAOM log file. State transitions are logged regardless. Nothing is drawn on the HUD.")]
    public bool SmartCavalryDebug { get; set; } = false;

    // --- Battle Tactics / Culture Doctrine ---
    // GroupOrder 52: 10, 15, 20-51 and 100 were taken as of 2026-09-16.

    [SettingPropertyGroup("Battle Tactics/Culture Doctrine", GroupOrder = 52)]
    [SettingPropertyBool("Enable Culture Doctrine", Order = 0, RequireRestart = false,
        HintText = "Each culture's AI fights field battles with its own doctrine: Dwarves hold a shield wall on the high ground, Elves ring their archers with infantry, Rohan leads with cavalry, Mordor and Gundabad mass their infantry. Replaces the tactic list the engine's team AI chooses from; the AI still picks by weight every 5 seconds. Applies from the NEXT battle (the list cannot change once a battle has started). Your own orders are never touched; when you delegate command (F6) your army fights with its culture's doctrine. Off by default until the in-game A/B passes; tuning lives in ModuleData/culture_doctrine/culture_doctrines.json, which reloads on game restart only.")]
    public bool EnableCultureDoctrine { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Culture Doctrine")]
    [SettingPropertyBool("Culture Doctrine Debug Mode", Order = 1, RequireRestart = false,
        HintText = "Write a [Doctrine] status line to the TAOM log every 5 seconds of battle time: each team's current tactic and every formation's active behaviour and arrangement. The console command taom.tactic_status prints the same line on demand. Nothing is drawn on the HUD.")]
    public bool CultureDoctrineDebug { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Culture Doctrine")]
    [SettingPropertyBool("Culture Morale", Order = 2, RequireRestart = false,
        HintText = "Each culture's soldiers hold under fear their own way: Dwarves, Elves, Isengard, Gundabad and Dol Guldur never rout (pride, or a hatred too great), the free peoples of Rohan, Gondor and Dale and the men of the East and South break as vanilla does, and every culture carries a bravery offset on its starting morale. Needs Enable Culture Doctrine. Applies from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineMorale { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Culture Doctrine")]
    [SettingPropertyBool("Culture Aggression", Order = 3, RequireRestart = false,
        HintText = "Each culture's soldiers fight the melee their own way: orcs attack on sight and rarely cover with a shield, Dwarves and Gondor raise shields and hold, Elves shoot straighter, Rohan commits to a charge from further out. Multiplies the engine's per-soldier AI decision values after skill has set them. Needs Enable Culture Doctrine. Applies to soldiers spawned from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineAggression { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Culture Doctrine")]
    [SettingPropertyBool("Horse Stand", Order = 4, RequireRestart = false,
        HintText = "Dwarves, Isengard and Dale, defending outnumbered against an army of mostly horse, stand where horse cannot ride them down: a choke point, a forest, a slope, or where they are. Where the riders sit on ordinary horses, the cavalry dismount and join the shield wall with polearms ready. The AI sides only; your own army is never touched. Needs Enable Culture Doctrine. Applies from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineHorseStand { get; set; } = true;

    // --- Performance / Dead Mount Cleanup ---
    // GroupOrder 49: 10, 15, 20-48 and 100 were taken as of 2026-09-03.

    [SettingPropertyGroup("Performance/Dead Mount Cleanup", GroupOrder = 49)]
    [SettingPropertyBool("Despawn Dead Mounts", Order = 0, RequireRestart = false,
        HintText = "Remove killed horses, wargs and other mounts a few seconds after they die instead of leaving them on the field. A mount is a full agent with a skeleton and a live ragdoll, so a cavalry battle piles up corpses that cost frame time and do nothing. Field battles, sieges and sally-outs only; towns, arenas and tournaments keep their bodies. Side effect: freeing agent slots lets reinforcements arrive slightly sooner than vanilla.")]
    public bool EnableDeadMountDespawn { get; set; } = true;

    [SettingPropertyGroup("Performance/Dead Mount Cleanup")]
    [SettingPropertyFloatingInteger("Despawn Delay (seconds)", 3f, 30f, "#0", Order = 1, RequireRestart = false,
        HintText = "Seconds between a mount's death and its fade. Default 5. Below 3 the corpse pops while the death animation is still playing, so values outside 3-30 fall back to 5.")]
    public float DeadMountDespawnDelaySeconds { get; set; } = 5f;

    // --- Performance / Battle Corpses (#701) ---
    // GroupOrder 55: 10, 15, 19-54 and 100 were taken as of 2026-10-01.

    [SettingPropertyGroup("Performance/Battle Corpses", GroupOrder = 55)]
    [SettingPropertyBool("Clean Up Battle Corpses", Order = 0, RequireRestart = false,
        HintText = "Fade fallen soldiers after a set time and cap how many bodies a battle keeps, whatever your Number of Corpses option says. The engine walks its whole corpse list every frame, and players reported mid-battle freezes at high corpse settings. Field battles, sieges and sally-outs only; towns, arenas and hideouts keep their bodies. Never raises your own Number of Corpses option.")]
    public bool EnableBattleCorpseCleanup { get; set; } = true;

    [SettingPropertyGroup("Performance/Battle Corpses")]
    [SettingPropertyFloatingInteger("Corpse Fade Time (seconds)", 10f, 300f, "#0", Order = 1, RequireRestart = false,
        HintText = "Seconds a body stays on the field, counted from when it settles, before it fades. Default 60. The engine's own default is an hour, so without this a body only goes when the corpse cap pushes it out. Values outside 10-300 fall back to 60.")]
    public float BattleCorpseFadeSeconds { get; set; } = 60f;

    [SettingPropertyGroup("Performance/Battle Corpses")]
    [SettingPropertyInteger("Maximum Corpses", 0, 250, Order = 2, RequireRestart = false,
        HintText = "Most bodies a battle keeps at once; the oldest fade first. Default 25, the vanilla Low setting. If your own Number of Corpses option is lower, yours is used.")]
    public int BattleCorpseCap { get; set; } = 25;

    [SettingPropertyGroup("Performance/Battle Corpses")]
    [SettingPropertyBool("Recommend Battle Settings", Order = 3, RequireRestart = false,
        HintText = "On the first main menu after the game starts, offer to lower Number of Ragdolls to 5 and Number of Corpses to Low when yours are higher. Nothing changes unless you click Apply.")]
    public bool ShowBattleSettingsAdvice { get; set; } = true;

    [SettingPropertyGroup("Performance/Battle Corpses")]
    [SettingPropertyButton("Apply Recommended Battle Settings", Order = 4, RequireRestart = false,
        Content = "Apply",
        HintText = "Lower Number of Ragdolls to 5 and Number of Corpses to Low (25) in your game options now. A setting already lower is kept. Change them back at any time in Options, Performance.")]
    public System.Action ApplyRecommendedBattleSettingsAction { get; set; } = static () =>
    {
        // MCM calls this with no handler around it; a throw would vanish into the UI frame.
        try
        {
            TAOM.Features.BattleCorpses.BattleSettingsAdviceNotifier.Apply(
                TAOM.IoC.Resolve<TAOM.Features.BattleCorpses.BattleSettingsAdvisor>(),
                TAOM.IoC.Resolve<TAOM.Core.Logging.IModLogger>());
        }
        catch (System.Exception ex)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"[TAOM] Battle settings could not be applied: {ex.GetType().Name}: {ex.Message}", Colors.Red));
        }
    };

    // --- Battle Tactics / Companion Roles ---
    // GroupOrder 22 was originally planned but SmartCavalryAI parallel port consumed it.
    // CompanionTactics settings live at GroupOrder 27/28/29.

    [SettingPropertyGroup("Battle Tactics/Companion Roles", GroupOrder = 27)]
    [SettingPropertyBool("Enable Companion Role Tooltips", Order = 0, RequireRestart = false,
        HintText = "Append detected combat role (e.g., [BOW], [INF]) to companion/troop tooltips on the party screen.")]
    public bool EnableCompanionRoleTooltips { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Companion Roles")]
    [SettingPropertyBool("Enable OOB Role Display", Order = 1, RequireRestart = false,
        HintText = "Show role indicators on hero items in the Order of Battle screen.")]
    public bool EnableOOBRoleDisplay { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Companion Roles")]
    [SettingPropertyBool("Companion Roles Debug Mode", Order = 2, RequireRestart = false,
        HintText = "Show diagnostic [CompanionRoles] messages on the in-game HUD.")]
    public bool CompanionRolesDebug { get; set; } = false;

    // --- Battle Tactics / Formation Presets ---

    // Renamed from EnableFormationPresets (#1017): it was switched off in v2.0.8 (53a8cfc21) while loading a preset
    // was unwired, but a saved "on" survived under the same name. Loading a preset shipped in v2.0.34; the in-game
    // check is still owed, so the default stays off.
    [SettingPropertyGroup("Battle Tactics/Formation Presets", GroupOrder = 28)]
    [SettingPropertyBool("Enable Formation Presets", Order = 0, RequireRestart = false,
        HintText = "Presets save and load the formation types, captains and hero troops of the Order of Battle, per campaign. Off by default until it is checked in game; opt in to try it. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public bool EnableOobFormationPresets { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Formation Presets")]
    [SettingPropertyInteger("Max Formation Presets", 1, 20, Order = 1, RequireRestart = false,
        HintText = "Maximum saved formation presets per campaign. Save attempts beyond this limit are refused with a warning. Default: 10.")]
    public int MaxFormationPresets { get; set; } = 10;

    [SettingPropertyGroup("Battle Tactics/Formation Presets")]
    [SettingPropertyBool("Formation Presets Debug Mode", Order = 2, RequireRestart = false,
        HintText = "Show diagnostic [FormationPresets] messages.")]
    public bool FormationPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Battle Action Bar ---

    // Renamed from EnableBattleActionBar (default on) in #970. MCM keeps a saved value per property, so
    // flipping that default would have reached fresh installs only; a new property name is what reaches
    // every install. The old key in TAOM.json is ignored.
    [SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 29)]
    [SettingPropertyBool("Show Battle Action Bar", Order = 0, RequireRestart = false,
        HintText = "Parked: show a bar of stance buttons during field battles. The buttons record a stance but do not change formation behavior yet, and they can be clicked only while a menu shows the cursor (for example Esc). Default: off.")]
    public bool ShowBattleActionBar { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Cancel Stance On Move", Order = 1, RequireRestart = false,
        HintText = "Auto-clear stance when the formation receives a movement order.")]
    public bool CancelStanceOnMove { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Enable Volley Fire", Order = 2, RequireRestart = false,
        HintText = "Include 'Volley Fire' as a ranged action option.")]
    public bool EnableVolleyFire { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
    [SettingPropertyBool("Battle Action Bar Debug Mode", Order = 3, RequireRestart = false,
        HintText = "Show diagnostic [BattleActionBar] messages.")]
    public bool BattleActionBarDebug { get; set; } = false;

    // --- Map UI / Settlement Nameplates ---

    // --- World / Bandit Scaling ---

    [SettingPropertyGroup("World/Bandit Scaling", GroupOrder = 35)]
    [SettingPropertyBool("Enable Bandit Scaling", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below. Turning it off restores vanilla's live numbers at once (including its 2-party minimum for a camp to count as infested) and stops scaling future spawns; it deletes no bandit party already on the map and culls no hideout above the cap, so a swamped campaign thins only as they are cleared.")]
    public bool EnableBanditScaling { get; set; } = true;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Density Curve", 0.0f, 5.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "Multiplier on hideout count + parties-per-hideout at PlayerProgress=1.0. Curve: 1 + curve * progress. 0 = vanilla density throughout. 1.5 (default) = up to 2.5x density in endgame. Both caps below bind first.")]
    public float BanditDensityCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Party Size Curve", 0.0f, 5.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "Multiplier on bandit party troop counts at PlayerProgress=1.0. Vanilla already scales 0.4 -> 1.2; this is a final multiplier on top. 1.5 (default) = up to 2.5x bandit party sizes in endgame. Each troop stack is capped at its party template's max_value, which binds before 2.5x is reached.")]
    public float BanditPartySizeCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyFloatingInteger("Hideout First Fight Curve", 0.0f, 5.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "Multiplier on the FIRST fight's troop count inside hideouts at PlayerProgress=1.0. 1.5 (default) = up to 2.5x bandits in the camp-clearing phase of a hideout assault in endgame. The boss fight is not scaled: it is exactly the boss plus Hideout Boss Bodyguards below.")]
    public float BanditBossFightCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Max Hideouts Per Faction Cap", 1, 100, Order = 4, RequireRestart = false,
        HintText = "Hard cap on hideouts per bandit faction regardless of scaling curve. Vanilla = 9. Default: 100 (effectively the physical hideout count per faction).")]
    public int BanditMaxHideoutsPerFaction { get; set; } = 100;

    // Renamed from BanditMaxPartiesPerHideout (#1017): its default went 3 to 6 in v2.0.29 under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 3, which pins vanilla.
    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Max Parties Per Hideout Cap", 1, 20, Order = 5, RequireRestart = false,
        HintText = "Hard cap on bandit parties per hideout regardless of scaling curve. Vanilla = 3, which is also the floor: a cap at or below 3 pins the value at vanilla and the Density Curve cannot move it. Default: 6. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int BanditPartiesPerHideoutCap { get; set; } = 6;

    // Renamed from BanditInitialHideoutsPerFaction (#1017): its default went 14 to 7 in v2.0.29 under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 14.
    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Initial Hideouts Per Faction", 1, 30, Order = 6, RequireRestart = false,
        HintText = "Hideouts each bandit faction starts with on a NEW campaign; read once at world-gen, so changing it does nothing to a campaign already in progress. TAOM has 8 bandit factions, so this is a per-faction target, x8 on the map, bounded by each faction's physical hideout count (three have only 10). Vanilla = 7. Default: 7. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int BanditStartingHideoutsPerFaction { get; set; } = 7;

    [SettingPropertyGroup("World/Bandit Scaling")]
    [SettingPropertyInteger("Hideout Boss Bodyguards", 0, 10, Order = 7, RequireRestart = false,
        HintText = "Soldiers standing with the hideout boss in the final fight, on both the daytime assault and the night sneak-in. The fight is exactly the boss plus this many (0 = the boss alone), whether you take the duel or fight them all; the rest of the camp is cleared in the first phase. Applies with Enable Bandit Scaling off too. Vanilla fields 5 to 10 with the boss. Default: 4.")]
    public int BanditHideoutBossBodyguards { get; set; } = 4;

    // --- World / Recruitment Alignment ---

    [SettingPropertyGroup("World/Recruitment Alignment", GroupOrder = 36)]
    [SettingPropertyBool("Enable Recruitment Alignment Block", Order = 0, RequireRestart = false,
        HintText = "When enabled, a recruiter cannot recruit volunteers at a settlement controlled by an opposed-alignment kingdom (Free vs Evil). Alignment comes from execution/alignment.json, keyed by the kingdom you serve. Neutral factions (Umbar etc.) never block. When off, recruitment is vanilla.")]
    public bool EnableAlignmentRecruitment { get; set; } = true;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Only Good Rejects Evil", Order = 1, RequireRestart = false,
        HintText = "When ON, only a Free-aligned recruiter is blocked from Evil-controlled settlements; Evil recruiters may recruit anywhere. When OFF (default), the block is symmetric — Free and Evil each refuse the other.")]
    public bool AlignmentRecruitmentGoodRejectsEvilOnly { get; set; } = false;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Apply To Player", Order = 2, RequireRestart = false,
        HintText = "When ON (default), YOU are blocked from recruiting in opposed-alignment settlements. When OFF, you may recruit anyone regardless of alignment (AI lords are still gated if 'Apply To AI Lords' is on). The master 'Enable Recruitment Alignment Block' toggle off disables the whole feature for everyone.")]
    public bool EnableAlignmentRecruitmentPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Recruitment Alignment")]
    [SettingPropertyBool("Apply To AI Lords", Order = 3, RequireRestart = false,
        HintText = "When ON (default), AI lords are also blocked from recruiting in opposed-alignment settlements. When OFF, AI recruits freely (you are still gated if 'Apply To Player' is on).")]
    public bool EnableAlignmentRecruitmentAi { get; set; } = true;

    // --- World / Marriage Alignment ---

    [SettingPropertyGroup("World/Marriage Alignment", GroupOrder = 50)]
    [SettingPropertyBool("Enable Marriage Alignment Block", Order = 0, RequireRestart = false,
        HintText = "When enabled, a Free-aligned hero cannot marry an Evil-aligned one (no Gondorian weds an orc). Alignment comes from execution/alignment.json, keyed by the hero's CULTURE. Neutral cultures (Khand, Umbar, Shaghana, Abanissa) marry anyone; Dunland is Evil. When off, marriage is vanilla.")]
    public bool EnableMarriageAlignment { get; set; } = true;

    [SettingPropertyGroup("World/Marriage Alignment")]
    [SettingPropertyBool("Apply To Player", Order = 1, RequireRestart = false,
        HintText = "When ON (default), YOUR clan is also held to the rule — you and your siblings/children cannot marry across the Free/Evil line. When OFF, the player clan marries freely (AI lords are still gated if 'Apply To AI Lords' is on).")]
    public bool EnableMarriageAlignmentPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Marriage Alignment")]
    [SettingPropertyBool("Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "When ON (default), AI lords are blocked from marrying across the Free/Evil line. When OFF, AI marries freely (you are still gated if 'Apply To Player' is on).")]
    public bool EnableMarriageAlignmentAi { get; set; } = true;

    [SettingPropertyGroup("World/Marriage Alignment")]
    [SettingPropertyBool("Steer AI Partner Search", Order = 3, RequireRestart = false,
        HintText = "When ON (default), AI lords look for a partner among compatible clans instead of drawing uniformly from every clan in the world. Vanilla's draw wastes the day whenever it lands on a blocked clan, which would make Free factions (Gondor, Rohan, Dale) marry far less often and run short of heirs. Turn OFF to keep the block but restore vanilla's draw.")]
    public bool MarriageAlignmentSteerAiSearch { get; set; } = true;

    // --- World / Wanderer Allegiance ---

    [SettingPropertyGroup("World/Wanderer Allegiance", GroupOrder = 51)]
    [SettingPropertyBool("Enable Wanderer Allegiance", Order = 0, RequireRestart = false,
        HintText = "When enabled, a wanderer will not take service across the Free/Evil line: Free-culture wanderers (Gondor, Rohan, the Elves, Erebor and the rest) refuse a player who serves Sauron, and Evil-culture wanderers refuse a player on the Free side. Your side is the kingdom you serve (execution/alignment.json), falling back to your clan's culture when you serve no kingdom. Neutral cultures serve anyone and a Neutral player is refused by nobody. Companions already in your clan are never affected. When off, hiring is vanilla.")]
    public bool EnableWandererAllegiance { get; set; } = true;

    [SettingPropertyGroup("World/Wanderer Allegiance")]
    [SettingPropertyDropdown("Who Refuses", Order = 1, RequireRestart = false,
        HintText = "All wanderers (default): every wanderer, by culture. Named companions only: just the lore characters from named_companions.xml (Aragorn, Legolas, Gimli and the rest); generic tavern wanderers hire as in vanilla.")]
    public Dropdown<string> WandererAllegianceScope { get; set; } = new Dropdown<string>(
        new[] { "All wanderers (by culture)", "Named companions only" }, 0);

    // --- World / Hard Questions ---

    // GroupOrder 57: 9, 10, 15, 19-56 and 100 were taken as of 2026-10-18.
    [SettingPropertyGroup("World/Hard Questions", GroupOrder = 57)]
    [SettingPropertyBool("Enable Hard Questions", Order = 0, RequireRestart = false,
        HintText = "When enabled, a lord held prisoner by your clan (in your party or in one of your settlements) can be questioned about his clan's or kingdom's lands, for a bribe or by force, and stripped of his arms and armour. Force and stripping cost relation, Honor and Mercy, and stripping costs renown. Each topic works once per captivity. When off, those conversation options do not appear.")]
    public bool EnableHardQuestions { get; set; } = true;

    // --- World / Lord Courtesies ---

    // GroupOrder 63: 0, 1, 2, 9, 10, 15, 19-62 and 100 were taken as of 2026-10-20 (rebase on trunk 599987653).
    [SettingPropertyGroup("World/Lord Courtesies", GroupOrder = 63)]
    [SettingPropertyBool("Enable Troop Gifts", Order = 0, RequireRestart = false,
        HintText = "When enabled, a conversation with a lord who leads a party offers a line to gift him troops through a donate-style party screen. The lord must not be in your clan or kingdom, not at war with you, and not on the opposite side of the Free/Evil divide (a Neutral lord may receive). Troops move from you to him only. The gift is paid by Bonds of Battle's hand-over rule (half the influence the gift is worth, at most 10 relation a day per relation owner), so the relation needs Bonds of Battle and its Troop Gifts Win Lords' Favour switch on; with either off the gift is still accepted but earns nothing. When off, the line does not appear.")]
    public bool EnableTroopGifts { get; set; } = true;

    [SettingPropertyGroup("World/Lord Courtesies")]
    [SettingPropertyBool("Enable Domain Reveal", Order = 1, RequireRestart = false,
        HintText = "When enabled, you can ask a free lord about his clan's holdings. A lord who knows you well enough names them and they are marked on your map; a lord who does not refuses, and a lord whose clan holds nothing says so, each at a small cost in relation. Lords at war with you and your own clan never answer. When off, the line does not appear.")]
    public bool EnableDomainReveal { get; set; } = true;

    // --- World / Notable Services (#899) ---

    // GroupOrder 77: 0-5, 9, 10, 15, 19-71, 76 and 100 were taken as of 2026-10-24 (the highest below the Map Tools sentinel is 76).
    [SettingPropertyGroup("World/Notable Services", GroupOrder = 77)]
    [SettingPropertyBool("Enable Notable Services", Order = 0, RequireRestart = false,
        HintText = "When enabled, a notable of the settlement you stand in (an artisan, gang leader, merchant, rural notable or headman, but not a preacher) offers three lines in conversation: give men to the settlement (troops raise its militia, prisoners its hearth in a village or its prosperity in a town; a castle takes troops only), pay to be introduced to the other notables there, and pay to have the nearest towns and castles marked on your map. Single player only; all three lines are hidden during a messenger conversation. At war with the settlement's realm only the gift line is hidden: the introduction still works and the holds question is refused. The gift line is also hidden while a party screen is open or when you hold no regular troop or prisoner. Amounts are in notable_services.json. When off, none of the lines appear. Default: on.")]
    public bool EnableNotableServices { get; set; } = true;

    // --- World / Bandit Voices (#900) ---

    // GroupOrder 78 is used by no other group (77 is Notable Services, 76 War of the Ring/Armies; checked 2026-10-26).
    [SettingPropertyGroup("World/Bandit Voices", GroupOrder = 78)]
    [SettingPropertyBool("Enable Bandit Clan Greetings", Order = 0, RequireRestart = false,
        HintText = "When enabled, if you attack on land a bandit party you have not dealt with before and it belongs to one of the eight talking clans (Dunlending, Rhun, Haradrim, Gundabad, Umbar, Gondor deserters, Blacklocks, Mirkwood Stalkers), there is a one in four chance that it greets you in its own words, and your ultimatum is vanilla's reply to that kind of greeting. Spiders, wild trolls and looters are unchanged. Applies to the next bandit conversation. Text only: nothing else about the encounter changes. When off, every greeting is vanilla's. The lines bandits use when they rob you are separate data and always apply. Default: on.")]
    public bool EnableBanditGreetings { get; set; } = true;

    // --- World / Inn Rounds (#901) ---

    // Read live at each tavern keeper conversation, through InnRoundsSettingsProvider's one cached settings reference, so none
    // needs a restart. GroupOrder 79 is used by no other group (78 is Bandit Voices, 77 Notable Services; checked 2026-10-26); it
    // sits on the group's first property, because MCM ignores a GroupOrder on any later one.
    [SettingPropertyGroup("World/Inn Rounds", GroupOrder = 79)]
    [SettingPropertyBool("Enable Inn Rounds", Order = 0, RequireRestart = false,
        HintText = "When enabled, the tavern keeper of a town offers three more lines: stand a round for your whole party (morale and a little Charm experience), stand a round for everyone in the tavern (renown and a little Charm experience), and pay to have the nearest towns and castles marked on your map. A round costs the town's price for the local drink for each head, capped, and the gold goes to the town. At most a few rounds a day, set in inn_rounds.json. Single player only; the lines are hidden during a messenger conversation. The map question is refused while the town's realm is at war with yours or its owner does not know you well enough. When off, none of the lines appear. Default: on.")]
    public bool EnableInnRounds { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("World/Inn Rounds")]
    [SettingPropertyFloatingInteger("Inn Round Morale Scale", 0f, 3f, "0.00", Order = 1, RequireRestart = false,
        HintText = "Multiplies the morale a party round gives (20 to 40 before the scale, set in inn_rounds.json). 0 gives none; the party's morale never goes past 100. Between 0 and 3. Default: 1.")]
    public float InnRoundsMoraleScale { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultScale;

    [SettingPropertyGroup("World/Inn Rounds")]
    [SettingPropertyFloatingInteger("Inn Round Renown Scale", 0f, 3f, "0.00", Order = 2, RequireRestart = false,
        HintText = "Multiplies the renown a house round gives (1 to 3 before the scale, set in inn_rounds.json), rounded to a whole number. 0 gives none. Between 0 and 3. Default: 1.")]
    public float InnRoundsRenownScale { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultScale;

    // --- Prisoner Recruitment ---

    [SettingPropertyGroup("World/Prisoner Recruitment", GroupOrder = 39)]
    [SettingPropertyBool("Waive Same-Side Prisoner Morale Cost", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), recruiting a prisoner of your own faction or your own side of the war costs no morale — an Isengard captor loses nothing taking on Mordor, Gundabad or Dunland troops, since all serve Sauron. Alignment comes from execution/alignment.json. Neutral factions (Khand, Umbar) only waive their own culture, not each other. When off, prisoner morale is vanilla (-1 per troop, -2 per bandit).")]
    public bool EnablePrisonerRecruitmentWaiver { get; set; } = true;

    [SettingPropertyGroup("World/Prisoner Recruitment")]
    [SettingPropertyBool("Apply To Player", Order = 1, RequireRestart = false,
        HintText = "When ON (default), YOU pay no morale for same-side prisoners. When OFF, you pay the vanilla cost (AI lords still get the waiver if 'Apply To AI Lords' is on).")]
    public bool EnablePrisonerRecruitmentWaiverPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Prisoner Recruitment")]
    [SettingPropertyBool("Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "When ON (default), AI lords also pay no morale for same-side prisoners, so allied factions absorb each other's captured troops more readily. When OFF, only you benefit (if 'Apply To Player' is on).")]
    public bool EnablePrisonerRecruitmentWaiverAi { get; set; } = true;

    // --- Naval Travel ---

    [SettingPropertyGroup("World/Naval Travel", GroupOrder = 37)]
    [SettingPropertyBool("Enable Naval Travel", Order = 0, RequireRestart = false,
        HintText = "Currently DISABLED — the map's navmesh isn't set up for naval travel yet, so this feature is parked in code and this toggle has no effect for now. (When re-enabled: parties sail across water on the campaign map — the engine's native naval travel, unlocked without the Naval DLC.)")]
    public bool EnableNavalTravel { get; set; } = false;

    [SettingPropertyGroup("World/Naval Travel")]
    [SettingPropertyBool("Apply To Player", Order = 1, RequireRestart = false,
        HintText = "When ON (default), YOUR party can sail. When OFF, you stay land-bound (AI still sails if 'Apply To AI Lords' is on). The master 'Enable Naval Travel' toggle off disables sailing for everyone.")]
    public bool NavalTravelApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Naval Travel")]
    [SettingPropertyBool("Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "When ON (default), AI lords' parties can also sail. When OFF, only the player sails and AI stays on land — the conservative option if AI naval routing looks odd.")]
    public bool NavalTravelApplyToAi { get; set; } = true;

    // --- World / Alignment Desertion ---

    [SettingPropertyGroup("World/Alignment Desertion", GroupOrder = 38)]
    [SettingPropertyBool("Enable Alignment Desertion", Order = 0, RequireRestart = false,
        HintText = "When enabled, troops whose culture is opposed in alignment to their lord (Free vs Evil) desert each day — an Evil lord sheds Good troops and a Good lord sheds Evil troops. Alignment comes from execution/alignment.json. Neutral cultures (Umbar etc.) never desert. When off, vanilla.")]
    public bool EnableAlignmentDesertion { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyFloatingInteger("Daily Desertion Rate", 0f, 1f, "#0%", Order = 1, RequireRestart = false,
        HintText = "Fraction of each opposed-alignment troop type that deserts per day (minimum 1 per type). Default 0.50 = half per day. Higher clears mixed parties/garrisons faster.")]
    public float AlignmentDesertionRate { get; set; } = 0.5f;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Player", Order = 2, RequireRestart = false,
        HintText = "When ON (default), YOUR party and garrisons shed opposed-alignment troops. When OFF, you may keep mixed-alignment troops (AI is still affected if 'Apply To AI Lords' is on).")]
    public bool EnableAlignmentDesertionPlayer { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To AI Lords", Order = 3, RequireRestart = false,
        HintText = "When ON (default), AI lords' parties and garrisons also shed opposed-alignment troops. When OFF, only the player is affected (if 'Apply To Player' is on).")]
    public bool EnableAlignmentDesertionAi { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Mobile Parties", Order = 4, RequireRestart = false,
        HintText = "When ON (default), lords' field parties (including army members) shed opposed troops. When OFF, only garrisons are affected (if 'Apply To Garrisons' is on).")]
    public bool EnableAlignmentDesertionParties { get; set; } = true;

    [SettingPropertyGroup("World/Alignment Desertion")]
    [SettingPropertyBool("Apply To Garrisons", Order = 5, RequireRestart = false,
        HintText = "When ON (default), settlement garrisons shed opposed troops (e.g. a conquered fief's old-culture defenders leave). When OFF, only mobile parties are affected (if 'Apply To Mobile Parties' is on).")]
    public bool EnableAlignmentDesertionGarrisons { get; set; } = true;

    // --- Combat Mechanics (CombatMechanics feature — crush-through, cleave, charge knockdown) ---

    [SettingPropertyGroup("Combat Mechanics", GroupOrder = 24)]
    [SettingPropertyBool("Enable Combat Mechanics", Order = 0, RequireRestart = false,
        HintText = "Master toggle for the combat feel pack: skill-based crush-through, monster/orc crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers. When off, everything below is inert and combat behaves exactly as before this feature.")]
    public bool EnableCombatMechanics { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Skill-Based Crush-Through", Order = 1, RequireRestart = false,
        HintText = "High-energy swings from an attacker who greatly outskills the defender have a chance to crush through the block (chance grows with skill gap and swing momentum; off-angle swings are penalized). Tuning constants live in combat_mechanics_config.json.")]
    public bool EnableSkillCrushThrough { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Monster Crush-Through", Order = 2, RequireRestart = false,
        HintText = "Troll, mumakil, war elephant and giant spider melee attacks can only be blocked with a SHIELD — a bare-weapon block is crushed through automatically.")]
    public bool EnableMonsterCrushThrough { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Orc Shield Crush-Through", Order = 3, RequireRestart = false,
        HintText = "AI orc/uruk swings can crush through even SHIELD blocks (energy- and skill-gated) — turtling against an orc horde is no longer safe. Never applies to the player's own attacks.")]
    public bool EnableOrcShieldCrush { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Creature Cleave", Order = 4, RequireRestart = false,
        HintText = "Troll and mumakil swings slice through a victim and carry 30% momentum into the next — one swing can hit multiple soldiers, including through shield blocks that take damage (a zero-damage block still stops the swing).")]
    public bool EnableCreatureCleave { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Creature Stagger Immunity", Order = 5, RequireRestart = false,
        HintText = "Big creatures shrug off hits below a per-creature damage threshold — no flinch-locking a troll with arrows. Thresholds live in combat_mechanics_config.json.")]
    public bool EnableCreatureUnstoppable { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Weight-Based Charge Knockdown", Order = 6, RequireRestart = false,
        HintText = "Mount charges knock down based on relative weight (charger + rider vs victim), charge speed and the victim's race resistance: a mumakil bowls over infantry, a horse can't floor a troll, dwarves stand their ground.")]
    public bool EnableChargeKnockdown { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Shield Penetration", Order = 7, RequireRestart = false,
        HintText = "Missiles from config-listed weapon classes/items can penetrate shields and damage the soldier behind. Off by default, and the shipped lists are empty, so javelins behave as in the base game: only a HERO with the Throwing Impale perk pierces a shield. Regular troops never do, in vanilla or here. Turning this on with an empty config changes nothing; add entries in combat_mechanics_config.json first.")]
    public bool EnableShieldPenetration { get; set; } = false;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Race Combat Modifiers", Order = 8, RequireRestart = false,
        HintText = "Per-race combat flavor from combat_mechanics_config.json: dwarves resist knockdown and stagger, elves crush through better and strike true from any angle, orcs swing with brute momentum. When off, all races use neutral values.")]
    public bool EnableRaceCombatModifiers { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Crush-Through Max Chance", 0f, 1f, "#0%", Order = 9, RequireRestart = false,
        HintText = "Ceiling for the skill-based crush-through chance, reached at a huge skill gap with a full-momentum swing. Default 50%.")]
    public float CrushThroughMaxChance { get; set; } = 0.5f;

    // Renamed from ChargeAutoKnockdownWeightRatio (#1017): its default went 8 to 6 in v2.0.30 under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 8.
    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyInteger("Auto-Knockdown Weight Ratio", 2, 30, Order = 10, RequireRestart = false,
        HintText = "Charger-to-victim weight ratio at which a charge ALWAYS knocks the victim down, from any angle, regardless of resistance (mumakil vs man is ~125, warg + orc vs man 8, horse + man vs man exactly 6). Default 6 since 2026-09-17, so a full-speed horse contact floors a man; raise it to 7 or 8 to give that back to wargs and chariots only. Values below the neutral weight ratio are treated as the neutral ratio. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int ChargeAutoKnockdownRatio { get; set; } = 6;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Charge Neutral Weight Ratio", 1f, 30f, "0.0", Order = 13, RequireRestart = false,
        HintText = "Charger-to-victim weight ratio at which a charge knocks down exactly as vanilla does (needs about 8 damage on a head-on hit). Heavier chargers than this ratio get MORE penetration, lighter ones less, down to the min factor. Default 6 = horse + man vs man. Lower it and every charge hits harder; raise it and only the heavy mounts do.")]
    public float ChargeNeutralWeightRatio { get; set; } = 6f;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Charge Penetration", 0f, 1f, "0.00", Order = 14, RequireRestart = false,
        HintText = "The base knockdown penetration of a charge at the neutral ratio and full speed. Default 0.4, vanilla's value, which cancels a troop's 0.4 base resistance so only Athletics protects him. 0.5 and up floors an ordinary man on any head-on hit that does damage; 0.3 needs a hard hit.")]
    public float ChargeHorsePenetration { get; set; } = 0.4f;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Charge Min Penetration Factor", 0f, 2.5f, "0.00", Order = 15, RequireRestart = false,
        HintText = "How far below vanilla the weight term may push a light charger's penetration against a heavy victim. Default 1.0 = never below vanilla (uruks and trolls fall like men; the troll, dwarf and Sauron race rows do the resisting). 0.25 restores the pre-2026-09-17 feel where a horse could not floor anything heavier than a man. The ceiling is the JSON's maxPenetrationFactor (shipped 2.5); a slider value above it is treated as that maximum.")]
    public float ChargeMinPenetrationFactor { get; set; } = 1f;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Culture Charge Damage", Order = 16, RequireRestart = false,
        HintText = "Cavalry charge damage scales by the rider's culture (elves 1.6x, Rohan 1.5x, Rhun 1.4x, Gondor 1.3x, Dale and the orc kingdoms 1.2x, Erebor 1.0x). The table lives in combat_mechanics_config.json; this switch turns the whole thing off.")]
    public bool EnableCultureChargeDamage { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Signature Strikes", Order = 11, RequireRestart = false,
        HintText = "Configured heroes (shipped: Sauron and the Nine) get direction-mapped effects on any melee hit. Sauron: an overhead is a Slam (the struck foe always goes down, enemies in a ring around the impact are knocked down and take part of the damage, a burst of fear drains their morale; also fires on an overhead into the ground), a side swing is a Sweep (the struck foe and enemies in front are staggered back). The Nine: an overhead or a side swing is a Scream (enemies around the wraith take part of the damage, are staggered back and lose morale; the struck foe is staggered and frightened too). Never on a throw, a shot, a kick or bare hands. The same rules apply in a Custom Battle. Cooldowns and radii live in signature_strikes_config.json.")]
    public bool EnableSignatureStrikes { get; set; } = true;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyFloatingInteger("Signature Strike Cooldown Multiplier", 0.5f, 5f, "0.0x", Order = 12, RequireRestart = false,
        HintText = "Multiplies every signature-strike cooldown (shipped: Sauron's Slam 20 s and Sweep 12 s, the Nine's Scream 15 s). Raise it if Sauron or the Nazgul feel overpowered; the JSON values need a full restart, this slider applies live.")]
    public float SignatureStrikeCooldownMultiplier { get; set; } = 1f;

    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyBool("Hammerhand (Shield Bash Knockdown)", Order = 17, RequireRestart = false,
        HintText = "A shield bash or a kick can floor a man. The chance grows with the attacker's Athletics lead over the victim; the victim's race and any race ability that toughens him cut it, a kick is weaker than a bash, and you resist more than a soldier does. Fighters who shrug off blows never fall: trolls while Creature Stagger Immunity is on, a soldier with a live race ability that shrugs off blows, and a hero with a career shrug-off passive. The numbers live in combat_mechanics_config.json.")]
    public bool EnableShieldBashKnockdown { get; set; } = true;

    // --- Aura of Dread ---

    [SettingPropertyGroup("Aura of Dread", GroupOrder = 34)]
    [SettingPropertyBool("Enable Aura of Dread", Order = 0, RequireRestart = false,
        HintText = "Nazgul and Sauron drain the morale of enemy troops right beside them. The aura weakens them so they break sooner in the fighting, but it never routs anyone by itself. Takes effect immediately, but morale already drained is not restored when you switch it off.")]
    public bool EnableDreadAura { get; set; } = true;

    // Renamed from DreadAuraRadius on 2026-10-15, when the default dropped from 12 to 5: MCM keeps a
    // saved value per property name, so the old name would have kept every existing 12. Never rename
    // it back, because settings files not yet re-saved still hold DreadAuraRadius = 12.
    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyFloatingInteger("Dread Radius", 4f, 15f, "#0", Order = 1, RequireRestart = false,
        HintText = "Metres over which a wraith projects dread. Full strength within the inner radius from dread_aura_config.json (default 2m), falling to nothing at this distance. Default 5. The ceiling is 15: beyond 15 m the engine's proximity search stops using its grid and checks every agent on the field, on every pulse.")]
    public float DreadAuraCloseRadius { get; set; } = 5f;

    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyFloatingInteger("Morale Drained Per Second", 0f, 20f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "Morale per second drained at full strength, before the target's tier/hero resistance and its racial resistance. The aura stops at a morale floor of 10 (dread_aura_config.json), so it never routs anyone by itself. At the default 5.0, in an even fight, a tier-3 human reaches the floor after about 22 seconds of contact, a tier-6 elite about 37, and an elven hero of level 16 or more never. Below about 1.0 the engine's own morale recovery outpaces the drain.")]
    public float DreadAuraMoralePerSecond { get; set; } = 5f;

    [SettingPropertyGroup("Aura of Dread")]
    [SettingPropertyBool("Affects Your Own Troops", Order = 3, RequireRestart = false,
        HintText = "When off, your team is immune and dread only touches the AI. Left on, an enemy wraith weakens your soldiers so they rout sooner in combat, and routed troops are LOST from your party. Your own character is always immune either way: the engine gives player-controlled agents no morale component at all.")]
    public bool DreadAuraAffectsPlayerTroops { get; set; } = true;

    // --- Battle Tactics / Race Abilities ---

    [SettingPropertyGroup("Battle Tactics/Race Abilities", GroupOrder = 56)]
    [SettingPropertyBool("Enable Race Abilities", Order = 0, RequireRestart = false,
        HintText = "AI soldiers fight with a battle ability for their race (or, for men, their culture) on a cooldown, fired when the moment comes and joined by nearby kin: berserkers go berserk when hurt in melee, Uruk-hai fall into bloodlust on a kill, dwarves stand fast against a charge or a crowd, elves quicken, orcs swarm, Rohan's riders spur on, Gondor closes ranks, and more. Your own character never fires one. Switching it on takes effect from the next battle; switching it off stops new abilities at once and lets running ones finish. Numbers live in race_abilities.json; the console command taom.print_race_abilities shows what each ability has done in the battle on screen.")]
    public bool EnableRaceAbilities { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Race Abilities")]
    [SettingPropertyBool("War Cries", Order = 1, RequireRestart = false,
        HintText = "The soldier who fires an ability, and a few of the kin who join him, shout in their own voice.")]
    public bool RaceAbilityWarCries { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Race Abilities")]
    [SettingPropertyBool("Show Ability Messages", Order = 2, RequireRestart = false,
        HintText = "One line in the message log when five or more soldiers on either side fire the same ability within two seconds. Never one line per soldier.")]
    public bool RaceAbilityMessages { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Race Abilities")]
    [SettingPropertyBool("Race Ability Debug Log", Order = 3, RequireRestart = false,
        HintText = "Writes one log line for every wave of abilities and every phase change (active, spent, ready). Off, the log still gets a summary every 30 s of activity and at the end of each battle. For checking that the abilities work; leave off in normal play.")]
    public bool RaceAbilityDebugLog { get; set; } = false;

    [SettingPropertyGroup("Battle Tactics/Race Abilities")]
    [SettingPropertyBool("Ability Glow and Sparks", Order = 4, RequireRestart = false,
        HintText = "A soldier whose ability is active wears a coloured outline: red for fury, steel blue for a guard, violet for dread; the speed and aim abilities stay unlit. Sparks burst on the soldier who fires and on a few of the kin who join him. Only the soldiers nearest the camera are outlined (visuals.maxGlowing in race_abilities.json). Drawn on this client only, and hidden while Hide Battle UI is on.")]
    public bool RaceAbilityGlow { get; set; } = true;

    // --- World / Uncapturable Heroes ---

    [SettingPropertyGroup("World/Uncapturable Heroes", GroupOrder = 48)]
    [SettingPropertyBool("Sauron and the Nazgûl Cannot Be Captured", Order = 0, RequireRestart = false,
        HintText = "When you beat them, they escape the field as fugitives instead of going to your dungeon, and return with a new army within days. This does NOT make them immortal: one of the Nine can still die in a battle he could not be captured in. Edit uncapturable_heroes_config.json to change who this covers.")]
    public bool EnableUncapturableHeroes { get; set; } = true;

    // --- Map UI / Settlement Nameplates ---

    [SettingPropertyGroup("Map UI/Settlement Nameplates", GroupOrder = 40)]
    [SettingPropertyBool("Enable Settlement Nameplate Fade", Order = 0, RequireRestart = false,
        HintText = "Fade settlement nameplates with camera distance. When off, all nameplates display at full visibility regardless of distance (vanilla).")]
    public bool EnableNameplateFade { get; set; } = true;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Fade Start Distance", 5f, 500f, "#0", Order = 1, RequireRestart = false,
        HintText = "Camera distance at which fade begins. Nameplates closer than this stay fully opaque. Default 80.")]
    public float NameplateFadeNearDistance { get; set; } = 80f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Fade End Distance", 10f, 1000f, "#0", Order = 2, RequireRestart = false,
        HintText = "Camera distance at which fade completes. Nameplates farther than this are fully hidden. Must be greater than Fade Start Distance. Default 200.")]
    public float NameplateFadeFarDistance { get; set; } = 200f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyBool("Colour Nameplates by Relation", Order = 3, RequireRestart = false,
        HintText = "Tint each settlement plate by your relation to its owner: green for your own faction, red for an enemy, blue for an allied kingdom, plain parchment for neutral. Off leaves every plate parchment. Applies immediately.")]
    public bool EnableNameplateRelationColors { get; set; } = true;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Relation Tint Strength (%)", 0f, 100f, "#0", Order = 4, RequireRestart = false,
        HintText = "How strongly the relation colour is applied to the bar, the name and the diamond frame. 100 is the full palette, 0 is plain parchment for everyone. Applies immediately. Default 100.")]
    public float NameplateRelationTintStrength { get; set; } = 100f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Neutral Plate Opacity (%)", 10f, 100f, "#0", Order = 5, RequireRestart = false,
        HintText = "Opacity of a neutral settlement's plate. Vanilla uses 35, which can be faint over dark terrain. Tracked settlements keep vanilla's 80. Applies immediately. Default 35.")]
    public float NameplateNeutralPlateOpacity { get; set; } = 35f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyFloatingInteger("Coloured Plate Opacity (%)", 10f, 100f, "#0", Order = 6, RequireRestart = false,
        HintText = "Opacity of a plate owned by your own faction, an enemy or an allied kingdom. Vanilla uses 50 for your own faction and 35 for the others. Tracked settlements keep vanilla's 80. Applies immediately. Default 50.")]
    public float NameplateRelationPlateOpacity { get; set; } = 50f;

    [SettingPropertyGroup("Map UI/Settlement Nameplates")]
    [SettingPropertyBool("{=taom_hroad_mcm_enable}Harvest Road notices", Order = 7, RequireRestart = false,
        HintText = "{=taom_hroad_mcm_enable_hint}Over a town's nameplate, show the small trade pop-ups the base game raises only for caravans, also when a villager delivers goods there and when a lord's party buys food there. Display only: the trades themselves are unchanged. Applies immediately. Default: on.")]
    public bool EnableHarvestRoadNotices { get; set; } = true;

    // --- Map UI / Party Icons ---

    [SettingPropertyGroup("Map UI/Party Icons", GroupOrder = 41)]
    [SettingPropertyFloatingInteger("Map Figure Scale", 0.05f, 1.0f, "#0.00", Order = 0, RequireRestart = false,
        HintText = "Size of party-icon figures and their mounts on the campaign map. Vanilla = 0.30; default 0.15 = half (makes parties feel smaller relative to settlements). Applies on the next icon rebuild after changing.")]
    public float MapFigureScale { get; set; } = 0.15f;

    // --- Map UI / Hidden Hosts ---

    [SettingPropertyGroup("Map UI/Hidden Hosts", GroupOrder = 42)]
    [SettingPropertyBool("Hide Exact Troop Counts", Order = 0, RequireRestart = false,
        HintText = "Another party's troop count on the campaign map blurs with its distance from you: '1??', '12?', then exact as you close in, and '...' at the very edge of sight. Standing closer, a higher Scouting skill and hosts of your own realm all sharpen it. Wounded and prisoner counts show only when the count is exact. A count you read stays remembered with a '~' and fades by the hour, and reading an enemy host's count from afar trains your scout's Scouting; Off stops both. AI lords still see true numbers. A host's orders in its hover tooltip stay hidden until its count is exact. Off = vanilla counts and orders. Applies immediately. Default: on.")]
    public bool HiddenHostsEnabled { get; set; } = true;

    [SettingPropertyGroup("Map UI/Hidden Hosts")]
    [SettingPropertyBool("Shorter Sight Under Trees", Order = 1, RequireRestart = false,
        HintText = "In vanilla, forest makes other parties harder for you to see and slows your party by 20%; it never hides your party from anyone. With this on, your own sight on the campaign map also shrinks by 30% of your base sight while your party travels through forest, and by half that if your scout has the Keen Sight perk. It does not apply inside a settlement. A raised lookout, perks and a Far Watch ride still add to your sight, and the penalties together never take more than half of your base sight. Only your own party is affected; AI parties see as before. The map bar view distance shows the shorter range, but its tooltip has no line for the forest. Off = vanilla sight in forest. Applies immediately. Default: on.")]
    public bool ForestSightPenaltyEnabled { get; set; } = true;

    // --- Kingdom Politics / Fief Grants ---
    //
    // #458, #565. When a kingdom takes a town or castle it holds an election for the new owner.
    // Every clan is scored by SettlementClaimantDecision.CalculateMeritOfOutcome, the top three by
    // merit go on the ballot, and every non-mercenary clan votes 1/2/3 points. Merit weights the
    // election heavily but does not decide it outright: a finalist that cannot afford its vote is
    // downgraded, so the "all three finalists tie" claim this comment once made was wrong (refuted
    // in review, rca-fiefgranting-2026-08-14.md C2). These knobs move that merit score, plus the
    // King's Vote that lets a rich ruler overrule the council. Weights are read live per election,
    // so no restart and no new campaign. The master toggle is applied when a decision is CREATED,
    // so it does not retrofit an already-pending election.

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("Enable Fief Grant Rebalance", Order = 0, RequireRestart = false,
        HintText = "Rebalances who gets a captured town or castle. Off = exact vanilla, where the ruling clan's +60 merit and its unlimited King's Vote override let it take fief after fief. Does not change starting ownership. The ballot's fief count shows either way.")]
    public bool EnableFiefGrantRebalance { get; set; } = true;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Siege Participation Bonus", 1.0f, 5.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Merit multiplier for the clan that carried the winning assault: the largest share of the winning besieging side's battle contribution, the same number the loot split uses. Every other clan of that kingdom that fielded a party there gets it in proportion, 1 + (bonus - 1) x its share of the top clan's contribution, so a clan with half the top clan's contribution gets half the bonus. Mercenaries and allies from other kingdoms cannot be granted the fief and are not counted. Vanilla's flat +30 for the assault leader's clan is untouched. 1.00 = off. Default: 2.50.")]
    public float FiefGrantCapturerBonus { get; set; } = 2.5f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Absent From Siege Factor", 0.1f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Merit multiplier for a clan that had no party in the winning assault, so land goes to the houses that fought for it. Applies to your clan as well, exempt or not: this is about what a clan did, not what it holds. No effect when nothing is on record for the settlement, such as a fief that changed hands without an assault. 1.00 = off. Default: 0.50.")]
    public float FiefGrantAbsentFromSiegeFactor { get; set; } = 0.5f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Landless Clan Bonus", 1.0f, 5.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "Merit multiplier for a clan that holds no town or castle at all. Keeps poor clans able to field parties instead of withering and leaving the kingdom. Vanilla already favours landless clans (a flat +30 and nothing in its holdings divisor), so this stacks on a strong base. 1.00 = no bonus. Default: 2.00.")]
    public float FiefGrantLandlessBonus { get; set; } = 2.0f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Concentration Penalty", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "How hard each fief a clan already holds counts against its next claim, as 1/(1 + fiefs x penalty). Vanilla only divides by the VALUE it holds, so a clan sitting on many cheap castles is barely damped. 0.00 = off. Default: 0.35.")]
    public float FiefGrantConcentrationPenalty { get; set; } = 0.35f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Culture Match Bonus", 1.0f, 3.0f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Merit multiplier when the clan's culture matches the settlement's. Vanilla has no equivalent, which is how an orc clan ends up holding an elven city. 1.00 = off. Default: 1.50.")]
    public float FiefGrantCultureMatchBonus { get; set; } = 1.5f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Culture Mismatch Penalty", 0.1f, 1.0f, "#0.00", Order = 6, RequireRestart = false,
        HintText = "Merit multiplier when it does not match. Lower means foreign clans rarely win a settlement of another culture. 1.00 = off. Default: 0.60.")]
    public float FiefGrantCultureMismatchPenalty { get; set; } = 0.6f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("Ruling Clan Factor", 0.1f, 2.0f, "#0.00", Order = 7, RequireRestart = false,
        HintText = "Merit multiplier for the king's own clan, which vanilla already hands a flat +60 on top of its tier and strength. Below 1.00 damps the crown's claim. 1.00 = vanilla. Default: 0.75.")]
    public float FiefGrantRulingClanFactor { get; set; } = 0.75f;

    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("King's Vote Fief Share Cap", 0.0f, 1.0f, "#0.00", Order = 8, RequireRestart = false,
        HintText = "Share of the kingdom's towns and castles above which the ruling clan loses its right to overrule the council. Vanilla lets a king with enough influence override every grant, and his pick is always himself. 1.00 = vanilla. Default: 0.34.")]
    public float FiefGrantKingsVoteFiefShareCap { get; set; } = 0.34f;

    // Renamed from FiefGrantApplyPenaltiesToPlayerClan (default off, i.e. exempt) in #565. MCM keeps
    // a saved value per property, so flipping that default would have reached fresh installs only;
    // a new property name is what reaches every install. The old key in TAOM.json is ignored.
    [SettingPropertyGroup("Kingdom Politics/Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("Exempt Your Clan From Penalties", Order = 9, RequireRestart = false,
        HintText = "On = your clan keeps every bonus but is never damped for the fiefs it already holds or for a culture mismatch. Off = your clan is scored like any other. Replaces the old Apply Penalties To Your Clan switch, which shipped with the exemption on and let a landed player out-score every AI clan for fiefs they never fought for; the switch was renamed so existing installs pick up the new default. The Absent From Siege Factor applies either way. Default: off.")]
    public bool FiefGrantExemptPlayerClanFromPenalties { get; set; } = false;

    // --- Character Preview / Race Framing ---
    //
    // Bannerlord frames and poses every character as if it were human. The per-race position
    // offsets that correct that live in ModuleData/configs/CharacterAvatarPatch.json (the 3D
    // tableau) and CharacterImagePatch.json (the 2D portraits), not here — they are per-race data,
    // not a single slider. Tune them in-game with taom.nudge_race_offset and persist with
    // taom.save_race_offsets.
    //
    // What IS here is the dwarf eye height, because it is one number that applies to one race and
    // players reported the third-person camera floating above a dwarf head. It was a compile-time
    // constant of -0.2f; the default below is deliberately the same value, so exposing it as a
    // slider does not silently retune existing installs. Bounds and default come from
    // EyeHeightAdjustment's consts rather than repeated literals, so the slider cannot drift away
    // from the clamp that enforces it.

    [SettingPropertyGroup("Character Preview/Dwarf Eye Height", GroupOrder = 44)]
    [SettingPropertyBool("Enable Dwarf Eye Height", Order = 0, RequireRestart = false,
        HintText = "Lower a dwarf eye height so the third-person camera sits at the head instead of floating above it. Applies to every dwarf, not just the player, so it also moves the aim origin for dwarf troops. Changing this takes effect the next time a dwarf visual is built (opening a character preview, or the next dwarf spawn), not the instant you flip it.")]
    public bool EnableDwarfEyeHeight { get; set; } = true;

    [SettingPropertyGroup("Character Preview/Dwarf Eye Height")]
    [SettingPropertyFloatingInteger("Dwarf Eye Height Offset",
        HeroRace.EyeHeightAdjustment.MinAdjuster, HeroRace.EyeHeightAdjustment.MaxAdjuster, "#0.00",
        Order = 1, RequireRestart = false,
        HintText = "Metres relative to the human eye height. Negative lowers the camera. Default -0.20. The resulting height is clamped to a safe range, so extreme values cannot put the camera in the floor. Like the toggle above, a change lands the next time a dwarf visual is built.")]
    public float DwarfEyeHeightAdjuster { get; set; } = HeroRace.EyeHeightAdjustment.DefaultAdjuster;

    // --- Supply Lines ---
    //
    // Resupply convoys ordered from a town, castle or friendly lord (#505, port of the yotthani
    // SupplyLines module). Costs and speed are the simulation-relevant knobs; the route arrows are
    // presentation. Defaults match the source module so its author's tuning carries over.

    [SettingPropertyGroup("Supply Lines", GroupOrder = 45)]
    [SettingPropertyBool("Enable Supply Lines", Order = 0, RequireRestart = false,
        HintText = "Order resupply convoys from a town, castle or friendly lord: goods from real market stock, troops from volunteers, delivered to you on the map. When off, the order option disappears and no new orders can be placed; convoys already on the road still arrive.")]
    public bool EnableSupplyLines { get; set; } = true;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyFloatingInteger("Goods Markup Factor", 1.0f, 3.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "Price multiplier on ordered goods over their market value. Default 1.05.")]
    public float SupplyGoodsMarkupFactor { get; set; } = 1.05f;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyFloatingInteger("Transport Fee Per Distance", 0f, 20f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "Delivery fee per map-distance unit between the source and your party. Default 2.")]
    public float SupplyTransportFeePerDistance { get; set; } = 2f;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyFloatingInteger("Mercenary Escort Wage Per Distance", 0f, 50f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "Cost of a mercenary escort per map-distance unit. Default 10.")]
    public float SupplyMercenaryWagePerDistance { get; set; } = 10f;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyInteger("Mercenary Guard Count", 0, 40, "0", Order = 4, RequireRestart = false,
        HintText = "Guards added to the convoy when a mercenary escort is hired. Default 10.")]
    public int SupplyMercenaryGuardCount { get; set; } = 10;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyFloatingInteger("Convoy Hours Per Distance", 0.5f, 10f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "Convoy travel time per map-distance unit. Default 2. Lower is faster delivery.")]
    public float SupplyCaravanHoursPerDistance { get; set; } = 2f;

    [SettingPropertyGroup("Supply Lines")]
    [SettingPropertyBool("Show Convoy Route", Order = 6, RequireRestart = false,
        HintText = "Draw the arrow trail from the source to your party while a convoy is on the road. Presentation only.")]
    public bool SupplyShowRouteVisual { get; set; } = true;

    // --- Field Camps ---
    //
    // Campaign-map camps (#506, port of the yotthani FieldCamp module). Every knob here reads
    // into campaign math (morale, food, ambush odds, placement), so all are coop
    // simulation-relevant. Defaults are the source author's tuning.

    [SettingPropertyGroup("Field Camps", GroupOrder = 46)]
    [SettingPropertyBool("Enable Field Camps", Order = 0, RequireRestart = false,
        HintText = "Pitch camps on the campaign map: field and fortified camps with morale and foraging, terrain-gated ambushes and lookouts. When off, the Make Camp button and camp menus disappear; an existing camp stays until you break it.")]
    public bool EnableFieldCamps { get; set; } = true;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Setup Hours", 0.5f, 24f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "Hours to raise a field camp. Ambushes and lookouts take a quarter of this; a fortified camp double. Default 4.")]
    public float CampSetupHours { get; set; } = 4f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Morale Per Hour", 0f, 5f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "Party morale gained per camped hour once the camp is raised; fortified camps double it. Default 1.")]
    public float CampMoralePerHour { get; set; } = 1f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Forage Rate", 0f, 1f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "Foraging throughput per troop. Grain per hour also scales with terrain, the party's Scouting skill and the square root of troop count. Default 0.10.")]
    public float CampForagePerTroopFactor { get; set; } = 0.1f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Max Ambush Range", 1f, 30f, "#0.0", Order = 4, RequireRestart = false,
        HintText = "Spotting distance inside which an armed ambush can spring on a passing hostile party. Default 10.")]
    public float CampMaxAmbushRange { get; set; } = 10f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Base Ambush Chance", 0f, 1f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "Base chance an eligible passer-by springs the ambush; proximity and Scouting raise it. Default 0.50.")]
    public float CampBaseAmbushChance { get; set; } = 0.5f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Min Town Distance", 0f, 50f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "Keep-out radius from towns and castles for pitching a field or fortified camp. 0 disables the check; ambushes and lookouts are exempt. Default 10.")]
    public float CampMinTownDistance { get; set; } = 10f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyInteger("Fortified Upgrade Cost", 0, 10000, "0", Order = 7, RequireRestart = false,
        HintText = "Gold to fortify a raised field camp. Default 500.")]
    public int CampFortifiedUpgradeCost { get; set; } = 500;

    // Camp work (#860): the men hunt, fell timber or fish from a raised camp. Single-player only, so the three knobs are
    // player-local (CoopSettingsRelevance).

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Work Hours", 1f, 48f, "#0.0", Order = 8, RequireRestart = false,
        HintText = "Hours a camp work (hunt, fell timber, fish) takes once the men are set to it. Default 10.")]
    public float CampActivityHours { get; set; } = 10f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Work Yield Per Ten Men", 0f, 50f, "#0.0", Order = 9, RequireRestart = false,
        HintText = "Goods a finished camp work brings in for every ten healthy men, on the best ground for it; poorer ground brings in less. Default 10.")]
    public float CampActivityYieldPerTenMen { get; set; } = 10f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyInteger("Camp Work Yield Cap", 0, 200, "0", Order = 10, RequireRestart = false,
        HintText = "Most goods one finished camp work can bring in, however many men are sent. Default 30.")]
    public int CampActivityYieldCap { get; set; } = 30;

    // Rest healing and start in camp (#860). Single-player only, so all three are player-local (CoopSettingsRelevance).

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Rest Healing: Troops Per Day", 0f, 50f, "#0.0", Order = 11, RequireRestart = false,
        HintText = "Extra wounded troops healed per day: a base amount that the usual healing percentages raise too. Works only while the camp panel is open (Camp Options, then Manage camp); the work menus do not count (set both to 0 to turn rest healing off, lodging perks included). Default 15.")]
    public float CampRestHealRegularsPerDay { get; set; } = 15f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyFloatingInteger("Camp Rest Healing: Heroes Per Day", 0f, 50f, "#0.0", Order = 12, RequireRestart = false,
        HintText = "Extra hit points per day for wounded heroes: a base amount that the usual healing percentages raise too. Works only while the camp panel is open (Camp Options, then Manage camp); the work menus do not count (set both to 0 to turn rest healing off, lodging perks included). Default 19.")]
    public float CampRestHealHeroesPerDay { get; set; } = 19f;

    [SettingPropertyGroup("Field Camps")]
    [SettingPropertyBool("Start New Campaigns In Camp", Order = 13, RequireRestart = false,
        HintText = "A new campaign begins with a field camp pitched where the party starts, if the ground allows it; otherwise a message says why and the game goes on as usual. Off by default.")]
    public bool CampStartInCamp { get; set; } = false;

    // --- Refuge ---
    //
    // Movable player bases raised from field camps (#507, port of the yotthani Refuge module).
    // Costs, distances, defence bonuses, militia and raids all read into campaign or battle math:
    // every knob is coop simulation-relevant. Defaults are the source author's tuning; raids ship
    // OFF as in the source (experimental).

    [SettingPropertyGroup("Refuge", GroupOrder = 47)]
    [SettingPropertyBool("Enable Refuges", Order = 0, RequireRestart = false,
        HintText = "Raise a field or fortified camp into a persistent, garrisonable base. When off: no founding, no upgrading, and your party is not held in place during a build. Standing refuges persist, builds in progress still finish, and refuges can still be entered, managed and dismantled.")]
    public bool EnableRefuges { get; set; } = true;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyInteger("Found Cost", 0, 50000, "0", Order = 1, RequireRestart = false,
        HintText = "Gold to establish a refuge. Default 2000.")]
    public int RefugeFoundCost { get; set; } = 2000;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyInteger("Stronghold Upgrade Cost", 0, 100000, "0", Order = 2, RequireRestart = false,
        HintText = "Gold to upgrade a refuge into a stronghold. Default 5000.")]
    public int RefugeStrongholdUpgradeCost { get; set; } = 5000;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Build Hours", 1f, 48f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "Hours to raise a refuge after founding; the stronghold rebuild takes the same. Default 6.")]
    public float RefugeBuildHours { get; set; } = 6f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyInteger("Max Refuges (hard cap)", 1, 10, "0", Order = 4, RequireRestart = false,
        HintText = "Ceiling on simultaneous refuges. The live limit is min(1 + clan tier / 2, this cap). Default 3.")]
    public int RefugeMaxCap { get; set; } = 3;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Manage Range", 1f, 15f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "How close your party must be to manage a refuge's garrison and stores. Default 4.")]
    public float RefugeManageRange { get; set; } = 4f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Min Town Distance (found)", 0f, 60f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "Founding keep-out radius from towns and castles. Default 16.")]
    public float RefugeMinTownDistance { get; set; } = 16f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Min Town Distance (stronghold)", 0f, 80f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "Stronghold-upgrade keep-out radius from towns and castles. Default 26.")]
    public float RefugeStrongholdMinTownDistance { get; set; } = 26f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Refuge Defense Bonus", 0f, 0.9f, "#0.00", Order = 8, RequireRestart = false,
        HintText = "Damage reduction for troops defending a refuge, in battle and in auto-resolve. Default 0.20.")]
    public float RefugeDefenseBonus { get; set; } = 0.2f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Stronghold Defense Bonus", 0f, 0.9f, "#0.00", Order = 9, RequireRestart = false,
        HintText = "Damage reduction for troops defending a stronghold. Default 0.35.")]
    public float RefugeStrongholdDefenseBonus { get; set; } = 0.35f;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyInteger("Militia Base", 0, 50, "0", Order = 10, RequireRestart = false,
        HintText = "Base temporary militia rallying to a refuge under attack; age of the refuge and garrison size add more. Default 6.")]
    public int RefugeMilitiaBase { get; set; } = 6;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyInteger("Militia Max", 0, 100, "0", Order = 11, RequireRestart = false,
        HintText = "Ceiling on the temporary defence militia. Default 40.")]
    public int RefugeMilitiaMax { get; set; } = 40;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyBool("Enable Enemy Raids", Order = 12, RequireRestart = false,
        HintText = "Experimental: hostile parties can attack your refuges (auto-resolved when you are away). Off by default.")]
    public bool RefugeEnableRaids { get; set; } = false;

    [SettingPropertyGroup("Refuge")]
    [SettingPropertyFloatingInteger("Raid Range", 1f, 20f, "#0.0", Order = 13, RequireRestart = false,
        HintText = "Search radius around a refuge for raid candidates when raids are enabled. Default 6.")]
    public float RefugeRaidRange { get; set; } = 6f;

    // --- Map Tools / Distance Cache Rebuild ---
    //
    // Rebuilds Modules/TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin
    // from the live campaign's map scene. The vanilla editor's ComputeAndSave button does the
    // same thing but takes ~108 hours on TAOM's 863-settlement map. Our parallel + smoke-test +
    // checkpoint pipeline brings that to ~30 min (full) or ~30s (incremental, 1-5 settlements
    // moved). Output file replaces the live cache; previous file is preserved as ".prev".
    // Reload the save (or start a new campaign) after the rebuild completes to pick up the
    // new distances.

    // --- Player Switcher (#514) ---

    [SettingPropertyGroup("Player Switcher")]
    [SettingPropertyBool("Enable Player Switcher", Order = 0, RequireRestart = false,
        HintText = "Adds a panel to the character creation face generator listing the existing lords of your chosen culture. Pick one and you play the campaign as that lord, with their face, gear, skills, clan, fiefs and kingdom. The character you build is set aside. Off means the panel never loads and character creation is untouched. When the Themed Faction & Hero Picker (Menus & Loading Screens) is shown, it takes this panel's place for that character creation, and a lord picked there is taken over through this feature, so with this off that picker copies the lord's look, gear and skills onto your own character instead.")]
    public bool EnablePlayerSwitcher { get; set; } = true;

    [SettingPropertyGroup("Player Switcher")]
    [SettingPropertyBool("Include Wanderers", Order = 1, RequireRestart = false,
        HintText = "Also lists the wanderers of your culture. A wanderer has no clan, so instead of taking over a house they are adopted into the clan you named, with the banner you designed, and you lead it. Only 20 of 39 cultures have any wanderers.")]
    public bool PlayerSwitcherIncludeWanderers { get; set; } = true;

    [SettingPropertyGroup("Player Switcher")]
    [SettingPropertyBool("Allow Sauron and the Nazgul", Order = 2, RequireRestart = false,
        HintText = "Offers the lore-locked dark lords as playable, here and on the Themed Faction & Hero Picker. Off by default for a reason: TAOM's capture immunity defers to vanilla for the player character, so a player-controlled Sauron or Witch-king CAN be captured and ransomed like any other lord. Turning this on knowingly trades that lore promise for the option.")]
    public bool PlayerSwitcherAllowLoreLockedHeroes { get; set; } = false;

    [SettingPropertyGroup("Player Switcher")]
    [SettingPropertyBool("Carry Over Starting Gold", Order = 3, RequireRestart = false,
        HintText = "When you take over a lord, adds the starting gold your created character was given on top of the lord's own treasury. An adopted wanderer starts with a new character's gold either way, and an Advanced Start type other than the default sets its own gold. Off by default: an established lord is already funded, so stacking a startup package on them is a balance change rather than a fix.")]
    public bool PlayerSwitcherTransferStartingGold { get; set; } = false;

    // --- Menus & Loading Screens (#704, Kysaro's themed front end) ---

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Themed Main Menu", Order = 0, RequireRestart = false,
        HintText = "Kysaro's Middle-earth main menu in place of the vanilla one. Takes effect the next time the main menu opens.")]
    public bool FrontEndCustomMainMenu { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Hide Game Version", Order = 1, RequireRestart = false,
        HintText = "Hides the Bannerlord version number in the corner of the main menu. The TAOM log still records it. Takes effect after a restart: the game builds the version line once, at startup.")]
    public bool FrontEndHideGameVersion { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Main Menu Video", Order = 2, RequireRestart = false,
        HintText = "Plays the Sauron video behind the main menu in place of the vanilla background video. Takes effect the next time the main menu opens.")]
    public bool FrontEndMenuVideo { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Mute Menu Music Under The Video", Order = 3, RequireRestart = false,
        HintText = "Keeps the vanilla menu theme off the main menu while the menu video plays its own soundtrack, so it needs Main Menu Video on. Vanilla's music returns once you leave the main menu. Takes effect the next time the main menu opens.")]
    public bool FrontEndMuteMenuMusic { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Themed Loading Screens", Order = 4, RequireRestart = false,
        HintText = "Shows Kysaro's Middle-earth paintings on loading screens, never the same one twice in a row. Only the picture last shown is kept in memory. Takes effect on the next loading screen.")]
    public bool FrontEndCustomLoadingImages { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens")]
    [SettingPropertyBool("Themed Skill Icons", Order = 5, RequireRestart = false,
        HintText = "Kysaro's skill icons in place of the vanilla ones, everywhere in the game. Turning them on applies the next time the main menu opens; turning them off takes effect after a restart, because the vanilla icons are not kept once replaced.")]
    public bool FrontEndSkillIcons { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Faction & Hero Picker", Order = 0, RequireRestart = false,
        HintText = "Kysaro's faction screen with painted portraits, a map and a hero picker, in place of TAOM's faction map. Picking one of that faction's lords or ladies makes you that character, with their clan, fiefs and kingdom: character creation goes straight to the career choice and then into the game (Player Switcher's settings apply). Picking a legend or any other character starts you with its look, gear and skills. When it is shown it takes the place of Player Switcher's panel for that character creation. Takes effect the next time character creation reaches the culture screen.")]
    public bool FrontEndFactionScreen { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Face Generator", Order = 1, RequireRestart = false,
        HintText = "Kysaro's reskin of the character creation face generator. Outside character creation the face generator stays vanilla. Takes effect the next time it opens.")]
    public bool FrontEndCustomFaceGen { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Backstory Screens", Order = 2, RequireRestart = false,
        HintText = "Kysaro's reskin of the family, childhood, adolescence, youth, adulthood and age screens. Takes effect the next time one of them opens.")]
    public bool FrontEndCustomNarrativeStage { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Review Screen", Order = 3, RequireRestart = false,
        HintText = "Kysaro's reskin of the final review screen, where you name your character. Takes effect the next time it opens.")]
    public bool FrontEndCustomReviewStage { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Banner Editor", Order = 4, RequireRestart = false,
        HintText = "Kysaro's reskin of the banner editor during character creation. The banner editor in a running campaign stays vanilla. Takes effect the next time it opens.")]
    public bool FrontEndCustomBannerEditor { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Clan Naming Screen", Order = 5, RequireRestart = false,
        HintText = "Kysaro's reskin of the clan naming screen. Takes effect the next time it opens.")]
    public bool FrontEndCustomClanNaming { get; set; } = true;

    [SettingPropertyGroup("Menus & Loading Screens/Character Creation")]
    [SettingPropertyBool("Themed Options Screen", Order = 6, RequireRestart = false,
        HintText = "Kysaro's reskin of the difficulty and campaign options screen at the end of character creation. Takes effect the next time it opens.")]
    public bool FrontEndCustomOptionsStage { get; set; } = true;

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

    // --- Creature Bandits (#692): the riderless creatures' own numbers, apart from the ridden spider's. Defaults are
    // CreatureBanditTuning's constants; CreatureBanditTuning reads these live and clamps them. Read at the
    // creature's spawn (hit points), its tree build (cooldowns), each attack (targets, damage, knockdown) and each hit it
    // takes (the damage-taken percents, CreatureBanditTuning.CurrentTakenFactor, #746). To change
    // a default later, rename the property: MCM json2 keeps a saved value (orientation.md "Persisted MCM defaults").
    // A top-level group: under "Combat Mechanics" its master switch would promise to make these inert, and none of
    // these reads fold it. ---

    [SettingPropertyGroup("Creature Bandits", GroupOrder = 53)]
    [SettingPropertyBool("Spawn Spider Broods", Order = 0, RequireRestart = false,
        HintText = "Whether spider broods keep spawning around Mirkwood and Dol Guldur on the campaign map (one a day, up to twenty). Off stops new broods; broods already on the map stay. Broods exist only in a campaign started with v2.0.32 or later.")]
    public bool CreatureBanditSpawnBroods { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnBroods;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyBool("Spawn Troll Bands", Order = 1, RequireRestart = false,
        HintText = "Whether wild troll bands keep spawning on the campaign map, about one per kingdom near its towns, castles and villages (one new band a day, two to four trolls each). Trolls are never taken prisoner or recruited. Off stops new bands; bands already on the map stay. Troll bands need a new campaign. The options below tune the spiders only.")]
    public bool CreatureBanditSpawnTrollBands { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnTrollBands;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyFloatingInteger("Creature Hit Points", 50f, 1000f, "0", Order = 2, RequireRestart = false,
        HintText = "Hit points of a riderless creature bandit (the giant spiders of the Mirkwood broods). The ridden spider mount keeps its own. Applies to creatures spawned after the change.")]
    public float CreatureBanditHitPoints { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultHitPoints;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Bite: Max Soldiers Hit", 1, 10, Order = 3, RequireRestart = false,
        HintText = "How many soldiers the standing front bite can strike at once (the nearest ones in its arc).")]
    public int CreatureBanditBiteTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultBiteTargets;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Pounce: Max Soldiers Hit", 1, 10, Order = 4, RequireRestart = false,
        HintText = "How many soldiers the running pounce can strike at once.")]
    public int CreatureBanditPounceTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultPounceTargets;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Swipe: Max Soldiers Hit", 1, 10, Order = 5, RequireRestart = false,
        HintText = "How many soldiers a left or right swipe can strike at once.")]
    public int CreatureBanditSwipeTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultSwipeTargets;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Bite Damage %", 0, 300, Order = 6, RequireRestart = false,
        HintText = "Bite damage as a percentage of the spider's base bite (100 = the ridden spider's damage).")]
    public int CreatureBanditBiteDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultBiteDamagePercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Pounce Damage %", 0, 300, Order = 7, RequireRestart = false,
        HintText = "Pounce damage as a percentage of the spider's base bite.")]
    public int CreatureBanditPounceDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultPounceDamagePercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Swipe Damage %", 0, 300, Order = 8, RequireRestart = false,
        HintText = "Swipe damage as a percentage of the spider's base bite. Low by default: the swipe reaches several soldiers.")]
    public int CreatureBanditSwipeDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultSwipeDamagePercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyBool("Only Crits Knock Down", Order = 9, RequireRestart = false,
        HintText = "When on, a creature's normal hit only staggers a soldier and only a critical hit knocks him down. When off, any hit of 30 or more knocks down, as the ridden spider's do.")]
    public bool CreatureBanditKnockdownOnCritOnly { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultKnockdownOnCritOnly;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyFloatingInteger("Pounce Cooldown (s)", 0.5f, 30f, "0.0", Order = 10, RequireRestart = false,
        HintText = "Seconds between a creature's pounces or bites. Applies to creatures spawned after the change.")]
    public float CreatureBanditPounceCooldownSeconds { get; set; } = (float)TAOM.Features.Spider.SpiderConfig.PounceCooldownSeconds;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyFloatingInteger("Swipe Cooldown (s)", 0.5f, 30f, "0.0", Order = 11, RequireRestart = false,
        HintText = "Seconds between a creature's swipes. Applies to creatures spawned after the change.")]
    public float CreatureBanditSwipeCooldownSeconds { get; set; } = (float)TAOM.Features.Spider.SpiderConfig.SideAttackCooldownSeconds;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Missile Damage Taken %", 0, 200, Order = 12, RequireRestart = false,
        HintText = "Share of damage a creature takes from arrows, bolts, javelins and thrown weapons (100 = normal). Applies at once.")]
    public int CreatureBanditMissileTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMissileTakenPercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Cut Damage Taken %", 0, 200, Order = 13, RequireRestart = false,
        HintText = "Share of damage a creature takes from cutting melee blows: swords, axes (100 = normal).")]
    public int CreatureBanditCutTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Pierce Damage Taken %", 0, 200, Order = 14, RequireRestart = false,
        HintText = "Share of damage a creature takes from piercing melee blows: spears, thrusts (100 = normal).")]
    public int CreatureBanditPierceTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    [SettingPropertyGroup("Creature Bandits")]
    [SettingPropertyInteger("Blunt Damage Taken %", 0, 200, Order = 15, RequireRestart = false,
        HintText = "Share of damage a creature takes from blunt melee blows: maces, hammers, charges (100 = normal).")]
    public int CreatureBanditBluntTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    // --- Realm Borders (#698) ---
    // Client-side presentation only: listed under Presentation in CoopSettingsRelevance. The keys (toggle
    // and map mode) are native rebindable game keys under Options > Keybindings > Campaign Map, not MCM.

    [SettingPropertyGroup("Realm Borders", GroupOrder = 54)]
    [SettingPropertyBool("Show Realm Borders", Order = 0, RequireRestart = false,
        HintText = "Draws each kingdom's border on the campaign map, following rivers, coasts and mountain ranges, and redraws it when a fief changes hands. While this is on, the Show Realm Borders key (M by default) hides and shows them, and the map mode key (G) switches what they show.")]
    public bool EnableRealmBorders { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Heraldic Bands Instead of Atlas Look", Order = 1, RequireRestart = false,
        HintText = "Off: the Atlas look, a watercolour wash inside each border with a dash-dot ink line. On: two solid bands in each realm's colour with a gap on the line.")]
    public bool RealmBordersHeraldicBands { get; set; } = false;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Gold Cord on Your Realm", Order = 2, RequireRestart = false,
        HintText = "Draws your own kingdom's frontier as a gold cord, so you can find it at a glance.")]
    public bool RealmBordersGildPlayerRealm { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyFloatingInteger("Border Width", 0.5f, 3f, "0.00", Order = 3, RequireRestart = false,
        HintText = "Multiplies the width of every border. Default: 1.")]
    public float RealmBordersWidthScale { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultWidthScale;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyFloatingInteger("Fade In From Camera Distance", 0f, 500f, "0", Order = 4, RequireRestart = false,
        HintText = "Camera distance at which the borders start to appear; closer than this they are hidden. Must be below Full Opacity Distance, or both revert to their defaults. Default: 45.")]
    public float RealmBordersFadeStartDistance { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFadeStartDistance;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyFloatingInteger("Full Opacity Distance", 0f, 500f, "0", Order = 5, RequireRestart = false,
        HintText = "Camera distance at which the borders are fully drawn. Default: 110.")]
    public float RealmBordersFullOpacityDistance { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFullOpacityDistance;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Draw Borders Through Hills", Order = 6, RequireRestart = false,
        HintText = "On: borders always show, drawn over hills and map figures. Off: borders lie on the ground and hide behind ridges.")]
    public bool RealmBordersDrawThroughTerrain { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Realm Names", Order = 7, RequireRestart = false,
        HintText = "Letters each kingdom's name across its land when the camera is zoomed out.")]
    public bool RealmBordersNames { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Border-Crossing Notices", Order = 8, RequireRestart = false,
        HintText = "Tells you when your party rides into another kingdom's lands.")]
    public bool RealmBordersCrossingNotices { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Colour Realm Lands", Order = 9, RequireRestart = false,
        HintText = "Tints each kingdom's land in its colour between the borders, like a map in an atlas.")]
    public bool RealmBordersFillLands { get; set; } = true;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyFloatingInteger("Realm Colour Strength", 0.05f, 0.8f, "0.00", Order = 10, RequireRestart = false,
        HintText = "How strongly the land is tinted: 0.05 is a faint wash, 0.8 nearly paints over the map. Default: 0.5.")]
    public float RealmBordersFillStrength { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFillStrength;

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyBool("Parchment Map at Full Zoom-Out", Order = 11, RequireRestart = false,
        HintText = "As you zoom all the way out, a parchment map of Middle-earth fades in over the campaign map, with the borders, realm colours and names drawn on it. It shows only while the borders do.")]
    public bool RealmBordersParchmentMap { get; set; } = true;

    // Dropdowns persist by index: never reorder either list (RealmBordersProviderTests pins both).
    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyDropdown("Border Blend Mode", Order = 12, RequireRestart = false,
        HintText = "Advanced: how the borders mix with the map beneath. 'Material default' keeps the engine material's own. If the borders glow and their dark ink line is missing, try Modulate (Factor turns blending off).")]
    public Dropdown<string> RealmBordersBlendMode { get; set; } = new Dropdown<string>(
        TAOM.Features.RealmBorders.RealmBordersSettingsProvider.BlendModeChoices, 0);

    [SettingPropertyGroup("Realm Borders")]
    [SettingPropertyDropdown("Border Material", Order = 13, RequireRestart = false,
        HintText = "Advanced: the engine material the borders are drawn from. 'Automatic' tries vertex_color_blend_after_postfx_mat first, which blends normally so the dark ink line shows, then the others in list order. Every choice draws over the parchment map.")]
    public Dropdown<string> RealmBordersMaterial { get; set; } = new Dropdown<string>(
        TAOM.Features.RealmBorders.RealmBordersSettingsProvider.MaterialChoices, 0);

    // --- Realm Borders: one colour per realm; blank keeps the palette's (ModuleData/realm_borders/palette.json) ---

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Gondor", Order = 0, RequireRestart = false,
        HintText = "Gondor's colour on the map, written #RRGGBB. Blank uses the default, #3F76B8.")]
    public string RealmColourGondor { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Rohan", Order = 1, RequireRestart = false,
        HintText = "Rohan's colour on the map, written #RRGGBB. Blank uses the default, #206803.")]
    public string RealmColourRohan { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Dunland", Order = 2, RequireRestart = false,
        HintText = "Dunland's colour on the map, written #RRGGBB. Blank uses the default, #7A4A28.")]
    public string RealmColourDunland { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Isengard", Order = 3, RequireRestart = false,
        HintText = "Isengard's colour on the map, written #RRGGBB. Blank uses the default, #3D3D40.")]
    public string RealmColourIsengard { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Mordor", Order = 4, RequireRestart = false,
        HintText = "Mordor's colour on the map, written #RRGGBB. Blank uses the default, #9E1A12.")]
    public string RealmColourMordor { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Harad", Order = 5, RequireRestart = false,
        HintText = "Harad's colour on the map, written #RRGGBB. Blank uses the default, #DF2100.")]
    public string RealmColourHarad { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Umbar", Order = 6, RequireRestart = false,
        HintText = "Umbar's colour on the map, written #RRGGBB. Blank uses the default, #2F3C58.")]
    public string RealmColourUmbar { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Shaghâna", Order = 7, RequireRestart = false,
        HintText = "Shaghâna's colour on the map, written #RRGGBB. Blank uses the default, #DC7FA8.")]
    public string RealmColourShaghana { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Âbanissa", Order = 8, RequireRestart = false,
        HintText = "Âbanissa's colour on the map, written #RRGGBB. Blank uses the default, #2A8C8C.")]
    public string RealmColourAbanissa { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Khand", Order = 9, RequireRestart = false,
        HintText = "Khand's colour on the map, written #RRGGBB. Blank uses the default, #E8841E.")]
    public string RealmColourKhand { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Rhûn", Order = 10, RequireRestart = false,
        HintText = "Rhûn's colour on the map, written #RRGGBB. Blank uses the default, #8A1236.")]
    public string RealmColourRhun { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Dale", Order = 11, RequireRestart = false,
        HintText = "Dale's colour on the map, written #RRGGBB. Blank uses the default, #43B4DC.")]
    public string RealmColourDale { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Erebor", Order = 12, RequireRestart = false,
        HintText = "Erebor's colour on the map, written #RRGGBB. Blank uses the default, #2A4DA8.")]
    public string RealmColourErebor { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Rivendell", Order = 13, RequireRestart = false,
        HintText = "Rivendell's colour on the map, written #RRGGBB. Blank uses the default, #530CAA.")]
    public string RealmColourRivendell { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Lothlórien", Order = 14, RequireRestart = false,
        HintText = "Lothlórien's colour on the map, written #RRGGBB. Blank uses the default, #E6E27A.")]
    public string RealmColourLothlorien { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Mirkwood", Order = 15, RequireRestart = false,
        HintText = "Mirkwood's colour on the map, written #RRGGBB. Blank uses the default, #185C2A.")]
    public string RealmColourMirkwood { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Lindon", Order = 16, RequireRestart = false,
        HintText = "Lindon's colour on the map, written #RRGGBB. Blank uses the default, #4FC4A0.")]
    public string RealmColourLindon { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Dol Guldur", Order = 17, RequireRestart = false,
        HintText = "Dol Guldur's colour on the map, written #RRGGBB. Blank uses the default, #4E2E6E.")]
    public string RealmColourDolGuldur { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Gundabad", Order = 18, RequireRestart = false,
        HintText = "Gundabad's colour on the map, written #RRGGBB. Blank uses the default, #5A6367.")]
    public string RealmColourGundabad { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Misty Mountain Orcs", Order = 19, RequireRestart = false,
        HintText = "Misty Mountain Orcs's colour on the map, written #RRGGBB. Blank uses the default, #7E7A50.")]
    public string RealmColourMistyMountainOrcs { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Goblins", Order = 20, RequireRestart = false,
        HintText = "Goblins's colour on the map, written #RRGGBB. Blank uses the default, #AFC93A.")]
    public string RealmColourGoblins { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Goblins of Blue Craig", Order = 21, RequireRestart = false,
        HintText = "Goblins of Blue Craig's colour on the map, written #RRGGBB. Blank uses the default, #6A8AA8.")]
    public string RealmColourBlueCraig { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Arthedain", Order = 22, RequireRestart = false,
        HintText = "Arthedain's colour on the map, written #RRGGBB. Blank uses the default, #A0B4D8.")]
    public string RealmColourArthedain { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Cardolan", Order = 23, RequireRestart = false,
        HintText = "Cardolan's colour on the map, written #RRGGBB. Blank uses the default, #28745C.")]
    public string RealmColourCardolan { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Rhudaur", Order = 24, RequireRestart = false,
        HintText = "Rhudaur's colour on the map, written #RRGGBB. Blank uses the default, #E0735A.")]
    public string RealmColourRhudaur { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Ered Luin", Order = 25, RequireRestart = false,
        HintText = "Ered Luin's colour on the map, written #RRGGBB. Blank uses the default, #7B68EE.")]
    public string RealmColourEredLuin { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Anduin Vale", Order = 26, RequireRestart = false,
        HintText = "Anduin Vale's colour on the map, written #RRGGBB. Blank uses the default, #654D37.")]
    public string RealmColourAnduinVale { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Angáladh", Order = 27, RequireRestart = false,
        HintText = "Angáladh's colour on the map, written #RRGGBB. Blank uses the default, #587A4A.")]
    public string RealmColourAngaladh { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Dorwinion", Order = 28, RequireRestart = false,
        HintText = "Dorwinion's colour on the map, written #RRGGBB. Blank uses the default, #722721.")]
    public string RealmColourDorwinion { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("South Rhovanion", Order = 29, RequireRestart = false,
        HintText = "South Rhovanion's colour on the map, written #RRGGBB. Blank uses the default, #4B3F0F.")]
    public string RealmColourSouthRhovanion { get; set; } = string.Empty;

    [SettingPropertyGroup("Realm Borders/Realm Colours")]
    [SettingPropertyText("Your Realm", Order = 30, RequireRestart = false,
        HintText = "The colour of your realm when it is none of the kingdoms above: your clan's land while it serves no kingdom, then a kingdom you found. Written #RRGGBB. Blank gives it a free colour. In one of the kingdoms above, that kingdom's own field applies.")]
    public string RealmColourYourRealm { get; set; } = string.Empty;

    // --- Animation Variety (orc and goblin gait, docs/features/animation-variety.md) ---

    [SettingPropertyGroup("Animation Variety", GroupOrder = 9)]
    [SettingPropertyBool("Random step size per soldier", Order = 0, RequireRestart = false,
        HintText = "Orc and goblin warriors in a battle line no longer walk in lockstep: each soldier's step size is scaled by a random factor between the minimum below and 1.0, fixed for that soldier. Applied when a soldier spawns, so it takes effect in the next battle.")]
    public bool EnableRandomStepSize { get; set; } = true;

    [SettingPropertyGroup("Animation Variety")]
    [SettingPropertyFloatingInteger("Step size minimum factor", 0.80f, 1.00f, "0.00", Order = 1, RequireRestart = false,
        HintText = "Each soldier's step size is vanilla's step size times a random factor between this value and 1.00. Vanilla's own step size is 0.80 at Athletics 0 and rises to 1.00 at Athletics 300, so the factor is not the final step size: at 0.92 an Athletics 0 soldier ends at 0.74 to 0.80 and an Athletics 300 soldier at 0.92 to 1.00, and at 0.80 the lowest step size is 0.64. 1.00 turns the spread off. A value outside 0.80 to 1.00 falls back to 0.92.")]
    public float GaitStepSizeMinFactor { get; set; } = 0.92f;

    [SettingPropertyGroup("Animation Variety")]
    [SettingPropertyBool("Random gait set per soldier", Order = 2, RequireRestart = false,
        HintText = "Each orc and goblin warrior walks with one of several gait sets, picked at random and fixed for that soldier. A gait set that is not installed is skipped, so with none installed this does nothing. Applied when a soldier spawns, so it takes effect in the next battle.")]
    public bool EnableRandomGaitSet { get; set; } = true;

    // On by default for the swing tests (Mike, 2026-10-18). MCM keeps each value in TAOM.json, so turning it
    // off for players later means renaming this setting, never flipping the default (orientation.md
    // "Persisted MCM defaults").
    [SettingPropertyGroup("Animation Variety")]
    [SettingPropertyBool("Swing alternative probe (diagnostic)", Order = 3, RequireRestart = false,
        HintText = "Tests alternative swing clips: when a mission has act_taom_ swing codes loaded, it counts which clip each swing plays and where in the clip a hit lands, and only logs; it changes nothing in the game. While such codes exist it costs a per-frame sweep of every person in every mission, towns included; with none loaded it writes one log line per mission. Read at the start of each mission.")]
    public bool EnableSwingAlternativeProbe { get; set; } = true;

    // --- World / Watchful Holds ---
    // Both change what the campaign simulates (who is slowed, who loses men), so neither is listed in
    // CoopSettingsRelevance. Both are new, so a fresh TAOM.json carries the defaults.

    [SettingPropertyGroup("World/Watchful Holds", GroupOrder = 58)]
    [SettingPropertyBool("Enable Watchful Holds", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), a castle or town whose realm is at war with yours harasses a lord party that marches within a few map units of its walls, if the fort is not besieged and holds enough troops (30 by default). Your party is slowed (disorganized: it moves slower, and its troops start a battle with lower morale until it wears off) and loses a few men, some food and morale. An army you lead loses cohesion, but never below 10, and each party marching with you takes its own losses. When off, forts never harass anyone. Applies at once, no restart.")]
    public bool EnableWatchfulHolds { get; set; } = true;

    [SettingPropertyGroup("World/Watchful Holds")]
    [SettingPropertyBool("Harass AI lords", Order = 1, RequireRestart = false,
        HintText = "When enabled (default), AI lords marching past an enemy fort are slowed (disorganized) too, at a lower chance than yours, and lose nothing else. Turn it off to protect the campaign pace: the effect then stays on your own party and the army you lead (AI lords marching with your army still take their share). Does nothing while Enable Watchful Holds is off.")]
    public bool WatchfulHoldsHarassAiLords { get; set; } = true;

    // --- Fire and Tribute (raiding hosts, docs/features/fire-and-tribute.md, #870) ---
    //
    // The switches are read live on every hourly tick while a host rides, so RequireRestart = false. GroupOrder 66 is
    // unused; it is the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one).
    // A host alive when its switch goes off is sent away at its next hourly tick, or once its battle ends if it is fighting.

    [SettingPropertyGroup("World/Fire and Tribute", GroupOrder = 66)]
    [SettingPropertyBool("Enable Fire and Tribute", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), raiding hosts ride once the War of the Ring is in Full War, or from day 89 when the War of the Ring is switched off. They ride out of their homelands to burn the villages of the opposed side: Orcs of Gundabad, Easterlings of Rhûn, Haradrim, Dunlendings and the Blacklocks of Erebor against the Free Peoples; outlaw bands of Gondor and Mirkwood against the Shadow; Corsairs of Umbar against either. A host raids the villages of the strongest enemy realm, fights weaker parties of that realm it meets, and goes home after its quota. When off, no host rides and a host already out is sent away at its next hourly tick; one still fighting is sent away once its battle ends. Single player only. Applies at once, no restart.")]
    public bool EnableFireAndTribute { get; set; } = true;

    [SettingPropertyGroup("World/Fire and Tribute")]
    [SettingPropertyBool("Free Peoples Hosts", Order = 1, RequireRestart = false,
        HintText = "When enabled (default), the outlaw bands of Gondor and Mirkwood may ride against the Shadow's realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeFreeHosts { get; set; } = true;

    [SettingPropertyGroup("World/Fire and Tribute")]
    [SettingPropertyBool("Neutral Hosts", Order = 2, RequireRestart = false,
        HintText = "When enabled (default), the Corsair Host of Umbar may ride against Free or Shadow realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeNeutralHosts { get; set; } = true;

    [SettingPropertyGroup("World/Fire and Tribute")]
    [SettingPropertyBool("Shadow Hosts", Order = 3, RequireRestart = false,
        HintText = "When enabled (default), the hosts of Gundabad, Rhûn, Harad and Dunland, and the Blacklocks of Erebor, may ride against the Free Peoples' realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeEvilHosts { get; set; } = true;

    [SettingPropertyGroup("World/Fire and Tribute")]
    [SettingPropertyBool("Tribute Envoys", Order = 4, RequireRestart = false,
        HintText = "When enabled (default), a host that rides against your kingdom sends an envoy a day or two later, if your clan owns villages and you are not enlisted. The envoy asks for gold, at a price that grows with your villages and with each envoy you have seized. Pay and the host raids none of your clan's villages and never attacks your party; haggle once (a Charm roll) when the tribute is large; ask for time; refuse; or seize the envoy, after which that side's next host rides against your kingdom first. Shown only while the map is free. When off, no envoy is scheduled or shown. Single player only. Applies at once, no restart.")]
    public bool FireAndTributeEnvoys { get; set; } = true;

    // --- Help Unlooked-For (docs/features/unlooked-for-aid.md, #872) ---
    //
    // Read live on the AI worker thread and the main thread, once per avoid question, through the settings object
    // the provider captured on the main thread when the model was built; RequireRestart = false. GroupOrder 68 is
    // unused; it is the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("World/Help Unlooked-For", GroupOrder = 68)]
    [SettingPropertyBool("Lords stop fleeing sieges", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), an AI lord stops running from an army that besieges a settlement when the lord, with its army if it leads one or is attached to it, is at least half as strong as the entire siege camp (0.5 by default, unlooked_for_aid.json). Lords can then pass a siege or gather near it. A lord shut inside a besieged settlement is never fled from, whatever the strengths. A weaker lord still flees, so it is not caught when the siege lifts beside it. At a siege you lead, AI lords keep the base game behaviour. Caravans, villagers, bandits, patrols and garrisons are unchanged. When off, lords flee exactly as in the base game. Applies at once, no restart.")]
    public bool EnableUnlookedForAid { get; set; } = true;

    // --- The Red Arrow (docs/features/red-arrow.md, #873) ---
    //
    // Read live from the Army Management screen (main thread), so RequireRestart = false. GroupOrder 76 is unused; it is
    // the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one). The group is meant to hold the other army features too.

    [SettingPropertyGroup("War of the Ring/Armies", GroupOrder = 76)]
    [SettingPropertyBool("Call armies during your siege", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), the Army Management screen works during a siege you lead as the besieger: you can call parties into your army and release them while the siege goes on, at the usual influence cost. The base game's other limits still apply (a party that is fighting, too weak, disbanding, too far away or at sea is refused, and so is a party already at the siege outside your army). Defending a siege, having no army and being in a map event keep the base game block. Singleplayer only: in a co-op session this does nothing. When off, the block is exactly the base game's. Takes effect the next time the screen opens, no restart.")]
    public bool EnableRedArrow { get; set; } = true;

    // --- The King's Muster (docs/features/kings-muster.md, #874) ---
    //
    // One switch, read live on the AI's hourly army think (main thread), through the settings object the provider took when the
    // army-management model was built, so RequireRestart = false. It joins the "War of the Ring/Armies" group above, which
    // already carries its GroupOrder on EnableRedArrow (MCM ignores a GroupOrder on any later property).

    [SettingPropertyGroup("War of the Ring/Armies")]
    [SettingPropertyBool("Rulers call hosts for less influence", Order = 1, RequireRestart = false,
        HintText = "When enabled (default), an AI kingdom ruler, Good or Evil, pays half the influence (0.5 by default, RulerCallCostFactor in army_targeting.json) for each party called into an army, after culture feats. Cheaper calls also make the influence an army spends on keeping itself together cheaper for the ruler, so those hosts hold longer and the ruler's clan keeps more influence for votes. Your own calls, as a ruler or a vassal, cost what they always did. Singleplayer only: in a co-op session this does nothing. When off, the cost is what it was before this feature (culture feats still apply). Applies at once, no restart.")]
    public bool EnableKingsMusterDiscount { get; set; } = true;

    // --- Bonds of Battle (relations earned on campaign, docs/features/bonds-of-battle.md) ---
    //
    // Every value is read live where it is used (at a battle's end, a troop hand-over, a barter), so nothing is applied once
    // at start-up and RequireRestart = false. GroupOrder 59 is unused; it is the group's first
    // property, so it carries the order (MCM ignores a GroupOrder on any later one). Defaults live in
    // BondsOfBattleSettingsProvider so the fallback before MCM is up cannot drift from them.

    [SettingPropertyGroup("Bonds of Battle", GroupOrder = 59)]
    [SettingPropertyBool("Enable Bonds of Battle", Order = 0, RequireRestart = false,
        HintText = "Relation with lords and notables is earned on campaign: fighting beside a lord, clearing bandits near a settlement, handing a lord troops, and overpaying in a barter. Off restores vanilla on all four. Does nothing while you are enlisted. Default: on.")]
    public bool BondsOfBattleEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyInteger("Relation With Allied Lords", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinAllyBonus, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxAllyBonus, Order = 1, RequireRestart = false,
        HintText = "Relation gained with each lord who fought on your side in a battle, once per lord per battle (once per clan when Personal Relations is off, because a clan then shares one relation). Your own clan, an army you lead and lords leading no party earn nothing; after a win, the side leader you came to help is thanked by vanilla's own conversation instead, but only when vanilla pays him something (a small contribution earns him this bonus). 0 = none. Default: 1.")]
    public int BondsAllyBonus { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultAllyBonus;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyBool("Lose Relation With Enemy Lords", Order = 2, RequireRestart = false,
        HintText = "Each lord who fought against you in a battle thinks less of you, once per lord per battle (once per clan when Personal Relations is off, because a clan then shares one relation). Bandits are never counted. Default: off.")]
    public bool BondsEnemyPenaltyEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnemyPenaltyEnabled;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyInteger("Relation Lost With Enemy Lords", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinEnemyPenalty, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxEnemyPenalty, Order = 3, RequireRestart = false,
        HintText = "How much relation each enemy lord loses when the option above is on. 0 = none. Default: 5.")]
    public int BondsEnemyPenalty { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnemyPenalty;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyBool("Notables Thank You For Clearing Bandits", Order = 4, RequireRestart = false,
        HintText = "In the field, against bandits alone (cleared hideouts are thanked by vanilla): after you win, one random notable in each settlement near the battle gains relation with you, one point for every ten bandits killed, wounded or routed, at least 1 and at most 10. A village in the other side's land (Free against Evil) does not thank you. Default: on.")]
    public bool BondsBanditGratitudeEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultBanditGratitudeEnabled;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyFloatingInteger("Bandit Gratitude Distance", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinBanditGratitudeRadius, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxBanditGratitudeRadius, "#0", Order = 5, RequireRestart = false,
        HintText = "How far from the battle, in map units, a settlement still thanks you. A settlement is usually about 17 units from its nearest neighbour. Default: 25.")]
    public float BondsBanditGratitudeRadius { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultBanditGratitudeRadius;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyBool("Troop Gifts Win Lords' Favour", Order = 6, RequireRestart = false,
        HintText = "When you hand troops to another lord on the Donate Troops screen, that lord gains relation with you: half of the influence the gift is worth, at most 10, give or take one. A gift worth less than 2 earns nothing. Hand-overs stop paying a lord once 10 relation has landed with him that day; the Done that reaches 10 asks only for what is left, and Charm can raise it. A lord gains at most 10 a day from hand-overs, however the gift is split (Charm can raise the last Done). The same rule pays for troops given to a lord through a Lord Courtesies gift. Default: on.")]
    public bool BondsTroopHandoverEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultTroopHandoverEnabled;

    [SettingPropertyGroup("Bonds of Battle")]
    [SettingPropertyInteger("Most Relation From One Barter Overpay", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinOverpayRelationCap, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxOverpayRelationCap, Order = 7, RequireRestart = false,
        HintText = "The most relation one barter can buy by offering more than the goods are worth. The loop buys at most 3 points in vanilla (the setting here), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change. Points cost more the further relation is from 0, so only a large overpay reaches the cap. Default: 15.")]
    public int BondsOverpayRelationCap { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultOverpayRelationCap;

    // --- Personal Relations (docs/features/personal-relations.md, #862) ---
    //
    // The master switch is read on every relation read (it gates TaomDiplomacyModel.GetHeroesForEffectiveRelation, on the
    // main thread), through PersonalRelationSettingsProvider's one cached settings reference. The disband values are read
    // when a disband costs relation. All RequireRestart = false: nothing is applied once at start-up. GroupOrder 60 is
    // unused. Defaults live in PersonalRelationSettingsProvider so the fallback before MCM is up cannot drift from them.

    [SettingPropertyGroup("Personal Relations", GroupOrder = 60)]
    [SettingPropertyBool("Enable Personal Relations", Order = 0, RequireRestart = false,
        HintText = "Your relation with a lord stays with that lord instead of passing to his clan leader, so a friendship with Faramir is not a friendship with Denethor. Affects only your own relations: relations between AI lords stay vanilla. Single player only: in a co-op session or on a dedicated server it stays vanilla. A save made before this was on is seeded once: on the first day this is on, each living lord whose personal relation with you is 0 takes his clan leader's value, and it is seeded again when this has been off for at least one in-game day. Off restores vanilla. Default: on.")]
    public bool PersonalRelationsEnabled { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Personal Relations")]
    [SettingPropertyBool("Disbanding An Army Costs Relation", Order = 1, RequireRestart = false,
        HintText = "Paying influence to disband another lord's army costs you relation with the army's leader by the amounts below (vanilla is 4); each other lord in it costs vanilla's 1. Applies only in single player, and only while Enable Personal Relations is on. Default: on.")]
    public bool PersonalDisbandCostEnabled { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultDisbandCostEnabled;

    [SettingPropertyGroup("Personal Relations")]
    [SettingPropertyInteger("Disband Cost, Army Leader, Least", TAOM.Features.Relations.PersonalRelationSettingsProvider.MinCost, TAOM.Features.Relations.PersonalRelationSettingsProvider.MaxCost, Order = 2, RequireRestart = false,
        HintText = "The least relation you lose with an army's leader when you pay influence to disband it. The cost is a random whole number between the least and the most. If the least is above the most, the two swap. Applies only in single player, and only while Enable Personal Relations is on. Default: 28.")]
    public int PersonalDisbandLeaderMin { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultLeaderMin;

    [SettingPropertyGroup("Personal Relations")]
    [SettingPropertyInteger("Disband Cost, Army Leader, Most", TAOM.Features.Relations.PersonalRelationSettingsProvider.MinCost, TAOM.Features.Relations.PersonalRelationSettingsProvider.MaxCost, Order = 3, RequireRestart = false,
        HintText = "The most relation you lose with an army's leader when you pay influence to disband it. Applies only in single player, and only while Enable Personal Relations is on. Default: 32.")]
    public int PersonalDisbandLeaderMax { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultLeaderMax;

    // --- Battle Tactics / Fell Foes (#893) ---
    // The first property carries the GroupOrder, because MCM ignores one on any later property; no other group may
    // share it (FellFoesSettingsProviderTests pins that). Defaults live in FellFoesTunables so the fallback before
    // MCM is up cannot drift from them. All RequireRestart = false: the service reads them each second.

    [SettingPropertyGroup("Battle Tactics/Fell Foes", GroupOrder = 61)]
    [SettingPropertyBool("Cornered Routers Fight", Order = 0, RequireRestart = false,
        HintText = "A routing enemy soldier who has just been hit, with a few of your men right beside him and enough health left, turns and fights instead of running on, and routers close to him turn with him (a few at most). They keep fighting only while one of your men is still near them and some of their side still stand; once the whole side has broken they run again, so the battle still ends in a rout. Soldiers of cultures that never rout, and soldiers ordered to retreat, are not touched. Field battles, sieges and sally-outs. Default: on.")]
    public bool FellFoesCorneredRouters { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyInteger("Cornered By No More Than", 1, 10, Order = 1, RequireRestart = false,
        HintText = "A routing soldier turns to fight only while this many of your men or fewer are within a few steps of him. Default: 3.")]
    public int FellFoesCorneredMaxFoes { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultCorneredMaxFoes;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyBool("Idle Archers Target You", Order = 2, RequireRestart = false,
        HintText = "Enemy archers that have nothing to shoot at, are steady, and are within bow range of you may fix on you and shoot, for a few seconds at a time. A cap keeps the volley small. Archers of a team that is making a stand against horse are left to that stand. Field battles, sieges and sally-outs. Default: on.")]
    public bool FellFoesArchersTargetPlayer { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyFloatingInteger("Archer Chance Each Second", 0f, 1f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "The chance each second that an idle archer in range fixes on you. 0 never, 1 always. Default: 0.25.")]
    public float FellFoesArcherChance { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultArcherChance;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyInteger("Archers On You At Once", 1, 30, Order = 4, RequireRestart = false,
        HintText = "The most enemy archers that hold you as their target at the same moment. Default: 8.")]
    public int FellFoesArcherMaxHolding { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultArcherMaxHolding;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyBool("Last Stragglers Leave The Field", Order = 5, RequireRestart = false,
        HintText = "In a large field battle, when only a few enemy soldiers are left, none of them a hero and none still to arrive, the survivors are sent off the field after a pause so the battle ends in your win and they can be taken prisoner. Sieges and sally-outs are not changed. Default: on.")]
    public bool FellFoesStragglersLeave { get; set; } = true;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyInteger("Stragglers, Most", 1, 20, Order = 6, RequireRestart = false,
        HintText = "The cleanup starts when this many enemy soldiers or fewer are left. Default: 3.")]
    public int FellFoesStragglerMaxCount { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultStragglerMaxCount;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyFloatingInteger("Straggler Pause (seconds)", 5f, 300f, "#0", Order = 7, RequireRestart = false,
        HintText = "Seconds the battle must stay down to the last few before the survivors are sent off, one a second. Default: 30.")]
    public float FellFoesStragglerHoldSeconds { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultStragglerHoldSeconds;

    [SettingPropertyGroup("Battle Tactics/Fell Foes")]
    [SettingPropertyBool("Fell Foes Debug Log", Order = 8, RequireRestart = false,
        HintText = "Writes a [FellFoes] line each second something is stopped, held or sent off, and whether held archers really are aiming at you. For checking the feature; leave off in normal play. Default: off.")]
    public bool FellFoesDebug { get; set; } = false;
    // --- Tokens of Esteem (docs/features/tokens-of-esteem.md, #863) ---
    //
    // Gifts of goods and gold for relation. Every value is read when a conversation line is shown or a gift is given, through
    // GiftSettingsProvider's one cached settings reference, so none needs a restart. GroupOrder 62 follows the
    // Fell Foes group at 61; 100 is the Map Tools group. Defaults
    // and ranges live in GiftSettingsProvider (the purse range in the gift band, GiftService.PurseGoldPerPoint and
    // PurseMaxPoints) so the fallback before MCM is up and the hint texts cannot drift from them.

    [SettingPropertyGroup("Tokens of Esteem", GroupOrder = 62)]
    [SettingPropertyBool("Enable Tokens of Esteem", Order = 0, RequireRestart = false,
        HintText = "Adds \"I have a gift for you.\" to the talk with the companions in your party and, on the diplomacy branch, with lords who are not children, not prisoners, not in your own clan and not at war with you. Goods the catalog lists, from your inventory, win relation; companions also take gold purses. A co-op client, or a peer running a co-op mod TAOM cannot probe, sees no gift line. Default: on.")]
    public bool GiftsEnabled { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyInteger("Days Between Gifts To One Hero", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinCooldownDays, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxCooldownDays, Order = 1, RequireRestart = false,
        HintText = "After a gift, the same hero takes no other for this many days, so goods cannot be turned straight into relation. Whenever Personal Relations is not engaged (switched off, a co-op host, or a save not yet seeded) a clan shares one relation, so the days count for the whole clan. The count is saved with the campaign. Default: 7.")]
    public int GiftCooldownDays { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultCooldownDays;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyInteger("Most Relation One Gift Asks For", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinRelationCap, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxRelationCap, Order = 2, RequireRestart = false,
        HintText = "The most relation one gift asks for, after the taste bonus and the scaling by relation (above relation 0 a gift adds (90 minus relation) / 90 of its worth, so a 1-point gift stops working near relation 45 and nothing helps at 90). A companion's gift starts at 1 to 5 by the item's value; a lord's starts at the catalog's value for the goods (1 to 10), and a hero who prizes the goods adds a bonus. Your Charm can add to what lands. Default: 15.")]
    public int GiftRelationCap { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultRelationCap;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyBool("Free Lords Refuse Gifts From Evil Players", Order = 3, RequireRestart = false,
        HintText = "A lord of a Free realm will not take a gift from a player who sides with the Shadow. The lord refuses with no penalty. Realms and cultures that side with neither never refuse. Default: on.")]
    public bool GiftAlignmentRefusal { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultAlignmentRefusalEnabled;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyInteger("Small Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 4, RequireRestart = false,
        HintText = "The gold in the smallest purse you can give a companion. The gold leaves you and goes to the companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold), before the scaling and the cap. Between 500 and 5000 gold. Default: 500.")]
    public int GiftPurseSmall { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseSmall;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyInteger("Medium Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 5, RequireRestart = false,
        HintText = "The gold in the middle purse you can give a companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold). Between 500 and 5000 gold. Default: 2000.")]
    public int GiftPurseMedium { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseMedium;

    [SettingPropertyGroup("Tokens of Esteem")]
    [SettingPropertyInteger("Large Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 6, RequireRestart = false,
        HintText = "The gold in the largest purse you can give a companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold). Between 500 and 5000 gold. Default: 5000.")]
    public int GiftPurseLarge { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseLarge;

    // --- Battle Tactics / Lost Footing (#894, phase 1: the census only) ---
    // The first property carries the GroupOrder, because MCM ignores one on any later property; no other group may
    // share it (LostFootingSettingsProviderTests pins that). GroupOrder 64 follows the groups at 62 and 63. The scan numbers are
    // compiled defaults in LostFootingTunables, not settings: nobody needs to tune a measurement.

    [SettingPropertyGroup("Battle Tactics/Lost Footing", GroupOrder = 64)]
    [SettingPropertyBool("Lost Footing Census", Order = 0, RequireRestart = false,
        HintText = "Counts soldiers, mounts and creatures that are stuck in a battle, standing off the walkable ground or wading in water without moving, and writes what it finds to the log with a [LostFooting] tag, with a summary when the battle ends. It only watches: it never moves or changes anything in the battle. Field battles, sieges and sally-outs. Default: on.")]
    public bool LostFootingCensus { get; set; } = true;
    // --- Good Deeds (docs/features/good-deeds.md, #864) ---
    //
    // Relation earned by releasing a captured lord, donating a hero prisoner to a settlement and winning a town's tournament.
    // Every value is read when the deed happens, through GoodDeedsSettingsProvider's one cached settings reference, so none
    // needs a restart. GroupOrder 65 follows Lost Footing at 64; 100 is the Map Tools group. Defaults and ranges live
    // in GoodDeedsSettingsProvider so the fallback before MCM is up and the hint texts cannot drift from them.

    [SettingPropertyGroup("Good Deeds", GroupOrder = 65)]
    [SettingPropertyBool("Enable Good Deeds", Order = 0, RequireRestart = false,
        HintText = "Turns the three rewards on or off together: relation for releasing a captured lord, for donating a hero prisoner to a settlement and for winning a town's tournament. Each reward also has its own switch below. Applies on the host in co-op. Default: on.")]
    public bool GoodDeedsEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyBool("Reward Releasing A Lord", Order = 1, RequireRestart = false,
        HintText = "Releasing a captured lord from your party (in conversation or on the party screen) or from your own dungeon earns relation with the lord, with the lord's living spouse and with the lord's clan leader. The post-battle \"free to go\" choice, ransom, peace, escape and any AI release earn nothing. Default: on.")]
    public bool GoodDeedsReleaseEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseEnabled;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyBool("Reward Donating A Hero Prisoner", Order = 2, RequireRestart = false,
        HintText = "Handing a hero prisoner to a settlement adds a bonus to the relation vanilla gives with the settlement owner's clan leader. Default: on.")]
    public bool GoodDeedsDonationEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationEnabled;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyBool("Reward Winning A Tournament", Order = 3, RequireRestart = false,
        HintText = "Winning a town's tournament earns relation with the owner's clan leader (unless your clan owns the town) and with every notable in the town. Default: on.")]
    public bool GoodDeedsTournamentEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentEnabled;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyInteger("Release: Relation With The Lord", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 4, RequireRestart = false,
        HintText = "Relation asked for with the lord you release, before the alignment multiplier. Vanilla pays another 4 in the conversation. Your Charm can add to what lands. Default: 5.")]
    public int GoodDeedsReleaseLordAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseLordAmount;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyInteger("Release: Relation With The Lord's Spouse", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 5, RequireRestart = false,
        HintText = "Relation asked for with the lord's living spouse, before the alignment multiplier. Default: 2.")]
    public int GoodDeedsReleaseSpouseAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseSpouseAmount;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyInteger("Release: Relation With The Lord's Clan Leader", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 6, RequireRestart = false,
        HintText = "Relation asked for with the leader of the lord's clan, before the alignment multiplier. While Personal Relations is off, the lord and the clan leader share one relation, so only the larger of the two is paid. Default: 2.")]
    public int GoodDeedsReleaseLeaderAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseLeaderAmount;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyInteger("Donation: Bonus Relation", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 7, RequireRestart = false,
        HintText = "Relation added to what vanilla gives with the settlement owner's clan leader for each hero prisoner donated, before the alignment multiplier. Vanilla itself pays nothing when the captive likes the owner. Default: 5.")]
    public int GoodDeedsDonationAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationAmount;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyInteger("Tournament: Relation Per Person", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxTournamentAmount, Order = 8, RequireRestart = false,
        HintText = "Relation asked for with the town owner's clan leader and with each notable in the town when you win its tournament, before the alignment multiplier. Between 0 and 10. Default: 1.")]
    public int GoodDeedsTournamentAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentAmount;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Release: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 9, RequireRestart = false,
        HintText = "Multiplier on every release reward when you and the lord are on the same side (both Free, or both Shadow). Results round half away from zero, and 0 grants nothing. Default: 1.00.")]
    public float GoodDeedsReleaseSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseSame;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Release: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 10, RequireRestart = false,
        HintText = "Multiplier on every release reward when you or the lord is on neither side. Default: 0.75.")]
    public float GoodDeedsReleaseNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseNeutral;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Release: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 11, RequireRestart = false,
        HintText = "Multiplier on every release reward when you and the lord are on opposite sides, so freeing an enemy of your cause earns little. Default: 0.50.")]
    public float GoodDeedsReleaseOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseOpposite;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Donation: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 12, RequireRestart = false,
        HintText = "Multiplier on the donation relation when the captive and the settlement owner are on the same side. Zero means no reward, and no vanilla relation either, for jailing a friend of the owner. Default: 0.00.")]
    public float GoodDeedsDonationSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationSame;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Donation: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 13, RequireRestart = false,
        HintText = "Multiplier on the donation relation when the captive or the settlement owner is on neither side. Default: 1.00.")]
    public float GoodDeedsDonationNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationNeutral;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Donation: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 14, RequireRestart = false,
        HintText = "Multiplier on the donation relation (vanilla's gain plus the bonus) when the captive is on the opposite side to the settlement owner, such as a Mordor captive handed to Minas Tirith. Default: 2.00.")]
    public float GoodDeedsDonationOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationOpposite;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Tournament: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 15, RequireRestart = false,
        HintText = "Multiplier on the tournament relation with an owner or notable on your side. Default: 1.00.")]
    public float GoodDeedsTournamentSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentSame;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Tournament: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 16, RequireRestart = false,
        HintText = "Multiplier on the tournament relation with an owner or notable who, or whose realm, is on neither side. Default: 1.00.")]
    public float GoodDeedsTournamentNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentNeutral;

    [SettingPropertyGroup("Good Deeds")]
    [SettingPropertyFloatingInteger("Tournament: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 17, RequireRestart = false,
        HintText = "Multiplier on the tournament relation with an owner or notable on the opposite side, who does not cheer a champion of the other cause. Default: 0.00.")]
    public float GoodDeedsTournamentOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentOpposite;

    // --- Long Memory (docs/features/long-memory.md, #865) ---
    //
    // Extra relation lost with a village owner's clan leader and the village's notables for raiding it or forcing it to give
    // goods or recruits, on top of vanilla's own loss. Every value is read when the wrong happens, through
    // LongMemorySettingsProvider's one cached settings reference, so none needs a restart. The sliders hold magnitudes (the
    // relation to lose), and the service negates them. GroupOrder 67 follows Fire and Tribute at 66.
    // Defaults and ranges live in LongMemorySettingsProvider so the fallback before MCM is up and the hint texts cannot drift.

    [SettingPropertyGroup("Long Memory", GroupOrder = 67)]
    [SettingPropertyBool("Enable Long Memory", Order = 0, RequireRestart = false,
        HintText = "Turns on the extra relation loss, on top of vanilla's own, that a village's owner and notables hold against you when you raid the village or force it to give goods or recruits. Villages your clan owns are never affected, and neither are raids you do not lead. Applies on the host in co-op. Default: on.")]
    public bool LongMemoryEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyBool("Remember Raids", Order = 1, RequireRestart = false,
        HintText = "A raid you lead costs relation with the owner's clan leader and with every notable of the village, in proportion to the damage the raid did. A raid that did none costs nothing. Default: on.")]
    public bool LongMemoryRaidEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidEnabled;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyBool("Remember Forced Takings", Order = 2, RequireRestart = false,
        HintText = "Forcing a village to give goods or recruits costs relation with the owner's clan leader and with every notable, whether the villagers resist or give way. A resisting village that wins the fight costs nothing. Default: on.")]
    public bool LongMemoryCoercionEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionEnabled;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyInteger("Raid: Relation Lost With The Owner's Clan Leader", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxRaidAmount, Order = 3, RequireRestart = false,
        HintText = "Relation lost with the clan leader who owns the village after a whole raid, before the alignment multiplier. Vanilla takes its own share as well. Between 0 and 100. Default: 50.")]
    public int LongMemoryRaidOwnerAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidOwnerAmount;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyInteger("Raid: Relation Lost With Each Notable", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxRaidAmount, Order = 4, RequireRestart = false,
        HintText = "Relation lost with every notable of the village after a whole raid, before the alignment multiplier. Vanilla takes its own share as well. Between 0 and 100. Default: 50.")]
    public int LongMemoryRaidNotableAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidNotableAmount;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyInteger("Forced Taking: Relation Lost With The Owner's Clan Leader", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxCoercionAmount, Order = 5, RequireRestart = false,
        HintText = "Relation lost with the clan leader who owns the village each time you force it to give goods or recruits, before the alignment multiplier. Between 0 and 50. Default: 20.")]
    public int LongMemoryCoercionOwnerAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionOwnerAmount;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyInteger("Forced Taking: Relation Lost With Each Notable", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxCoercionAmount, Order = 6, RequireRestart = false,
        HintText = "Relation lost with every notable of the village each time you force it to give goods or recruits, before the alignment multiplier. Between 0 and 50. Default: 25.")]
    public int LongMemoryCoercionNotableAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionNotableAmount;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyFloatingInteger("Same Side Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 7, RequireRestart = false,
        HintText = "Multiplier on the loss when you and the village owner's realm are on the same side, both Free or both Evil: the harshest pairing. Default: 1.00.")]
    public float LongMemorySame { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultSame;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyFloatingInteger("Neutral Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 8, RequireRestart = false,
        HintText = "Multiplier on the loss when you or the village owner's realm is on neither side. Default: 0.50.")]
    public float LongMemoryNeutral { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultNeutral;

    [SettingPropertyGroup("Long Memory")]
    [SettingPropertyFloatingInteger("Opposite Side Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 9, RequireRestart = false,
        HintText = "Multiplier on the loss when you and the village owner's realm are on opposite sides, such as an Evil warband raiding a Free village, where raiding is expected. Default: 0.10.")]
    public float LongMemoryOpposite { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultOpposite;

    // --- Hearth Roll (docs/features/hearth-roll.md, #906) ---
    //
    // GroupOrder 68 follows Long Memory at 67. The switch is read through HearthRollSettingsProvider's one cached settings
    // reference each time a clan member row refreshes, so it needs no restart.

    [SettingPropertyGroup("Hearth Roll", GroupOrder = 69)]
    [SettingPropertyBool("Tint Unhappy Companions", Order = 0, RequireRestart = false,
        HintText = "On the Members tab of the clan screen, gives the row of a companion of your clan a faint red tint when their relation with you has fallen below zero, a warning before they leave. The age beside each child's name is always shown. Applies the next time the clan screen opens. Default: on.")]
    public bool EnableUnhappyCompanionTint { get; set; } = true;
    // --- Prisoner Banners (docs/features/prisoner-banners.md, #902) ---
    //
    // The switch is read through PrisonerBannersSettingsProvider's one cached settings
    // reference each time a party screen row refreshes, so it needs no restart. A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("World/Prisoner Banners", GroupOrder = 75)]
    [SettingPropertyBool("Show Prisoner Banners", Order = 0, RequireRestart = false,
        HintText = "On the party screen, a captive lord's row shows the clan's banner at the top left of the portrait, the kingdom's banner at the top right (when the clan belongs to a kingdom), and a gold noble icon at the bottom left when the lord leads the clan. Applies the next time the party screen opens. Default: on.")]
    public bool PrisonerBannersEnabled { get; set; } = TAOM.Features.PrisonerBanners.PrisonerBannersSettingsProvider.DefaultEnabled;
    // --- Warden's Tally (docs/features/wardens-tally.md, #903) ---
    //
    // Read once each time a town or castle dungeon menu opens, through WardensTallySettingsProvider's one cached settings
    // reference, so it needs no restart. GroupOrder 80 is used by no other group (79 is Inn Rounds; 100 is Map Tools).
    // A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("World/Warden's Tally", GroupOrder = 80)]
    [SettingPropertyBool("Show Warden's Tally", Order = 0, RequireRestart = false,
        HintText = "In the dungeon menu of a town or castle, adds under the engine's own line how many prisoners the dungeon holds against its capacity, how many of them are lords, each lord's chance to escape each day with the terms behind it (your clan holding him halves it; the governor's perks change it), and a line for a lord whose realm has a War of the Ring escape boost, or whom nothing lets escape. It only shows the odds the game already uses and changes no rule. Applies the next time the menu opens. Default: on.")]
    public bool WardensTallyEnabled { get; set; } = TAOM.Features.WardensTally.WardensTallySettingsProvider.DefaultEnabled;
    // --- Sworn Anew (docs/features/sworn-anew.md, #869) ---
    //
    // Read at each conversation question (the diplomacy option and the ruler's prisoner line), through
    // SwornAnewSettingsProvider's one cached settings reference, so it needs no restart. GroupOrder 71 is used by no other group.
    // A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("World/Sworn Anew", GroupOrder = 71)]
    [SettingPropertyBool("Enable Sworn Anew", Order = 0, RequireRestart = false,
        HintText = "When enabled (default), a captured lord can be asked to join your kingdom, in a conversation or from the party screen, and serves it instead of being ransomed. The lord must hold a clan of another kingdom (not its ruler, and not a mercenary or minor clan), must be held by your party or in a settlement your clan owns, and must not be a lord who can never be captured, such as Sauron and the Nazgul. A Free Peoples lord is never asked to join an Evil realm, and an Evil lord is never asked to join a Free one. The base game's blood feud rule still applies. When off, the base game's rules apply: only a kingdom's ruler can recruit a captured lord, through its 'join us, and be set free' line, which checks no realm. When on, both lines pass this check, and the ruler-only line also needs the lord held by your party or in your clan's dungeon. Applies at once, no restart.")]
    public bool SwornAnewEnabled { get; set; } = TAOM.Features.SwornAnew.SwornAnewSettingsProvider.DefaultEnabled;
    // --- Provender-master (docs/features/provender.md, #908) ---
    //
    // Standing orders and morale options for the hero in the party's Quartermaster role. Every value is read when an order is
    // judged, through ProvenderSettingsProvider's one cached settings reference, so none needs a restart. GroupOrder 70
    // follows Hearth Roll at 69; 100 is the Map Tools group. Defaults and ranges live in ProvenderSettingsProvider so the
    // fallback before MCM is up and the hint texts cannot drift from them. These are new settings, so no existing TAOM.json
    // holds an older default for any of them.

    [SettingPropertyGroup("Provender-master", GroupOrder = 70)]
    [SettingPropertyBool("Enable Provender-master", Order = 0, RequireRestart = false,
        HintText = "Lets the hero you name Quartermaster (never you, and only while that hero is in your party) take standing orders in conversation: buy food at markets, slaughter livestock when the stores run low, lift the host's spirits with pay or drink, and sell your goods at a nearby town for a cut. Orders stand down for an enlisted player and for a co-op client. Default: on.")]
    public bool ProvenderEnabled { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Low Food Days", TAOM.Features.Provender.ProvenderSettingsProvider.MinLowFoodDays, TAOM.Features.Provender.ProvenderSettingsProvider.MaxLowFoodDays, Order = 1, RequireRestart = false,
        HintText = "When the food in the stores, not counting the herd, lasts fewer days than this, the slaughter order turns livestock into meat, a head at a time, until it lasts this long again. Between 1 and 30. Default: 3.")]
    public int ProvenderLowFoodDays { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLowFoodDays;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Livestock Kept Per Type", TAOM.Features.Provender.ProvenderSettingsProvider.MinKeepPerType, TAOM.Features.Provender.ProvenderSettingsProvider.MaxKeepPerType, Order = 2, RequireRestart = false,
        HintText = "The slaughter order always leaves this many head of each livestock type alive. The game itself still slaughters livestock when every other food is gone. Between 0 and 20. Default: 2.")]
    public int ProvenderKeepPerType { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultKeepPerType;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Small Pay: Gold Per Soldier", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayGold, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayGold, Order = 3, RequireRestart = false,
        HintText = "Gold paid to each soldier (not you) by the small pay option, in a settlement or a ready field camp. Between 10 and 500. Default: 50.")]
    public int ProvenderSmallPayGold { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSmallPayGold;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Small Pay: Morale Gained", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayMorale, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayMorale, Order = 4, RequireRestart = false,
        HintText = "Morale the party gains from the small pay option. Between 1 and 30. Default: 5.")]
    public int ProvenderSmallPayMorale { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSmallPayMorale;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Large Pay: Gold Per Soldier", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayGold, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayGold, Order = 5, RequireRestart = false,
        HintText = "Gold paid to each soldier (not you) by the large pay option. Never read below the small pay's gold. Between 10 and 500. Default: 100.")]
    public int ProvenderLargePayGold { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLargePayGold;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Large Pay: Morale Gained", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayMorale, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayMorale, Order = 6, RequireRestart = false,
        HintText = "Morale the party gains from the large pay option. Never read below the small pay's morale. Between 1 and 30. Default: 10.")]
    public int ProvenderLargePayMorale { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLargePayMorale;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Wage Days Kept", TAOM.Features.Provender.ProvenderSettingsProvider.MinWageDaysKept, TAOM.Features.Provender.ProvenderSettingsProvider.MaxWageDaysKept, Order = 7, RequireRestart = false,
        HintText = "The food order never spends the gold your party needs for this many days of wages, so buying food cannot leave your troops unpaid. 0 lets it spend the whole purse. Morale purchases are not limited: they show their price. Between 0 and 30. Default: 3.")]
    public int ProvenderWageDaysKept { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultWageDaysKept;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Remote Sale: Largest Cut (%)", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleFeePercent, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleFeePercent, Order = 8, RequireRestart = false,
        HintText = "The Provender-master's cut, in percent of a town's price, when you send goods to be sold there and the Provender-master has no Trade skill. It falls to the smallest cut at Trade 250, and each town further down the list adds 5. Between 0 and 90. Default: 30.")]
    public int ProvenderSaleMaxFeePercent { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleMaxFeePercent;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Remote Sale: Smallest Cut (%)", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleFeePercent, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleFeePercent, Order = 9, RequireRestart = false,
        HintText = "The Provender-master's cut at Trade 250. Never read above the largest cut. Between 0 and 90. Default: 10.")]
    public int ProvenderSaleMinFeePercent { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleMinFeePercent;

    [SettingPropertyGroup("Provender-master")]
    [SettingPropertyInteger("Remote Sale: Towns Offered", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleTownCount, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleTownCount, Order = 10, RequireRestart = false,
        HintText = "How many of the nearest towns that will buy from you the Provender-master offers. A town at war with you, of the opposing alignment, besieged, barring you or short of gold is never offered. Between 1 and 5. Default: 3.")]
    public int ProvenderSaleTownCount { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleTownCount;

    // --- Bond of Hire (docs/features/bond-of-hire.md, #867) ---
    //
    // Read when a clan signs on as a mercenary (the engine's service-started event), through BondOfHireSettingsProvider's one
    // cached settings reference, so RequireRestart = false. GroupOrder 72 is unused; it is the group's first property, so it
    // carries the order (MCM ignores a GroupOrder on any later one). The default and the range live in BondOfHireSettingsProvider
    // so the fallback before MCM is up and the hint cannot drift.

    [SettingPropertyGroup("World/Bond of Hire", GroupOrder = 72)]
    [SettingPropertyInteger("Minimum Mercenary Contract (Days)", TAOM.Features.BondOfHire.BondOfHireSettingsProvider.MinDays, TAOM.Features.BondOfHire.BondOfHireSettingsProvider.MaxDays, Order = 0, RequireRestart = false,
        HintText = "The fewest days a clan that signs on as a mercenary stays with its kingdom. Until then it cannot leave a ruler who is in debt, be hired away by another kingdom, or leave in its daily consideration of whether to stay. A clan with more time left is not shortened. Your own clan is not bound, and neither is dismissing or hiring away a mercenary yourself. 0 turns it off. Applies when a clan signs on; clans already serving get it at their next signing. Between 0 and 120. Default: 30.")]
    public int BondOfHireMinimumDays { get; set; } = TAOM.Features.BondOfHire.BondOfHireSettingsProvider.DefaultMinimumDays;

    // --- Seasoned Hosts (docs/features/seasoned-hosts.md, #890) ---
    //
    // The skill XP multiplier is read at every skill XP grant and the training bonus for every troop of every AI lord party
    // each day, through SeasonedHostsSettingsProvider's one cached settings reference, so none needs a restart. Four new
    // settings, so their defaults reach every install. GroupOrder 73 is used by no other group.

    [SettingPropertyGroup("World/Seasoned Hosts", GroupOrder = 73)]
    [SettingPropertyFloatingInteger("{=taom_seasoned_hosts_training_bonus}AI Lord Party Training Bonus", TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MinTrainingBonus, TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MaxTrainingBonus, "0.00", Order = 0, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_training_bonus_hint}Extra daily and battle training XP for the troops of AI lord parties, as a fraction: 0.25 is 25 percent more. Applies to a lord party that is not yours, not in your clan and not in an army you lead; caravans and garrisons are left out. In a co-op session only the host's value applies; while a co-op mod TAOM cannot probe (such as BannerlordTogether) is enabled, it is off for every player. 0 is vanilla. Between 0 and 1. Default: 0.25.")]
    public float SeasonedHostsTrainingBonus { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultTrainingBonus;

    [SettingPropertyGroup("World/Seasoned Hosts")]
    [SettingPropertyFloatingInteger("{=taom_seasoned_hosts_skill_xp}AI Lord Skill XP Multiplier", TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MinSkillXpMultiplier, TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MaxSkillXpMultiplier, "0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_skill_xp_hint}Multiplies the skill XP that AI lords earn, so their skills climb faster. Skills only: a lord's hero level is not sped up. Never applies to you or to any lord of your clan, spouse and family included. In a co-op session only the host's value applies; while a co-op mod TAOM cannot probe (such as BannerlordTogether) is enabled, it is off for every player. 1 is vanilla. Between 1 and 2. Default: 1.25.")]
    public float SeasonedHostsSkillXpMultiplier { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultSkillXpMultiplier;

    [SettingPropertyGroup("World/Seasoned Hosts")]
    [SettingPropertyBool("{=taom_seasoned_hosts_learning_floor}Learning Never Stops", Order = 2, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_learning_floor_hint}Past a skill's learning limit the game lowers the learning rate until it reaches zero and the skill stops growing. When on, the rate never falls below 0.05, so a skill keeps growing very slowly. This applies to every hero, you included. When off, the game's own rule applies. Default: on.")]
    public bool SeasonedHostsLearningNeverStops { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultLearningNeverStops;

    [SettingPropertyGroup("World/Seasoned Hosts")]
    [SettingPropertyBool("{=taom_seasoned_hosts_no_steward_xp}No Steward XP From Finished Projects", Order = 3, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_no_steward_xp_hint}When on, a settlement project that finishes grants no Steward XP, which the game otherwise gives 1000 of each time. This covers every settlement, yours included. In a co-op session only the host skips the grant; while a co-op mod TAOM cannot probe (such as BannerlordTogether) is enabled, the game's grant is left alone. Default: off.")]
    public bool SeasonedHostsNoStewardXp { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultNoStewardXp;

    // --- Smith's Measure (docs/features/smiths-measure.md, #910) ---
    //
    // Read through SmithsMeasureSettingsProvider's one cached settings reference: the smelt cap whenever TaomSmithingModel
    // answers a smelting output (the smelting list and preview, and the smelt itself), the Make Tools switch whenever the
    // refinement tab refreshes. None needs a restart. GroupOrder 74 sits on the group's first property, here; Many
    // Hammers (#911) adds its two settings to the group without one (MCM keeps the first order it meets). Order 0 to 9 is this feature's; #911 takes 10 and up.
    // The text is the {=KEY}English form, as Telchar's Mark does. All three are new settings, so the defaults reach every install.

    [SettingPropertyGroup("Smithing", GroupOrder = 74)]
    [SettingPropertyBool("{=taom_sm_mcm_cap}Cap Smelting Yield", Order = 0, RequireRestart = false,
        HintText = "{=taom_sm_mcm_cap_hint}Smelting a weapon returns at most the number set below of each iron grade, of iron ore and of hardwood, and no hardwood at all when any metal comes out. Charcoal use is unchanged. This curbs the loop of selling looted weapons as iron. Default: on.")]
    public bool SmithsMeasureCapSmelting { get; set; } = TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.DefaultCapSmelting;

    [SettingPropertyGroup("Smithing")]
    [SettingPropertyInteger("{=taom_sm_mcm_cap_value}Smelting Yield Cap", TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.MinYieldCap, TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.MaxYieldCap, Order = 1, RequireRestart = false,
        HintText = "{=taom_sm_mcm_cap_value_hint}The most of each iron grade, of iron ore and of hardwood that one smelt returns while the cap is on. Between 1 and 10; a value outside that range counts as 1. Default: 1.")]
    public int SmithsMeasureYieldCap { get; set; } = TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.DefaultYieldCap;

    [SettingPropertyGroup("Smithing")]
    [SettingPropertyBool("{=taom_sm_mcm_make_tools}Enable Make Tools", Order = 2, RequireRestart = false,
        HintText = "{=taom_sm_mcm_make_tools_hint}Adds a Make Tools button to the Refine tab of the smithing screen: one Iron and one Hardwood make one Tools, for smithy stamina and a little Smithing experience. Not shown in a co-op session. Default: on.")]
    public bool SmithsMeasureMakeTools { get; set; } = TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.DefaultMakeTools;

    // --- Many Hammers (docs/features/many-hammers.md, #911) ---
    //
    // Read at each smithy click and each smelting-list rebuild, through ManyHammersSettingsProvider's one cached settings
    // reference, so RequireRestart = false. The "Smithing" group belongs to Smith's Measure (#910), whose first property
    // carries GroupOrder 74 (MCM ignores it on any later one), so these carry none. Order 10 and 11 leave the low numbers to
    // the group's other features. Both are new settings, so the default (on) reaches every install. The defaults live in
    // ManyHammersSettingsProvider so the fallback before MCM is up cannot drift.

    [SettingPropertyGroup("Smithing")]
    [SettingPropertyBool("{=taom_mh_mcm_bulk}Bulk Smelting and Refining", Order = 10, RequireRestart = false,
        HintText = "{=taom_mh_mcm_bulk_hint}In the smithy, hold Shift while you smelt or refine to do five at once, or Ctrl to do the whole stack (Ctrl wins when both are held). It stops when your stamina or the charcoal or materials run out, and each unit costs what a single one would. Applies at once, no restart. Default: on.")]
    public bool ManyHammersBulkEnabled { get; set; } = TAOM.Features.ManyHammers.ManyHammersSettingsProvider.DefaultBulkEnabled;

    [SettingPropertyGroup("Smithing")]
    [SettingPropertyBool("{=taom_mh_mcm_hide_locked}Hide Locked Items from Smelting", Order = 11, RequireRestart = false,
        HintText = "{=taom_mh_mcm_hide_locked_hint}Weapons you locked in the inventory no longer appear in the smithy's smelting list, so they cannot be smelted by accident. Unlock one in the inventory to smelt it. A weapon you lock inside the smithy leaves the list at your next smelt or tab switch. No restart. Default: on.")]
    public bool ManyHammersHideLockedEnabled { get; set; } = TAOM.Features.ManyHammers.ManyHammersSettingsProvider.DefaultHideLockedEnabled;
}
