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

    [SettingPropertyGroup("{=taom_mcm_group_encyclopedia}Encyclopedia")]
    [SettingPropertyBool("{=taom_mcm_ShowAllEncyclopediaCharacters}Show All Characters", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_ShowAllEncyclopediaCharacters_hint}Reveals all characters in the encyclopedia, including those not yet encountered. Equivalent to the 'campaign.toggle_information_restrictions' cheat.")]
    public bool ShowAllEncyclopediaCharacters { get; set; } = true;

    // --- Troop Weight ---

    [SettingPropertyGroup("{=taom_mcm_group_troop_weight}Troop Weight")]
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
    [SettingPropertyBool("{=taom_mcm_EnableTroopWeight}Enable Troop Weight", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableTroopWeight_hint}Weighted party size: elite units consume more party capacity. Elites 2x, level 41+ elites 3x, warg riders 2x, cave trolls 4x, mumakil 10x. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public bool EnableTroopWeight { get; set; } = true;

    // --- AI Party Size ---

    // GroupOrder matters more than it looks: MCM sorts top-level groups by GroupOrder DESCENDING
    // (MCM.Abstractions.CollectionExtensions.SortDefault), then by name descending. Without an
    // explicit order this group sat at 0 and, starting with "A", fell to the very bottom of that
    // bucket: 24th of 32 groups, about 150 rows down, which is why it read as missing entirely.
    // 44 places it just under Supply Lines (45). Note the asymmetry with Order on the properties
    // below, which MCM sorts ASCENDING.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size", GroupOrder = 44)]
    [SettingPropertyBool("{=taom_mcm_EnableAiPartyScaling}Enable AI Party Scaling", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAiPartyScaling_hint}Lets AI lord parties HOLD the roster their party template spawns them with. Without this they spawn at 500-3000 and are trimmed back to the vanilla 50-150 cap within a day. Also relieves AI food and wage pressure, because those drive a second, morale-based desertion path that ignores the cap entirely. Off restores the pre-feature behaviour. Note that is not the same as raw vanilla: the Troop Weight elite tax has its own toggle and still deflates a heavy party's limit when it is on. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public bool EnableAiPartyScaling { get; set; } = true;

    // Renamed from AiLordPartySizeFactor (#1017): its default went 5 to 1 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 5.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AiLordPartyScale}AI Lord Party Size Multiplier", 1.0f, 20.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_AiLordPartyScale_hint}Multiplies an AI lord's party size limit. Preserves clan-tier progression, so a tier-4 lord still outgrows a tier-1. 1.0 = vanilla, which is the default: this feature ships neutral and you opt in. Raise this and the Flat Bonus together: the bonus is added after the multiplier, so moving one alone shifts the total by less than you expect. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiLordPartyScale { get; set; } = AiPartySizeService.DefaultLordFactor;

    // Renamed from AiLordPartySizeFlatBonus (#1017): its default went 150 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 150.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AiLordPartyFlatBonus}AI Lord Party Size Flat Bonus", 0.0f, 2000.0f, "#0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_AiLordPartyFlatBonus_hint}Added to an AI lord's party size limit AFTER the multiplier, so it is worth exactly this many men. Exists because template spawn size does not scale with clan tier: under a multiplier alone, low-tier lords still shed. Default: 0. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiLordPartyFlatBonus { get; set; } = AiPartySizeService.DefaultLordFlatBonus;

    // Renamed from AiGarrisonSizeFactor (#1017): its default went 3 to 1 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 3.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AiGarrisonScale}Garrison Size Multiplier", 1.0f, 10.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_AiGarrisonScale_hint}Multiplies every settlement garrison's size limit, player-owned included. Siege balance, not an AI handicap: lords fielding thousands would walk over garrisons still capped near vanilla's 200. Raising this also gives lords more room to donate troops on entering a friendly fief. 1.0 = vanilla, which is the default. IMPORTANT: raise this only alongside the AI Lord multiplier above. Vanilla refuses to even target a settlement when the attacker is under twice the defender's strength, and that strength counts garrison AND militia, so a high garrison multiplier with vanilla-sized lord parties stops AI sieges happening at all. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiGarrisonScale { get; set; } = AiPartySizeService.DefaultGarrisonFactor;

    // Renamed from AiFoodConsumptionRelief (#1017): its default went 0.9 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 0.9.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AiPartyFoodRelief}AI Food Relief", 0.0f, 0.95f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_AiPartyFoodRelief_hint}Fraction of an AI lord party's food consumption waived. A large party cannot buy 30 days of food from any town, so it starves permanently, and starvation is -30 morale which opens vanilla's morale-desertion path. 0 = vanilla, which is the default. Only worth raising if you have raised the AI Lord multiplier: at vanilla party sizes nothing starves. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiPartyFoodRelief { get; set; } = AiPartySizeService.DefaultFoodRelief;

    // Renamed from AiWageRelief (#1017): its default went 0.9 to 0 in cd5a6f5e8 (v2.0.26) under the same name,
    // and MCM loads the saved value by name, so every install that saved settings on v2.0.24 or earlier kept 0.9.
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AiPartyWageRelief}AI Wage Relief", 0.0f, 0.95f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_AiPartyWageRelief_hint}Fraction of an AI lord party's wage bill waived. AI clan wage budgets are set from clan gold and a large party blows past them, pinning unpaid wages at -20 morale and opening the same desertion path. 0 = vanilla, which is the default. Only worth raising if you have raised the AI Lord multiplier. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public float AiPartyWageRelief { get; set; } = AiPartySizeService.DefaultWageRelief;

    // Resolved by SelectedIndex, so the option ORDER here is load-bearing and must stay in step with
    // PlayerClanScalingMode. Only party size crosses to the player: the food and wage relief stay
    // AI-only regardless of this setting, because neither pressure they compensate for can occur for
    // a player clan (ClanVariablesCampaignBehavior guards `clan != Clan.PlayerClan` before setting the
    // wage cap, and BuyFoodInternal early-returns on IsMainParty).
    [SettingPropertyGroup("{=taom_mcm_group_ai_party_size}AI Party Size")]
    [SettingPropertyDropdown("{=taom_mcm_AiPlayerClanPartyScaling}Apply Party Size To Player Clan", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_AiPlayerClanPartyScaling_hint}Whether YOUR clan's lord parties get the same size scaling as AI lords. 'Taken-over lords only' (default) applies it when you started as an existing lord via Player Switcher, whose clan was given AI-sized rosters at world generation and would otherwise shed thousands of men. Detected from the player clan's id, so it survives save/load. 'Always' also applies it to an ordinary campaign start. 'Never' is the pre-2026-09-01 behaviour. Food and wage relief are never granted to your clan either way.")]
    public Dropdown<string> AiPlayerClanPartyScaling { get; set; } = new Dropdown<string>(
        new[] { "Never", "Taken-over lords only", "Always" }, (int)AiPartySizeService.DefaultPlayerClanScaling);

    // --- Settlement Food ---

    [SettingPropertyGroup("{=taom_mcm_group_settlement_food}Settlement Food")]
    [SettingPropertyBool("{=taom_mcm_EnableSettlementFoodTuning}Enable Settlement Food Tuning", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSettlementFoodTuning_hint}Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
    public bool EnableSettlementFoodTuning { get; set; } = true;

    // --- Settlement Economy ---

    [SettingPropertyGroup("{=taom_mcm_group_settlement_economy}Settlement Economy")]
    [SettingPropertyBool("{=taom_mcm_EnableSettlementEconomyTuning}Enable Settlement Economy Tuning", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSettlementEconomyTuning_hint}Regenerates town market gold toward a higher target (base 25000 vs vanilla 10000) so drained town markets recover — tunable in settlement_economy/settlement_economy_config.json (base, gold per prosperity, daily regen rate). Applies to existing saves immediately. Off = vanilla engine gold math. Config edits need an app restart.")]
    public bool EnableSettlementEconomyTuning { get; set; } = true;

    // --- Caravan Trade ---

    [SettingPropertyGroup("{=taom_mcm_group_caravan_trade}Caravan Trade")]
    [SettingPropertyBool("{=taom_mcm_EnableCaravanTrade}Enable Caravan Trade Overhaul", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCaravanTrade_hint}Makes AI/player caravans range beyond the local town cluster instead of shuttling between very-close towns, trade across the endless Free-vs-Evil war (per War Trade Policy below), and carry fuller baskets. Off = exact vanilla caravan behavior. Advanced curve knobs live in caravan_trade/caravan_trade_config.json; config edits need an app restart.")]
    public bool EnableCaravanTrade { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_caravan_trade}Caravan Trade")]
    [SettingPropertyBool("{=taom_mcm_CaravanTradeApplyToPlayer}Apply To Player Caravans", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CaravanTradeApplyToPlayer_hint}When on, your OWN caravans also range further, trade cross-war, and buy fuller baskets (they may enter contested territory and risk attack). Off scopes the selection re-weight + basket diversity off your clan's caravans. Note: the wider distance ceiling is engine-global (shared by all caravans) and the war-trade gate scopes at your whole faction, so those two levers aren't fully isolated to non-player caravans — but with this off your caravans still route by vanilla's nearest-first selection.")]
    public bool CaravanTradeApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_caravan_trade}Caravan Trade")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CaravanRangeMultiplier}Caravan Range Multiplier", 1.0f, 4.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CaravanRangeMultiplier_hint}How much further caravans range past the vanilla distance ceiling. 1.0 = vanilla reach; higher = they visit more distant, more profitable markets. Default: 1.6.")]
    public float CaravanRangeMultiplier { get; set; } = 1.6f;

    [SettingPropertyGroup("{=taom_mcm_group_caravan_trade}Caravan Trade")]
    [SettingPropertyDropdown("{=taom_mcm_CaravanWarTradePolicy}War Trade Policy", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_CaravanWarTradePolicy_hint}Which towns caravans may trade with despite the war. Vanilla = war blocks trade (the cause of the shuttle). Same Side + Neutral = trade with same-alignment or neutral factions but not the enemy side (default, lore-friendly). Ignore War = trade anywhere non-besieged.")]
    public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
        new[] { "Vanilla (war blocks)", "Same Side + Neutral", "Ignore War" }, 1);

    [SettingPropertyGroup("{=taom_mcm_group_caravan_trade}Caravan Trade")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CaravanBudgetDiversityFloor}Basket Diversity Floor", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_CaravanBudgetDiversityFloor_hint}Raises poor caravans' buying power so they stock several goods instead of one. 0 = vanilla (poor caravans buy a single item); higher = fuller baskets. Default: 0.35.")]
    public float CaravanBudgetDiversityFloor { get; set; } = 0.35f;

    // --- Castle Recruitment ---

    [SettingPropertyGroup("{=taom_mcm_group_castle_recruitment}Castle Recruitment")]
    [SettingPropertyBool("{=taom_mcm_EnableCastleRecruitment}Enable Castle Recruitment", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCastleRecruitment_hint}When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
    public bool EnableCastleRecruitment { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_castle_recruitment}Castle Recruitment")]
    [SettingPropertyBool("{=taom_mcm_EnableCastleRecruitmentAi}AI Recruits From Castles", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCastleRecruitmentAi_hint}When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
    public bool EnableCastleRecruitmentAi { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_castle_recruitment}Castle Recruitment")]
    [SettingPropertyInteger("{=taom_mcm_CastleNotablesPerCastle}Notables Per Castle", 1, 5, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CastleNotablesPerCastle_hint}How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
    public int CastleNotablesPerCastle { get; set; } = 3;

    // --- Elite Emissary ---

    [SettingPropertyGroup("{=taom_mcm_group_elite_emissary}Elite Emissary")]
    [SettingPropertyBool("{=taom_mcm_EnableEliteEmissary}Enable Elite Emissary", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableEliteEmissary_hint}At a faction's key settlements (capitals), speak with the faction emissary to buy that faction's elite troops for its special resource (Castar, War Spoils, Gems...). Conquering a settlement flips its offerings to the new owner.")]
    public bool EnableEliteEmissary { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_elite_emissary}Elite Emissary")]
    [SettingPropertyBool("{=taom_mcm_HideEmissaryWhenNoResource}Hide Emissary Without Resource", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_HideEmissaryWhenNoResource_hint}When on, the emissary option is hidden at settlements whose owner faction has no special resource. When off, the option still appears but is disabled with an explanatory hint.")]
    public bool HideEmissaryWhenNoResource { get; set; } = true;

    // --- Tournaments ---

    // Renamed from TournamentMaxBetPerRound (Mike, 2026-11-10): default goes from 0 (unlimited) to 15,000, and
    // MCM loads a saved value by name, so a player who ever saved any value under the old name would have kept it.
    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyInteger("{=taom_mcm_TournamentBetCapPerRound}Max Bet Per Round", 0, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxBetSetting, Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_TournamentBetCapPerRound_hint}The most you may bet in each tournament round. 0 = unlimited (up to your purse); raise this slider for a bigger cap, or back to 0 to remove it. Roguery's Deep Pockets doubles any other value. Vanilla is 150. Default: 15,000. Saved values from before 2026-11-10 were reset to this default; if you had chosen your own, set it again.")]
    public int TournamentBetCapPerRound { get; set; } = 15_000;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyFloatingInteger("{=taom_mcm_TournamentRenownMultiplier}Renown Multiplier", 0.0f, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxMultiplier, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_TournamentRenownMultiplier_hint}Scales the renown a tournament winner earns (base: vanilla's 3 plus 1 per lord or hero in the field, times the winner's culture factor). 1.00 = as designed. Applies to every winner. Default: 1.00.")]
    public float TournamentRenownMultiplier { get; set; } = 1.0f;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyFloatingInteger("{=taom_mcm_TournamentInfluenceMultiplier}Influence Multiplier", 0.0f, TAOM.Features.TournamentRewards.TournamentRewardRules.MaxMultiplier, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_TournamentInfluenceMultiplier_hint}Scales the influence a tournament winner earns in a town of their own kingdom (base: 2 plus 1 per 4 lords or heroes in the field, times the winner's culture factor). 1.00 = as designed. Default: 1.00.")]
    public float TournamentInfluenceMultiplier { get; set; } = 1.0f;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyBool("{=taom_mcm_TournamentKeepEnemySidesOut}Keep Enemy Sides Out", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_TournamentKeepEnemySidesOut_hint}A Free People's tournament seats no lord, wanderer or troop of the Shadow, and a tournament of the Shadow seats none of the Free Peoples; each is replaced by one of the town's own troops. A town's side is its owner's, so a captured town follows its conqueror, and a town held by a neutral realm bars nobody. Neutral lords and wanderers, the troops the town itself recruits, you and your clan always compete. Fewer lords in the field means a lesser prize. Default: on.")]
    public bool TournamentKeepEnemySidesOut { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyBool("{=taom_mcm_EnableTidingsOfTheLists}Tidings of the Lists", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_EnableTidingsOfTheLists_hint}Adds an option to a town's training grounds menu: for a small fee the training master names the nearest towns holding a tournament now that are not at war with your realm. It marks nothing on the map. When off, the option is hidden. Single-player only. Default: on.")]
    public bool EnableTidingsOfTheLists { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyBool("{=taom_mcm_EnableTeamPracticeFights}Team Practice Fights", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_EnableTeamPracticeFights_hint}In a town of the Free Peoples the practice fight at the training grounds is a team fight: you and the fighters outlined in blue against the other side, and no one can hurt his own side. A town's side is its owner's, so a captured town follows its conqueror. Towns of the Shadow and neutral towns keep the fight where every fighter is alone. Takes effect the next time you enter the training grounds. Single-player only. Default: on.")]
    public bool EnableTeamPracticeFights { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_tournaments}Tournaments")]
    [SettingPropertyBool("{=taom_mcm_EnableTrainingWaves}Wave Fights at the Training Grounds", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_EnableTrainingWaves_hint}When on, the training master of a town also runs a wave fight. You wager on yourself before each wave and fight the town's own fighters with practice weapons. Clear a wave and the master pays your wager back with odds; go down, or leave the yard mid-fight, and the wager stays with the town. Between waves you may stop and keep your winnings. A town runs one wave fight a day, and none while it holds a tournament. The tournament bet cap also limits a wave wager, and a very low cap hides the offer. Single-player only. Default: on.")]
    public bool EnableTrainingWaves { get; set; } = TAOM.Features.TrainingGrounds.Waves.TrainingWavesSettingsProvider.DefaultEnabled;

    // --- Armour Acquisition ---

    [SettingPropertyGroup("{=taom_mcm_group_armour_acquisition}Armour Acquisition")]
    [SettingPropertyBool("{=taom_mcm_EnableArmourAcquisition}Enable Armour Acquisition", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableArmourAcquisition_hint}Markets and battle loot hand out light and medium armour freely. Heavy, elite and lord pieces are sold only where a town's armoury, its Barracks, is high enough; lord kit can also be forged there or earned piece by piece on the lord's gear ladder, whose last rung awards the named hero weapons; they are never sold or looted. Visit a town's armoury to upgrade a piece with metals. Takes effect from the next game load.")]
    public bool EnableArmourAcquisition { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_armour_acquisition}Armour Acquisition")]
    [SettingPropertyBool("{=taom_mcm_EnableArmourLordEvent}Lord's Harness After Battle", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableArmourLordEvent_hint}After a battle your side wins against an enemy lord, you may rarely find his household's lord harness among the spoils. Needs Armour Acquisition on.")]
    public bool EnableArmourLordEvent { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_armour_acquisition}Armour Acquisition")]
    [SettingPropertyBool("{=taom_mcm_EnableVisitingArmourer}Visiting Master Armourers", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableVisitingArmourer_hint}Now and then a master armourer visits a town for a week, raising its armoury one level. Needs Armour Acquisition on.")]
    public bool EnableVisitingArmourer { get; set; } = true;

    // --- Culture Conversion ---

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_EnableCultureConversion}Enable Culture Conversion", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCultureConversion_hint}When enabled, a town/castle (and its villages) conquered by a different culture gradually adopts the new owner's culture — producing their troops, militia, and identity. Disabling stops NEW conversions; already-converted settlements stay converted.")]
    public bool EnableCultureConversion { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyInteger("{=taom_mcm_CultureConversionHoldDays}Days To Convert", 1, 365, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionHoldDays_hint}Days the new owner must hold a cross-culture fief before it converts. Lower = faster cultural takeover. Default: 1 (near-instant on capture).")]
    public int CultureConversionHoldDays { get; set; } = 1;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_CultureConversionRequireStableLoyalty}Require Stable Loyalty", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionRequireStableLoyalty_hint}When enabled, a conquered fief only converts once its loyalty is high enough (configured in culture_conversion_config.json), so a city in unrest never flips. Default: off.")]
    public bool CultureConversionRequireStableLoyalty { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_CultureConversionReplaceNotables}Replace Notables On Conversion", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionReplaceNotables_hint}When enabled, conversion also replaces the settlement's notables with ones of the new culture (a Mordor-held Gondor town gets orc notables). Their workshops, alleys and caravans transfer to the newcomers; your relations with the old notables do not. Default: on.")]
    public bool CultureConversionReplaceNotables { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_CultureConversionReplaceGarrison}Replace Garrison On Conversion", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionReplaceGarrison_hint}When enabled, conversion re-mans the fief's garrison with the new culture's troops, matching tier, battlefield role and head count (a Gondor tier-4 archer becomes a Mordor tier-4 archer). Without this a fief taken by gift, barter or a king's decision keeps its old culture's garrison forever. Default: on.")]
    public bool CultureConversionReplaceGarrison { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_CultureConversionReplaceMilitia}Replace Militia On Conversion", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionReplaceMilitia_hint}When enabled, conversion also swaps the settlement's and its villages' existing militia to the new culture's militia troops. New militia already spawn in the new culture once the fief converts; this makes the standing militia catch up the same day instead of over many. Default: on.")]
    public bool CultureConversionReplaceMilitia { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_culture_conversion}Culture Conversion")]
    [SettingPropertyBool("{=taom_mcm_CultureConversionReplaceGarrisonPlayerFiefs}Replace Garrison In Your Own Fiefs", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_CultureConversionReplaceGarrisonPlayerFiefs_hint}When disabled, the two swaps above never touch a fief your clan owns, so a garrison you stacked by hand survives the conversion. AI fiefs still swap, and your fief still changes culture. Default: on.")]
    public bool CultureConversionReplaceGarrisonPlayerFiefs { get; set; } = true;

    // --- War of the Ring ---

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring")]
    [SettingPropertyBool("{=taom_mcm_WarOfTheRingEnabled}Enable War of the Ring", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_WarOfTheRingEnabled_hint}When enabled, a scripted war will escalate between Free Peoples and Dark Powers. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public bool WarOfTheRingEnabled { get; set; } = true;

    // Renamed from Phase1TriggerDay (#1017): its default went 2 to 30 in v2.0.15 (e273cd23f) under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 2.
    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring")]
    [SettingPropertyInteger("{=taom_mcm_WarPhase1TriggerDay}Phase 1 Start Day", 1, 365, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_WarPhase1TriggerDay_hint}Days after campaign start when Isengard and Dunland attack Rohan. Default 30. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public int WarPhase1TriggerDay { get; set; } = 30;

    // Renamed from Phase2TriggerDay (#1017): its default went 14 to 44 in v2.0.15 (e273cd23f) under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 14.
    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring")]
    [SettingPropertyInteger("{=taom_mcm_WarPhase2TriggerDay}Phase 2 Start Day", 1, 365, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_WarPhase2TriggerDay_hint}Days after campaign start when all hostile kingdoms go to war and peace between hostile tiers is blocked. Default 44. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public int WarPhase2TriggerDay { get; set; } = 44;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_test_mode}Test Mode")]
    [SettingPropertyBool("{=taom_mcm_TestMode}Enable Test Mode", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_TestMode_hint}Uses the short delays from war_of_the_ring.json (1/3 days) for rapid testing. Overrides Phase 1/2 days.")]
    public bool TestMode { get; set; }

    // --- War of the Ring / Momentum (#327) ---

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyBool("{=taom_mcm_MomentumEnabled}Enable Momentum Tracking", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumEnabled_hint}Tracks Evil vs Good war progress from battles, sieges, raids, armies and relative strength. The war ends at the victory threshold only when Enable Victory (End the War) is on; that setting is off by default, so the war is endless. Default: on.")]
    public bool MomentumEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyBool("{=taom_mcm_ShowWarOfTheRingMapMeter}Show Map Meter", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_ShowWarOfTheRingMapMeter_hint}Shows the on-map War of the Ring slider once the full war begins. Click it for the detailed breakdown. Default: on.")]
    public bool ShowWarOfTheRingMapMeter { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyBool("{=taom_mcm_MomentumVictoryEnabled}Enable Victory (End the War)", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumVictoryEnabled_hint}When OFF (default) the War of the Ring is endless: momentum is tracked but no side ever wins. Turn ON to let a side win at the Victory Threshold (or by eliminating the other), which ends the war and makes peace. Default: off (endless). This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public bool MomentumVictoryEnabled { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyInteger("{=taom_mcm_MomentumVictoryThreshold}Victory Threshold", 100, 2000, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumVictoryThreshold_hint}Momentum lead one side needs to win the war outright. Default 500.")]
    public int MomentumVictoryThreshold { get; set; } = 500;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyFloatingInteger("{=taom_mcm_MomentumParticipationMultiplier}Player Participation Multiplier", 1.0f, 3.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumParticipationMultiplier_hint}Momentum gains are multiplied by this when your party takes part in the event. Default 1.5.")]
    public float MomentumParticipationMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyBool("{=taom_mcm_MomentumRequirePlayerForVictory}Require Player Participation For Victory", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumRequirePlayerForVictory_hint}When on, the war cannot end until you have fought in enough momentum events yourself. Default: on.")]
    public bool MomentumRequirePlayerForVictory { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_momentum}Momentum")]
    [SettingPropertyInteger("{=taom_mcm_MomentumMinPlayerEvents}Minimum Player Events For Victory", 0, 20, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_MomentumMinPlayerEvents_hint}How many battles/sieges you must take part in before either side can win. Default 5.")]
    public int MomentumMinPlayerEvents { get; set; } = 5;

    // --- War of the Ring / Rally (#765) ---

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_rally}Rally")]
    [SettingPropertyBool("{=taom_mcm_WarRallyEnabled}Enable Rally", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_WarRallyEnabled_hint}Gives AI kingdoms that have lost a large share of their towns and castles a small, temporary boost: a higher daily volunteer roll in their towns, castles and villages (empty recruit slots fill faster and waiting recruits are promoted sooner) and faster escapes for their captured lords. Never applies to a kingdom you rule, or to your own clan's settlements and lords; its lords in your prisons escape faster too. Takes effect at the next daily tick; turning it off removes every rally effect. Default on.")]
    public bool WarRallyEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_rally}Rally")]
    [SettingPropertyFloatingInteger("{=taom_mcm_WarEffectStrength}War Effect Strength", 0.0f, 2.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_WarEffectStrength_hint}Scales every War of the Ring catch-up and event effect (a higher daily volunteer roll, which fills recruit slots faster and promotes waiting recruits sooner; faster escapes of captured lords). 0 turns them off, 1 is the authored size, 2 doubles it. Default 1.")]
    public float WarEffectStrength { get; set; } = 1.0f;

    // --- War of the Ring / Eye of Sauron (#765 M8) ---
    // Defaults live in EyeOfSauronSettingsProvider, the single source; the provider re-validates every value.

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyBool("{=taom_mcm_EyeOfSauronEnabled}Enable the Hunt", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EyeOfSauronEnabled_hint}When on, great deeds against the enemy draw a hunter: a Free player is hunted by one of the Nine with a host of Mordor (of Dol Guldur for its four wraiths), an Evil player by Glorfindel with a host of the Eldar. Only while your kingdom is at war with the hunter's. Single-player only. Default: on. This is the default for new campaigns: each campaign keeps its own choice from the start screen, and older saves follow this setting.")]
    public bool EyeOfSauronEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyFloatingInteger("{=taom_mcm_EyeThreshold}Deed Threshold", EyeOfSauron.EyeOfSauronSettingsProvider.MinThreshold, EyeOfSauron.EyeOfSauronSettingsProvider.MaxThreshold, "#0.0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EyeThreshold_hint}Deed score that draws a hunt. A victory over an enemy kingdom in the field, a village raid or a forced levy scores 1, a sally or a battle outside a siege 2, a siege assault 3. Default 10.")]
    public float EyeThreshold { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultThreshold;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyFloatingInteger("{=taom_mcm_EyeHalfLifeDays}Deed Half-Life (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinHalfLifeDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxHalfLifeDays, "#0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EyeHalfLifeDays_hint}Days for the deed score to fall by half. Default 30.")]
    public float EyeHalfLifeDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultHalfLifeDays;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyInteger("{=taom_mcm_EyeCooldownDays}Cooldown (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinCooldownDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxCooldownDays, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EyeCooldownDays_hint}Days after a hunt ends before another can start. Default 60.")]
    public int EyeCooldownDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultCooldownDays;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyInteger("{=taom_mcm_HuntTimeoutDays}Hunt Timeout (days)", EyeOfSauron.EyeOfSauronSettingsProvider.MinHuntTimeoutDays, EyeOfSauron.EyeOfSauronSettingsProvider.MaxHuntTimeoutDays, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_HuntTimeoutDays_hint}Days a hunt lasts before the hunter gives up. Default 20.")]
    public int HuntTimeoutDays { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultHuntTimeoutDays;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyFloatingInteger("{=taom_mcm_HunterSpeedMultiplier}Hunter Speed Multiplier", EyeOfSauron.EyeOfSauronSettingsProvider.MinSpeedMultiplier, EyeOfSauron.EyeOfSauronSettingsProvider.MaxSpeedMultiplier, "#0.00", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_HunterSpeedMultiplier_hint}Once the hunter is within one and a half times your seeing range, it moves at least this many times your speed. Default 1.3.")]
    public float HunterSpeedMultiplier { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultSpeedMultiplier;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyFloatingInteger("{=taom_mcm_HunterStrengthRatio}Hunter Strength Ratio", EyeOfSauron.EyeOfSauronSettingsProvider.MinStrengthRatio, EyeOfSauron.EyeOfSauronSettingsProvider.MaxStrengthRatio, "#0.00", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_HunterStrengthRatio_hint}The hunter's host is filled to this many times your strength (your army's, when you lead one). Default 1.3.")]
    public float HunterStrengthRatio { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultStrengthRatio;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyInteger("{=taom_mcm_HunterMaxTroops}Hunter Max Troops", EyeOfSauron.EyeOfSauronSettingsProvider.MinMaxTroops, EyeOfSauron.EyeOfSauronSettingsProvider.MaxMaxTroops, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_HunterMaxTroops_hint}The most men the hunter's party holds after its host is topped up. Default 250.")]
    public int HunterMaxTroops { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultMaxTroops;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_eye_of_sauron}Eye of Sauron")]
    [SettingPropertyFloatingInteger("{=taom_mcm_HunterStepCap}Hunter Step Cap", EyeOfSauron.EyeOfSauronSettingsProvider.MinStepCap, EyeOfSauron.EyeOfSauronSettingsProvider.MaxStepCap, "#0.0", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_HunterStepCap_hint}The farthest the hunter closes in one hour while out of your sight, in map units. Default 15.")]
    public float HunterStepCap { get; set; } = EyeOfSauron.EyeOfSauronSettingsProvider.DefaultStepCap;

    // --- War of the Ring / War Chronicle (#765) ---

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_war_chronicle}War Chronicle")]
    [SettingPropertyBool("{=taom_mcm_WarChronicleEnabled}Enable Chronicle", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_WarChronicleEnabled_hint}Runs the War Chronicle: lore events of the War of the Ring at set points, and the great sieges (Helm's Deep, Osgiliath, Minas Tirith and others) as timed windows whose result gives both sides small, temporary effects. Off stops new events and clears the army focus; the saved progress is kept. Default on.")]
    public bool WarChronicleEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_war_chronicle}War Chronicle")]
    [SettingPropertyBool("{=taom_mcm_WarChronicleAnnouncements}Show Announcements", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_WarChronicleAnnouncements_hint}Shows a popup on the campaign map when a chronicle event happens, a siege window opens, or a siege is decided. Off keeps the events running silently. Default on.")]
    public bool WarChronicleAnnouncements { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_war_chronicle}War Chronicle")]
    [SettingPropertyFloatingInteger("{=taom_mcm_WarChronicleArmyFocus}Army Focus Multiplier", TAOM.Features.WarChronicle.WarChronicleSettingsProvider.ArmyFocusMin, TAOM.Features.WarChronicle.WarChronicleSettingsProvider.ArmyFocusMax, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_WarChronicleArmyFocus_hint}During a siege window, how much more the attackers' armies want to besiege the target and the defender's armies want to defend it once it is attacked. 1 turns the focus off. Needs AI Strategic Intelligence on. Default 1.4.")]
    public float WarChronicleArmyFocus { get; set; } = 1.4f;

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_war_chronicle}War Chronicle")]
    [SettingPropertyBool("{=taom_wc_mcm_story}Offer Story Missions", Order = 3, RequireRestart = false,
        HintText = "{=taom_wc_mcm_story_hint}When a great siege opens (the Battle of the Hornburg), offers you its battle to fight yourself. A win holds the fortress; a loss gives it to the attackers. If you let fate decide, the map decides, and a siege that nobody attacks ends as in the book. Off: no offer, and the map and fate decide. Not offered in co-op or while enlisted. Default on.")]
    public bool WarChronicleStoryMissions { get; set; } = true;

    // --- War of the Ring / War Pledge (#1113) ---

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_war_pledge}War Pledge")]
    [SettingPropertyBool("{=taom_war_pledge_mcm_enabled}Enable War Pledge", Order = 0, RequireRestart = false,
        HintText = "{=taom_war_pledge_mcm_enabled_hint}When on, a Neutral realm can join the War of the Ring for good. In Full War, the side that is far behind on the war meter (25 points or more) calls on a Neutral realm that leans its way: Khand, Rhudaur, Umbar, Shaghana and Abanissa lean to the Shadow, Cardolan and Dorwinion to the Free Peoples. The realm then goes to war with every realm of the other side, and that war cannot end in peace while the War of the Ring lasts. One realm joins at a time, 30 days apart unless war_pledge.json says otherwise, and never a realm that a player rules. Needs Momentum Tracking on. Off stops new joins by the AI; a realm that has joined stays joined. Default: on.")]
    public bool WarPledgeEnabled { get; set; } = true;

    // --- Battle Balance / Stamina (#1497) ---
    // Off by default while the feature is built on its branch. Decide the release default before the first release
    // that carries this property: MCM keeps a saved value over a later default (config-providers.md).

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_stamina}Stamina")]
    [SettingPropertyBool("{=taom_stamina_mcm_enabled}Enable Combat Stamina", Order = 0, RequireRestart = false,
        HintText = "{=taom_stamina_mcm_enabled_hint}Experimental. In field battles, sieges and the Fords of War match soldiers have stamina. A melee swing, a shot, a throw, a jump, a kick, a shield bash and a blow stopped with a weapon or shield cost stamina, and running on foot drains it down to half. Stamina comes back while a soldier does not attack, more slowly while a block is held and not at all while running. A tired soldier swings and handles a weapon more slowly, and a tired AI soldier attacks less often. A soldier whose stamina reaches zero is exhausted until it is back at about a third: an exhausted soldier cannot attack, block, kick or jump, and on foot slows to a walk. Exceptions: a soldier who works a siege engine pays nothing and is not stopped, and a rider or a swimmer can still jump. Higher tier troops and heroes have more stamina. A soldier's people changes it too: Elves, Dwarves, Uruk-hai and trolls have more than most Men of the same rank, Orcs and goblins have less, and the Nazgul do not tire. Horses and other mounts are not affected. Takes effect from the next battle. Default: off.")]
    public bool EnableStamina { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_stamina}Stamina")]
    [SettingPropertyBool("{=taom_stamina_mcm_player}Stamina Applies To You", Order = 1, RequireRestart = false,
        HintText = "{=taom_stamina_mcm_player_hint}When on, the soldier you control has stamina while Combat Stamina is on: your actions cost it and you can become exhausted. Off exempts that soldier: your stamina never drains, you are never stopped, and the stamina bar is hidden. AI soldiers still follow the next setting. Read at each battle tick. Default: on.")]
    public bool StaminaApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_stamina}Stamina")]
    [SettingPropertyBool("{=taom_stamina_mcm_ai}Stamina Applies To AI Soldiers", Order = 2, RequireRestart = false,
        HintText = "{=taom_stamina_mcm_ai_hint}When on, the soldiers the AI controls have stamina while Combat Stamina is on, allies and enemies alike. Off exempts them all: only the soldier you control tires, if the setting above is on. Read at each battle tick. Default: on.")]
    public bool StaminaApplyToAi { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_stamina}Stamina")]
    [SettingPropertyFloatingInteger("{=taom_stamina_mcm_cost_scale}Stamina Cost Scale", 0.5f, 2.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_stamina_mcm_cost_scale_hint}Multiplies what every action costs in stamina, for every soldier: the player, allies and enemies alike. 0.50 halves the costs, 2.00 doubles them. It does not change how fast stamina comes back or how much a soldier has. Read at each battle tick. Default: 1.00.")]
    public float StaminaCostScale { get; set; } = 1.0f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_stamina}Stamina")]
    [SettingPropertyBool("{=taom_stamina_mcm_show_bar}Show Stamina Bar", Order = 4, RequireRestart = false,
        HintText = "{=taom_stamina_mcm_show_bar_hint}Shows your own stamina as a bar beside the battle HUD rings while Combat Stamina is on. The bar changes colour as you tire and turns grey while you are exhausted. Off hides the bar only: stamina still works. Drawn on this client only. Default: on.")]
    public bool ShowStaminaBar { get; set; } = true;

    // --- Battle Balance / Troop Power ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyBool("{=taom_mcm_EnableCustomTroopPower}Enable Custom Troop Power", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCustomTroopPower_hint}Enables configurable T7-T10 troop power values for battle simulation.")]
    public bool EnableCustomTroopPower { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyBool("{=taom_mcm_OverrideVanillaTierPower}Override Vanilla Tiers (T1-T6)", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_OverrideVanillaTierPower_hint}If enabled, battle_balance_config.json TierPower values replace the vanilla formula for T1-T6.")]
    public bool OverrideVanillaTierPower { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_Tier7Power}Tier 7 Base Power", 2.0f, 6.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_Tier7Power_hint}Base simulation power for T7 troops (vanilla formula extrapolation = 3.06).")]
    public float Tier7Power { get; set; } = 2.91f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_Tier8Power}Tier 8 Base Power", 2.0f, 7.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_Tier8Power_hint}Base simulation power for T8 troops (vanilla formula extrapolation = 3.60).")]
    public float Tier8Power { get; set; } = 3.26f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_Tier9Power}Tier 9 Base Power", 2.0f, 8.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_Tier9Power_hint}Base simulation power for T9 troops (vanilla formula extrapolation = 4.18).")]
    public float Tier9Power { get; set; } = 3.61f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_Tier10Power}Tier 10 Base Power", 2.0f, 9.0f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_Tier10Power_hint}Base simulation power for T10 troops (vanilla formula extrapolation = 4.80).")]
    public float Tier10Power { get; set; } = 3.96f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_HeroMultiplier}Hero Power Multiplier", 1.0f, 3.0f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_HeroMultiplier_hint}Multiplier applied to heroes in battle simulation. Vanilla = 1.5.")]
    public float HeroMultiplier { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_troop_power}Troop Power")]
    [SettingPropertyFloatingInteger("{=taom_mcm_MountedMultiplier}Mounted Power Multiplier", 1.0f, 2.0f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_MountedMultiplier_hint}Multiplier applied to mounted troops in battle simulation. Vanilla = 1.2.")]
    public float MountedMultiplier { get; set; } = 1.2f;

    // --- Battle Balance / Casualty Ratios ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_casualty_ratios}Casualty Ratios")]
    [SettingPropertyBool("{=taom_mcm_EnableCustomCasualtyRatios}Enable Custom Casualty Ratios", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCustomCasualtyRatios_hint}Enables configurable wound/kill ratios for battle simulation.")]
    public bool EnableCustomCasualtyRatios { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_casualty_ratios}Casualty Ratios")]
    [SettingPropertyFloatingInteger("{=taom_mcm_PlayerBluntDamageChance}Player Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_PlayerBluntDamageChance_hint}Blunt (wound-only) damage chance in player battles. Vanilla = 0.30.")]
    public float PlayerBluntDamageChance { get; set; } = 0.30f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_casualty_ratios}Casualty Ratios")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AIBluntDamageChance}AI Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_AIBluntDamageChance_hint}Blunt damage chance in AI vs AI battles. Vanilla = 0.10.")]
    public float AIBluntDamageChance { get; set; } = 0.10f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_balance}Battle Balance/{=taom_mcm_group_casualty_ratios}Casualty Ratios")]
    [SettingPropertyBool("{=taom_mcm_EnableCulturalSurvivalBonuses}Enable Cultural Survival Bonuses", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCulturalSurvivalBonuses_hint}Applies the per-culture survival modifiers from battle_balance_config.json. A positive value means more of that culture's wounded survive.")]
    public bool EnableCulturalSurvivalBonuses { get; set; } = true;

    // --- Siege Defense ---

    [SettingPropertyGroup("{=taom_mcm_group_siege_defense}Siege Defense")]
    [SettingPropertyBool("{=taom_mcm_EnableSiegeDefenseEvents}Enable Siege Defense Events", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSiegeDefenseEvents_hint}When enabled, you receive an event when a watched faction's settlement is besieged, with a timed window to help defend.")]
    public bool EnableSiegeDefenseEvents { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_siege_defense}Siege Defense")]
    [SettingPropertyInteger("{=taom_mcm_SiegeDefenseResponseDays}Response Window (Days)", 1, 14, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_SiegeDefenseResponseDays_hint}Number of in-game days to travel to a besieged settlement before the event expires.")]
    public int SiegeDefenseResponseDays { get; set; } = 3;

    // --- AI Strategic Intelligence ---

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyBool("{=taom_mcm_EnableArmyStrategicIntelligence}Enable AI Strategic Intelligence", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableArmyStrategicIntelligence_hint}When enabled, AI armies stick to their current target rather than re-optimising every 3 hours. Reduces army thrashing and improves siege follow-through.")]
    public bool EnableArmyStrategicIntelligence { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ArmyCommitmentMultiplier}Commitment Multiplier", 1.0f, 10.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_ArmyCommitmentMultiplier_hint}How strongly an army commits to its current target. 4.0 = the alternative must score 4x better before the army will divert. Vanilla implicit = 1.3.")]
    public float ArmyCommitmentMultiplier { get; set; } = 4.0f;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ArmyPriorityBoost}Priority List Boost", 1.0f, 5.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_ArmyPriorityBoost_hint}Score multiplier applied to the first settlement in a faction's priority list. Decays linearly to 1.0 at the last entry. Affects Mordor, Isengard etc.")]
    public float ArmyPriorityBoost { get; set; } = 3.0f;

    [SettingPropertyFloatingInteger("{=taom_mcm_EvilFactionAggressionScale}Evil Faction Aggression Scale", 0.5f, 3.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EvilFactionAggressionScale_hint}Global multiplier applied to all per-faction strength inflation values from army_targeting.json. 1.0 = use JSON defaults. Raise to make evil factions siege even when outnumbered.")]
    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    public float EvilFactionAggressionScale { get; set; } = 1.0f;

    [SettingPropertyFloatingInteger("{=taom_mcm_ArmyBorderProximityFloor}Border Proximity Floor", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_ArmyBorderProximityFloor_hint}Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range, and only for targets inside the march radius below. 0 = vanilla.")]
    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    public float ArmyBorderProximityFloor { get; set; } = 0.15f;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyBool("{=taom_mcm_EnableWarTheaters}Enable War Theaters", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_EnableWarTheaters_hint}When enabled, kingdoms favour enemies on their own front. Gondor prefers Mordor and Isengard over far northern targets. Weighting only: no war is ever forbidden.")]
    public bool EnableWarTheaters { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ArmyBorderRescueRadius}Border Rescue Radius", 1.0f, 20.0f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_ArmyBorderRescueRadius_hint}How far, in average town gaps, a priority-list target may be and still have its unreachable verdict overturned by the Border Proximity Floor above. Not a general distance penalty: the game already applies one of its own. 3.2 clears the widest real front on the map.")]
    public float ArmyBorderRescueRadius { get; set; } = 3.2f;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ArmyDefenderPriority}Home Defence Priority", 1.0f, 5.0f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_ArmyDefenderPriority_hint}Score multiplier on defending one of your own settlements. Raise it if AI kingdoms keep besieging abroad while their own fiefs fall. 1.0 = vanilla.")]
    public float ArmyDefenderPriority { get; set; } = 1.6f;

    [SettingPropertyGroup("{=taom_mcm_group_ai_strategic_intelligence}AI Strategic Intelligence")]
    [SettingPropertyBool("{=taom_mcm_HoldCapturedForts}Good armies hold captured forts", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_HoldCapturedForts_hint}When an AI army of a Free realm takes a town or castle by siege, it stays inside for about 22 hours (12 to 30: longer for a Calculating lord, shorter for a Valorous one) instead of the 10 the game gives. It leaves early if the fortress is besieged, its cohesion drops below 40 or it has less than a day of food, but holds are not saved: after loading a save a running hold cannot end early and runs to its end. Mordor's armies hold too; other Evil realms and (by default) Neutral ones keep rolling on. Single-player only. Needs AI Strategic Intelligence on. Default: on.")]
    public bool HoldCapturedForts { get; set; } = true;

    // --- Time Acceleration ---

    // The keys themselves are NOT configured here. They are native rebindable game keys, so they
    // live in Options > Keybindings > Campaign Map (see TaomTimeControlHotKeyCategory). MCM v5 has
    // no keybind widget at all, which is why these three settings tune only the multipliers.

    [SettingPropertyGroup("{=taom_mcm_group_time_acceleration}Time Acceleration", GroupOrder = 10)]
    [SettingPropertyInteger("{=taom_mcm_FastForwardMultiplier}Fast Forward Multiplier", 1, 128, Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_FastForwardMultiplier_hint}Speed multiplier for fast-forward: the map bar fast-forward button, and the fast-forward key (Space by default; rebind under Options > Keybindings > Campaign Map). Default: 4.")]
    public int FastForwardMultiplier { get; set; } = 4;

    [SettingPropertyGroup("{=taom_mcm_group_time_acceleration}Time Acceleration")]
    [SettingPropertyInteger("{=taom_mcm_ExtraFastForwardMultiplier}Extra Fast Forward Multiplier", 1, 128, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_ExtraFastForwardMultiplier_hint}Speed multiplier for extra fast-forward: the map bar Extra Fast Forward button (the fourth time button), and the extra fast-forward key (E by default, which is also Rotate Camera Right; rebind either under Options > Keybindings > Campaign Map). Never below the Fast Forward Multiplier. Default: 8.")]
    public int ExtraFastForwardMultiplier { get; set; } = 8;

    [SettingPropertyGroup("{=taom_mcm_group_time_acceleration}Time Acceleration")]
    [SettingPropertyInteger("{=taom_mcm_CtrlSpaceMultiplier}Turbo Multiplier (hold Ctrl)", 1, 128, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CtrlSpaceMultiplier_hint}Speed multiplier while holding Ctrl and the turbo key (Space by default; rebind under Options > Keybindings > Campaign Map). Releases back to prior speed on key-up. Default: 16.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_dismount}Siege Dismount", GroupOrder = 20)]
    [SettingPropertyBool("{=taom_mcm_EnableSiegeDismount}Enable Siege Dismount", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSiegeDismount_hint}Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
    public bool EnableSiegeDismount { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_dismount}Siege Dismount")]
    [SettingPropertyInteger("{=taom_mcm_SiegeMountBehavior}Siege Mount Behavior (0=Vanilla, 1=Reserved, 2=ToInventory, 3=AutoRemount)", 0, 3, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_SiegeMountBehavior_hint}0 = Vanilla (no change). 1 = RESERVED (currently equivalent to Vanilla — full implementation deferred; would spawn the horse on the map separately). 2 = Mount moves to inventory for siege duration; player must re-equip manually after. 3 = Mount moves to inventory and is auto-restored after siege ends. Default: 3.")]
    public int SiegeMountBehavior { get; set; } = 3;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_dismount}Siege Dismount")]
    [SettingPropertyBool("{=taom_mcm_SiegeDismountDebug}Siege Dismount Debug Mode", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_SiegeDismountDebug_hint}Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
    public bool SiegeDismountDebug { get; set; } = false;

    // --- Battle Tactics / Siege Forces ---
    // The wall-battle troop picker (#734). The toggle gates only the OFFER of the screen, read at each assault, so
    // RequireRestart = false: nothing is applied once at start. A defender who presses Escape mid-battle gets
    // ExitResult.SurrenderSiege (BattleEndLogic.TryExit, v1.5.3 :255-258), and CampaignSiegeStateHandler.OnSurrenderMission
    // sets PlayerSurrender, which stops the siege component continuing the battle: the surrender takes the whole side.
    // GroupOrder 25 is free among the Battle Tactics subgroups (19-24, 27-29, 52 and 56 are taken); it is the group's
    // first property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_forces}Siege Forces", GroupOrder = 25)]
    [SettingPropertyBool("{=taom_mcm_EnableSiegeTroopPicker}Choose Troops Before Wall Battles", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSiegeTroopPicker_hint}Before an assault, or before defending a siege, open the 'Manage Troops' screen the lord's hall fight uses and choose who fights. By default, trolls start unticked; tick them to bring them. Troops you leave out take no part in the battle and suffer no losses in it. Takes effect from the next assault. As defender, pressing Escape in the battle surrenders the siege and everyone with it, reserves included. When off, every troop fights as in vanilla. Default: on.")]
    public bool EnableSiegeTroopPicker { get; set; } = true;

    // --- Battle Tactics / Creature Siege Role ---
    // Trolls in a wall battle (#735). Read ONCE per battle at the mission's AfterStart, so a change applies from the next
    // battle and RequireRestart = false: nothing is applied at start-up and nothing is half-applied mid-battle. The
    // face exclusion it adds to a creature is never removed, which is why a mid-battle toggle is not live.
    // GroupOrder 26 is free among the Battle Tactics subgroups (19-25, 27-29, 52 and 56 are taken); it is the group's first
    // property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_creature_siege_role}Creature Siege Role", GroupOrder = 26)]
    [SettingPropertyBool("{=taom_mcm_EnableCreatureSiegeRole}Trolls Stay Off Siege Ladders, Towers and Walls", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCreatureSiegeRole_hint}In a wall battle, trolls never use siege ladders, towers or siege engines. Attacking trolls break the gate down instead, and defending trolls hold the ground behind it and stay off the walls while the AI commands them; a formation you give orders to obeys them. Takes effect from the next battle. When off, trolls behave as in vanilla. Default: on.")]
    public bool EnableCreatureSiegeRole { get; set; } = true;

    // --- Battle Tactics / Staged Siege (docs/features/staged-siege.md, #1054) ---
    // Read ONCE per battle when the mission behavior decides whether to arm, so a change applies from the next battle and
    // RequireRestart = false. Ships OFF: turning the default on later needs a renamed property, because MCM keeps a saved
    // value over a changed default (the ShaderPrecompilation trap; check_mcm_default_flips.py). GroupOrder 81 is free
    // (80 is Warden's Tally; it is the group's first property, so it carries the order).

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_staged_siege}Staged Siege", GroupOrder = 81)]
    [SettingPropertyBool("{=taom_staged_siege_mcm_enable}Staged Sieges (Capture Points)", Order = 0, RequireRestart = false,
        HintText = "{=taom_staged_siege_mcm_enable_hint}In a castle assault, in the campaign or in a Custom Battle, on a scene with recorded capture points, the attackers must take points A to D in order: outside the walls, the gatehouse, the courtyard and an inner point. Each captured point brings fresh troops from both armies. In the campaign, the attacker who takes D sends the defenders back to the lord's hall, and taking the hall's last room (point E) wins the castle. In a Custom Battle, taking D wins the battle. The defender who loses D loses. Scenes without recorded points stay vanilla, and in a staged siege the Reinforcement Wave Count option does not apply. Takes effect from the next battle. Default: off.")]
    public bool EnableStagedSiege { get; set; } = false;

    // --- Battle Tactics / Siege Prop Diagnostics ---
    // Diagnostic only, no gameplay effect. The engine reports NOTHING when a rock pile or ammo
    // barrel is unusable, so this turns that silence into one log line per prop.

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_prop_diagnostics}Siege Prop Diagnostics", GroupOrder = 23)]
    [SettingPropertyBool("{=taom_mcm_EnableSiegePropDiagnostics}Enable Siege Prop Diagnostics", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSiegePropDiagnostics_hint}Log why rock piles and arrow barrels can or cannot be used in the current mission. Diagnostic only — changes no gameplay. Off by default.")]
    public bool EnableSiegePropDiagnostics { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_siege_prop_diagnostics}Siege Prop Diagnostics")]
    [SettingPropertyBool("{=taom_mcm_SiegePropDiagnosticsVerbose}Siege Prop Diagnostics Verbose", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_SiegePropDiagnosticsVerbose_hint}Log every prop, including working ones. Off = log only props that are unusable, plus the summary.")]
    public bool SiegePropDiagnosticsVerbose { get; set; } = false;

    // --- Battle Tactics / Crew Platform Diagnostics ---
    // Instrumentation only (#627): gates the crew-platform decision, layout, status and summary lines on BOTH the
    // war elephant's howdah and the mumakil's war tower, never behaviour. On by default while the platforms are
    // tested (Mike, 2026-09-19); HowdahDiagnosticsSettingsProvider falls back to the same value and a test pins both.
    // The PROPERTY NAME stays EnableHowdahDiagnostics although the label widened: MCM's json2 persists per property,
    // so renaming it would hand every existing install a fresh default (CLAUDE.md, the ShaderPrecompilation trap).
    // RequireRestart = false: the platforms read it every sample.
    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_crew_platform_diagnostics}Crew Platform Diagnostics", GroupOrder = 19)]
    [SettingPropertyBool("{=taom_mcm_EnableHowdahDiagnostics}Log Crew Platform Diagnostics", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableHowdahDiagnostics_hint}Write the war elephant howdah's and the mumakil war tower's diagnostics to the TAOM log: why a rider did or did not get a howdah, each platform's layout and floor clearance over its beast, a status line every 5 seconds per platform (beast speed against its walking speed, platform drift, how many archers are seated and how many can reach a target) and a battle summary. Diagnostic only, changes no gameplay. On by default while the platforms are being tested.")]
    public bool EnableHowdahDiagnostics { get; set; } = true;

    // --- Enlistment ---
    // Master switch for the whole serve-as-a-soldier feature. OFF removes the enlist dialog line,
    // so no new service can start. Turning it off MID-SERVICE does not simply freeze the feature:
    // the enlisted player is parked hidden and inactive next to their commander, and the code that
    // un-hides them is the same code this switch disables — so a plain "stop running" would strand
    // them invisible on the map with no menu and no way out. The reconciler therefore performs one
    // honourable discharge and hands the player back properly: on the next hourly tick, or, when the
    // player is in a battle, once that battle has ended (#1021).

    [SettingPropertyGroup("{=taom_mcm_group_enlistment}Enlistment", GroupOrder = 41)]
    [SettingPropertyBool("{=taom_mcm_EnableEnlistment}Enable Enlistment", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableEnlistment_hint}Serve as a soldier in a lord's party — enlist, take a rank, draw wages, follow the column into battle. Turn OFF to remove the feature: the option to enlist disappears, and if you are already serving you are released honourably (you keep your pay and your gear) rather than being left parked on the map. Takes effect within three in-game hours (in a battle, once it ends); no restart. Default ON.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_enlistment}Enlistment", GroupOrder = 41)]
    [SettingPropertyBool("{=taom_mcm_OfferLeaveOnArrival}Offer Leave When The Column Halts", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_OfferLeaveOnArrival_hint}When your company stops at a town, castle or village, ask whether you want a few hours to yourself. The popup exists because the wait menu runs at full speed: a lord's stop is a few campaign hours, which is about two real seconds, and the 'Take leave in the settlement' menu option appears and vanishes inside it. Turn this off and that menu option still works. Takes effect immediately; no restart. Default ON.")]
    public bool OfferLeaveOnArrival { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_enlistment}Enlistment/{=taom_mcm_group_diagnostics}Diagnostics", GroupOrder = 42)]
    [SettingPropertyBool("{=taom_mcm_EnableEnlistmentTrace}Enable Enlistment Trace", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableEnlistmentTrace_hint}Off by default: turn it on to record the routine enlistment trace ([EnlistDiag] TICK, SYNC ok, PARK ok, RESTORE ok) in the TAOM debug log when you are reproducing an enlistment problem for a bug report. It is roughly ten times the volume of the ordinary enlistment log, so turn it off again afterwards. Real faults are always logged regardless of this setting, so [EnlistDiag] lines still appear for them when it is off. Takes effect immediately; no restart.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions", GroupOrder = 43)]
    [SettingPropertyBool("{=taom_mcm_EnableFieldCommission}Enable Battlefield Promotions", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFieldCommission_hint}Soldiers who do the killing in a hard-won battle can be promoted into named companions. Turn OFF to stop it entirely: no merit is earned, no promotion is ever offered. Companions you already promoted stay exactly as they are, and banked merit is kept in case you turn it back on. Takes effect immediately; no restart. Default ON.")]
    public bool EnableFieldCommission { get; set; } = true;

    // Renamed from FieldCommissionMaxOffersPerBattle (#1017): its default went 1 to 2 in v2.0.21 (14160f0a4) under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 1.
    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyInteger("{=taom_mcm_FieldCommissionOffersPerBattle}Offers Per Battle", 1, 20, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FieldCommissionOffersPerBattle_hint}The most promotion prompts a single won battle may raise. Each prompt pauses the game, so a large number after a large battle means a long queue of dialogs. Merit above the cap is not lost — it carries to the next battle. Default: 2. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int FieldCommissionOffersPerBattle { get; set; } = 2;

    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyFloatingInteger("{=taom_mcm_FieldCommissionRatioThreshold}Fair-Fight Ratio", 0.1f, 3.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FieldCommissionRatioThreshold_hint}How outnumbered you must be for a battle to count. Your party's healthy troops divided by the whole enemy side must come in UNDER this. 1.00 = only fights where you are outnumbered; higher = easier fights still earn merit. Default: 1.30.")]
    public float FieldCommissionRatioThreshold { get; set; } = 1.3f;

    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyInteger("{=taom_mcm_FieldCommissionMeritPerKill}Merit Per Kill", 1, 10, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FieldCommissionMeritPerKill_hint}Merit a troop type banks for each enemy its soldiers kill in a qualifying battle. Higher = faster promotions. Default: 1.")]
    public int FieldCommissionMeritPerKill { get; set; } = 1;

    // Renamed from FieldCommissionMeritThreshold (#1017): its default went 8 to 32 in v2.0.21 (14160f0a4) under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 8.
    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyInteger("{=taom_mcm_FieldCommissionMeritToPromote}Merit To Promote", 1, 100, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FieldCommissionMeritToPromote_hint}Merit a troop type must bank before it earns a promotion offer. Note merit is pooled per troop TYPE, not per soldier — a large stack of one type reaches the bar faster than a small one. Higher = rarer promotions. Default: 32. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int FieldCommissionMeritToPromote { get; set; } = 32;

    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyInteger("{=taom_mcm_FieldCommissionRetainerAllowance}Companions Over The Limit", 0, 10, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_FieldCommissionRetainerAllowance_hint}How many promoted companions you may take beyond your clan tier's companion limit. 0 = the limit is strict and an offer waits until you have room. Default: 0.")]
    public int FieldCommissionRetainerAllowance { get; set; } = 0;

    [SettingPropertyGroup("{=taom_mcm_group_battlefield_promotions}Battlefield Promotions")]
    [SettingPropertyBool("{=taom_mcm_EnableFieldCommissionDiagnostics}Promotion Diagnostics", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFieldCommissionDiagnostics_hint}Write a detailed [FieldCommission] trace to the TAOM debug log — which battles counted, which troops earned merit, which offers were raised, and every completed promotion. Turn this on before reproducing a promotion problem so the log you send answers it. Real faults are logged regardless of this setting. Off by default.")]
    public bool EnableFieldCommissionDiagnostics { get; set; } = false;

    // --- Messengers ---

    [SettingPropertyGroup("{=taom_mcm_group_messengers}Messengers", GroupOrder = 25)]
    [SettingPropertyBool("{=taom_mcm_EnableMessengers}Enable Messengers", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMessengers_hint}Send paid messengers to heroes. The price grows with distance. They travel for several days and trigger a conversation on arrival. Disable to grey out the encyclopedia button and stop the dialog line.")]
    public bool EnableMessengers { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_messengers}Messengers")]
    [SettingPropertyInteger("{=taom_mcm_MessengerGoldCost}Base Gold Cost", 10, 500, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_MessengerGoldCost_hint}Denar price of the shortest trip. The price grows with the distance to the target, and is higher again when the target is at war with you, when you have never met them, and when they sit in a besieged town or castle. A stranger costs more but is never refused. The per-day rate and the multipliers live in messenger_config.json.")]
    public int MessengerGoldCost { get; set; } = 50;

    [SettingPropertyGroup("{=taom_mcm_group_messengers}Messengers")]
    [SettingPropertyInteger("{=taom_mcm_MessengerTravelDays}Travel Days", 1, 10, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_MessengerTravelDays_hint}In-game days a messenger spends in transit before arriving at the target. Speed scales to map size. The days also scale the price.")]
    public int MessengerTravelDays { get; set; } = 3;

    [SettingPropertyGroup("{=taom_mcm_group_messengers}Messengers")]
    [SettingPropertyBool("{=taom_mcm_MessengerAccidents}Enable Accidents", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_MessengerAccidents_hint}Random ambush chance during travel. The base hourly probability lives in messenger_config.json (default 0.2%).")]
    public bool MessengerAccidents { get; set; } = true;

    // --- Battle Tactics / Mixed Formations ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_mixed_formations}Mixed Formations", GroupOrder = 21)]
    [SettingPropertyBool("{=taom_mcm_EnableMixedFormations}Enable Mixed Formations", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMixedFormations_hint}Master toggle. When off, formations use vanilla positioning. When on, formations with mixed melee + ranged units are reordered per the chosen layout while holding position.")]
    public bool EnableMixedFormations { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_mixed_formations}Mixed Formations")]
    [SettingPropertyInteger("{=taom_mcm_MixedFormationsDefaultLayout}Default Layout (0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard)", 0, 3, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_MixedFormationsDefaultLayout_hint}Default layout auto-applied to mixed-class formations (>=5 minority units AND >=20% minority share AND >=10 total units). 0=Infantry front + Ranged back. 1=Ranged front + Infantry back. 2=Ranged on the wings, Infantry in the center. 3=Checkerboard. Default: 0.")]
    public int MixedFormationsDefaultLayout { get; set; } = 0;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_mixed_formations}Mixed Formations")]
    [SettingPropertyText("{=taom_mcm_MixedFormationsCycleHotkey}Cycle Layout Hotkey", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_MixedFormationsCycleHotkey_hint}Bannerlord InputKey name. Pressing this while a formation is selected cycles its layout to the next; pressing while no formation is selected cycles all formations. Default: L.")]
    public string MixedFormationsCycleHotkey { get; set; } = "L";

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_mixed_formations}Mixed Formations")]
    [SettingPropertyBool("{=taom_mcm_MixedFormationsDebug}Mixed Formations Debug Mode", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_MixedFormationsDebug_hint}Writes two diagnostic lines, the auto-assigned default layout and the cycled layout, to the TAOM log file. Nothing is shown on the HUD.")]
    public bool MixedFormationsDebug { get; set; } = false;

    // --- Fief Management ---

    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management", GroupOrder = 26)]
    [SettingPropertyBool("{=taom_mcm_EnableFiefManagement}Enable Fief Management", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFiefManagement_hint}Master toggle. When off, the F6 hotkey and the Fiefs button on the map bar are inert and the carousel options are disabled. Effective immediately at runtime. Default: true.")]
    public bool EnableFiefManagement { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management")]
    [SettingPropertyBool("{=taom_mcm_AllowRemoteBuildingQueue}Allow Remote Building Queue", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_AllowRemoteBuildingQueue_hint}When on, you can manage any owned fief from anywhere via F6 or the Fiefs button on the map bar. When off, the Manage option is disabled unless you are physically at the selected fief. Default: true.")]
    public bool AllowRemoteBuildingQueue { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management")]
    [SettingPropertyBool("{=taom_mcm_FiefManagementDebug}Fief Management Debug Mode", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FiefManagementDebug_hint}Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent.")]
    public bool FiefManagementDebug { get; set; } = false;

    // The group's first property carries GroupOrder; 0 sorts it above the parent's own settings (MCM ignores a GroupOrder on a later one).
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_postern_gate}Postern Gate", GroupOrder = 0)]
    [SettingPropertyBool("{=taom_mcm_EnablePosternGate}Postern Gate Cuts Siege Losses", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnablePosternGate_hint}A Postern Gate (a Hidden Door, Culvert, Sally Port, Hidden Way or Bolt-Hole, by culture) in a besieged fief cuts the troops lost breaking into or out of the siege there: by default 50, 67 and 75 percent by building level (postern_gate.json). When off, the building still exists but the cut does not apply. Takes effect at the next break-in or break-out. Default: on.")]
    public bool EnablePosternGate { get; set; } = true;

    // GroupOrder 1 sorts it just after the Postern Gate group; the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_from_the_ashes}From the Ashes", GroupOrder = 1)]
    [SettingPropertyBool("{=taom_mcm_EnableFromTheAshes}From the Ashes", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFromTheAshes_hint}Lets your company rebuild a looted village of your own realm from the village menu, and lend its labour to a construction project in a fief you own from the keep menu (from_the_ashes.json holds the hours, labour and materials). When off, those two options are hidden; a job already under way still finishes or stops normally. Single-player only. Default: on.")]
    public bool EnableFromTheAshes { get; set; } = true;

    // GroupOrder 2 sorts it after the Postern Gate (0) and From the Ashes (1) groups; the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_reeve_s_ledger}Reeve's Ledger", GroupOrder = 2)]
    [SettingPropertyBool("{=taom_mcm_EnableReevesLedger}Reeve's Ledger", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableReevesLedger_hint}On the town management window: the fief's name as the title, a Profit row with a daily breakdown, a Patrol row for towns, and tooltips on the stash and prison icons. When off, the title, rows and icons are vanilla; the step buttons have their own switch. Applies the next time the window opens. Default: on.")]
    public bool EnableReevesLedger { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_reeve_s_ledger}Reeve's Ledger")]
    [SettingPropertyBool("{=taom_mcm_EnableReserveSteps}Reserve Step Buttons", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableReserveSteps_hint}Adds a button beside the reserve's plus button to put gold into the reserve in one click, and one to take a step back out, refunded in full with no penalty. Single-player only. Applies the next time the window opens. Default: on.")]
    public bool EnableReserveSteps { get; set; } = true;

    // GroupOrder 3 sorts it after the Postern Gate (0), From the Ashes (1) and Reeve's Ledger (2) groups; the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_namegiving}Namegiving", GroupOrder = 3)]
    [SettingPropertyBool("{=taom_mcm_EnableNamegiving}Namegiving", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableNamegiving_hint}Lets the ruler of your realm rename its towns, castles and villages from the Fiefs tab of the kingdom screen for influence, and lets four fiefs take an older or darker name while the matching side holds them. A name you chose is never overwritten. Single-player only. Switching it off gives those fiefs their map names back; a name you chose stays. The button follows the next time the kingdom screen opens; the older names change at the next capture, load or change of realm. Default: on.")]
    public bool EnableNamegiving { get; set; } = true;

    // GroupOrder 5 sorts it after the Postern Gate (0), From the Ashes (1), Reeve's Ledger (2), Namegiving (3) and Keep Wardens (4) groups; the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_watch_silver}Watch-Silver", GroupOrder = 5)]
    [SettingPropertyBool("{=taom_mcm_EnableWatchSilver}Watch-Silver", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableWatchSilver_hint}Lets you talk to the governor of a town or castle your clan owns, inside that fief, and pay silver to the watch to raise its loyalty. Each offer shows its price, and there is a ceiling on how far silver can raise loyalty. When off, the line is hidden. Single-player only. Default: on.")]
    public bool EnableWatchSilver { get; set; } = true;

    // GroupOrder 6 sorts it after the Watch-Silver group (5); the group's first property carries it. Ships ON and is read
    // once per spawn pass, so a change applies from the next visit to a lord's hall.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_hall_seats}Hall Seats", GroupOrder = 6)]
    [SettingPropertyBool("{=taom_mcm_EnableHallSeats}Hall Seats", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableHallSeats_hint}A lord's hall shows only as many lords as it has places for, instead of packing every lord in the town or castle onto one spot. The settlement's owner, the ruler, the governor, your own clan, quest givers and the lord you open a conversation with from the town menu always appear; the other places go to the owner's clan first, then clan leaders, then the strongest clans. The lords left out are still in the settlement. Takes effect from the next visit. Default: on.")]
    public bool EnableHallSeats { get; set; } = true;

    // --- Character Screen / Loremaster's Line (#877) ---

    // GroupOrder 44 orders the "Loremaster's Line" subgroup inside "Character Screen"; the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_character_screen}Character Screen/{=taom_mcm_group_loremaster_s_line}Loremaster's Line", GroupOrder = 44)]
    [SettingPropertyBool("{=taom_ll_mcm_enable}Loremaster's Line", Order = 0, RequireRestart = false,
        HintText = "{=taom_ll_mcm_enable_hint}On the character screen, marks the level past which the selected skill learns at its slowest: a red arrow and a red band to the end of the bar; hover the band for the details. Also adds a line to the learning limit tooltip. Applies the next time the character screen opens. Default: on.")]
    public bool EnableLoremastersLine { get; set; } = true;

    // GroupOrder 4 sorts it after the Namegiving group (3); the group's first property carries it.
    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_keep_wardens}Keep Wardens", GroupOrder = 4)]
    [SettingPropertyBool("{=taom_keep_wardens_label}Castle Patrols", Order = 0, RequireRestart = false,
        HintText = "{=taom_keep_wardens_hint}A castle with a built Guard House fields one patrol party of its owner's culture at a time, as a town does: stronger at each Guard House level. A lost patrol is replaced after 10, 8 or 6 days. While a castle's patrol is alive, the castle also gains security each day: 1, 1.5 or 2 by Guard House level (both in castle_patrols.json). When off, no new castle patrol is queued, and the game's daily check removes the castle patrols already out. Default: on.")]
    public bool EnableCastlePatrols { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_fief_management}Fief Management/{=taom_mcm_group_keep_wardens}Keep Wardens")]
    [SettingPropertyBool("{=taom_keep_wardens_build_label}Guard House Build Priority", Order = 1, RequireRestart = false,
        HintText = "{=taom_keep_wardens_build_hint}The AI builds a Guard House in its towns and castles ahead of other buildings: always while it has none, and about 60 percent of the time while it can still be upgraded (guardHousePriorityChance in castle_patrols.json). Your own fiefs are never touched. Default: on.")]
    public bool EnableGuardHouseBuildPriority { get; set; } = true;

    // --- Inventory / Quick Actions ---

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions", GroupOrder = 30)]
    [SettingPropertyBool("{=taom_mcm_EnableQuickActions}Enable Quick Actions", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableQuickActions_hint}Master toggle. When off, inventory 'Sell All' uses vanilla. When on, it opens a 4-option menu.")]
    public bool EnableQuickActions { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions")]
    [SettingPropertyBool("{=taom_mcm_EnableInventorySearch}Enable Inventory Search", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableInventorySearch_hint}Inventory search box visibility; persists per save and reconciles to MCM each campaign frame.")]
    public bool EnableInventorySearch { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions")]
    [SettingPropertyBool("{=taom_tm_mcm_enable}Highlight better items", Order = 2, RequireRestart = false,
        HintText = "{=taom_tm_mcm_enable_hint}Tint inventory items that beat the selected character's gear: green for an upgrade, yellow for a sidegrade (more total armour, less in its own slot).")]
    public bool EnableTelcharsMark { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions")]
    [SettingPropertyBool("{=taom_tm_mcm_filter_default}Show only better items by default", Order = 3, RequireRestart = false,
        HintText = "{=taom_tm_mcm_filter_default_hint}Tick \"Show only better items\" each time the inventory opens.")]
    public bool TelcharsMarkShowOnlyUpgradesByDefault { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_damaged}Sell Damaged", GroupOrder = 30)]
    [SettingPropertyDropdown("{=taom_mcm_DamagedQualityDropdown}Damage Threshold Preset", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_DamagedQualityDropdown_hint}Items at or below this damage level are sold. Pristine = unused (sentinel). Default: Moderate (-20%).")]
    public Dropdown<string> DamagedQualityDropdown { get; set; } = new Dropdown<string>(
        new[] { "Pristine", "Slight (-10%)", "Moderate (-20%)", "Heavy (-40%)" }, 2);

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_damaged}Sell Damaged")]
    [SettingPropertyFloatingInteger("{=taom_mcm_DamagedThreshold}Custom Damage Threshold", -1.0f, 0.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_DamagedThreshold_hint}Custom threshold. Only used when 'Use Custom Threshold' is on. Default: -0.20.")]
    public float DamagedThreshold { get; set; } = -0.2f;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_damaged}Sell Damaged")]
    [SettingPropertyBool("{=taom_mcm_UseCustomThreshold}Use Custom Threshold", Order = 2, RequireRestart = false, HintText = "{=taom_mcm_UseCustomThreshold_hint}Toggle dropdown vs custom value above.")]
    public bool UseCustomThreshold { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_damaged}Sell Damaged")]
    [SettingPropertyBool("{=taom_mcm_ExcludeDamagedHorses}Exclude Damaged Horses", Order = 4, RequireRestart = false, HintText = "{=taom_mcm_ExcludeDamagedHorses_hint}Skip horses/mounts when selling damaged. Default: true.")]
    public bool ExcludeDamagedHorses { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_low_value}Sell Low Value", GroupOrder = 31)]
    [SettingPropertyInteger("{=taom_mcm_LowValueThreshold}Low Value Threshold (denars)", 1, 10000, Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_LowValueThreshold_hint}Items at or below this denars value are sold. Default: 100.")]
    public int LowValueThreshold { get; set; } = 100;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_low_value}Sell Low Value")]
    [SettingPropertyBool("{=taom_mcm_ExcludeLowValueFood}Exclude Low Value Food", Order = 2, RequireRestart = false, HintText = "{=taom_mcm_ExcludeLowValueFood_hint}Skip food items. Default: true.")]
    public bool ExcludeLowValueFood { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_low_value}Sell Low Value")]
    [SettingPropertyBool("{=taom_mcm_ExcludeLowValueHorses}Exclude Low Value Horses", Order = 3, RequireRestart = false, HintText = "{=taom_mcm_ExcludeLowValueHorses_hint}Skip horses/mounts. Default: true.")]
    public bool ExcludeLowValueHorses { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_sell_low_value}Sell Low Value")]
    [SettingPropertyBool("{=taom_mcm_ExcludeLowValueTradeGoods}Exclude Low Value Trade Goods", Order = 4, RequireRestart = false, HintText = "{=taom_mcm_ExcludeLowValueTradeGoods_hint}Skip trade goods. Default: false.")]
    public bool ExcludeLowValueTradeGoods { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_misc}Misc", GroupOrder = 32)]
    [SettingPropertyBool("{=taom_mcm_QuickActionsShowConfirmation}Show Confirmation Dialog", Order = 0, RequireRestart = false, HintText = "{=taom_mcm_QuickActionsShowConfirmation_hint}Ask for confirmation before bulk-selling. Default: true.")]
    public bool QuickActionsShowConfirmation { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_misc}Misc")]
    [SettingPropertyBool("{=taom_mcm_QuickActionsPlaySounds}Play Sounds", Order = 1, RequireRestart = false, HintText = "{=taom_mcm_QuickActionsPlaySounds_hint}Play 'event:/ui/transfer' chime after each batch action. Default: true.")]
    public bool QuickActionsPlaySounds { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_quick_actions}Quick Actions/{=taom_mcm_group_misc}Misc")]
    [SettingPropertyBool("{=taom_mcm_QuickActionsDebug}Quick Actions Debug Mode", Order = 2, RequireRestart = false, HintText = "{=taom_mcm_QuickActionsDebug_hint}Show diagnostic [QuickActions] HUD messages. Off = file log only.")]
    public bool QuickActionsDebug { get; set; } = false;

    // --- Inventory / Equipment Presets ---

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_equipment_presets}Equipment Presets", GroupOrder = 33)]
    [SettingPropertyBool("{=taom_mcm_EnableEquipmentPresets}Enable Equipment Presets", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableEquipmentPresets_hint}Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
    public bool EnableEquipmentPresets { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_equipment_presets}Equipment Presets")]
    [SettingPropertyInteger("{=taom_mcm_MaxPresetsPerCharacter}Max Presets Per Character", 1, 20, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_MaxPresetsPerCharacter_hint}Maximum saved presets per hero. Default: 10.")]
    public int MaxPresetsPerCharacter { get; set; } = 10;

    [SettingPropertyGroup("{=taom_mcm_group_inventory}Inventory/{=taom_mcm_group_equipment_presets}Equipment Presets")]
    [SettingPropertyBool("{=taom_mcm_EquipPresetsDebug}Equipment Presets Debug Mode", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EquipPresetsDebug_hint}Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
    public bool EquipPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Smart Cavalry ---

    // Renamed from EnableSmartCavalryAI (#1017): it was switched off in v2.0.7 (625396dde) while war-elephant
    // interaction is tuned, but a saved "on" survived under the same name.
    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry", GroupOrder = 22)]
    [SettingPropertyBool("{=taom_mcm_EnableSmartCavalryCharge}Enable Smart Cavalry AI", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSmartCavalryCharge_hint}Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior. Default OFF while war-elephant interaction is being tuned. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public bool EnableSmartCavalryCharge { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyBool("{=taom_mcm_SmartCavalryAvoidFriendlies}Enable Friendly Collision Avoidance", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryAvoidFriendlies_hint}When charging, cavalry will reroute around friendly infantry on the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
    public bool SmartCavalryAvoidFriendlies { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SmartCavalryChargeStrictness}Charge Formation Strictness", 0.0f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryChargeStrictness_hint}How close riders must be to their slots before the line charges, and before a reform counts as done: the average rider may be 10 m off at 0, 4.4 m at the 0.7 default, 2 m at 1. Max Line-Up Seconds launches the charge regardless when it runs out.")]
    public float SmartCavalryChargeStrictness { get; set; } = 0.7f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SmartCavalryReformDistance}Reform Distance After Charge", 10f, 80f, "#0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryReformDistance_hint}Meters past the target before cavalry reforms a new line. Larger = wider passthrough sweep. Default 25.")]
    public float SmartCavalryReformDistance { get; set; } = 25f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SmartCavalryLineSpacing}Charge Line Spacing Multiplier", 0.8f, 3.0f, "#0.0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryLineSpacing_hint}Multiplier on default unit spacing during line formation. 1.0 = vanilla. 1.2 (default) = slightly wider line for cleaner charge.")]
    public float SmartCavalryLineSpacing { get; set; } = 1.2f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SmartCavalryMaxLineUpSeconds}Max Line-Up Seconds", 1f, 15f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryMaxLineUpSeconds_hint}Longest the cavalry spend lining up before they charge anyway, and before they re-charge after a reform. Lower is snappier with a looser line. Default 4.")]
    public float SmartCavalryMaxLineUpSeconds { get; set; } = 4f;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_smart_cavalry}Smart Cavalry")]
    [SettingPropertyBool("{=taom_mcm_SmartCavalryDebug}Smart Cavalry Debug Mode", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_SmartCavalryDebug_hint}Also write every intercepted charge order and the state it produced to the TAOM log file. State transitions are logged regardless. Nothing is drawn on the HUD.")]
    public bool SmartCavalryDebug { get; set; } = false;

    // --- Battle Tactics / Culture Doctrine ---
    // GroupOrder 52: 10, 15, 20-51 and 100 were taken as of 2026-09-16.

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_culture_doctrine}Culture Doctrine", GroupOrder = 52)]
    [SettingPropertyBool("{=taom_mcm_EnableCultureDoctrine}Enable Culture Doctrine", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCultureDoctrine_hint}Each culture's AI fights field battles with its own doctrine: Dwarves hold a shield wall on the high ground, Elves ring their archers with infantry, Rohan leads with cavalry, Mordor and Gundabad mass their infantry. Replaces the tactic list the engine's team AI chooses from; the AI still picks by weight every 5 seconds. Applies from the NEXT battle (the list cannot change once a battle has started). Your own orders are never touched; when you delegate command (F6) your army fights with its culture's doctrine. Off by default until the in-game A/B passes; tuning lives in ModuleData/culture_doctrine/culture_doctrines.json, which reloads on game restart only.")]
    public bool EnableCultureDoctrine { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_culture_doctrine}Culture Doctrine")]
    [SettingPropertyBool("{=taom_mcm_CultureDoctrineDebug}Culture Doctrine Debug Mode", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CultureDoctrineDebug_hint}Write a [Doctrine] status line to the TAOM log every 5 seconds of battle time: each team's current tactic and every formation's active behaviour and arrangement. The console command taom.tactic_status prints the same line on demand. Nothing is drawn on the HUD.")]
    public bool CultureDoctrineDebug { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_culture_doctrine}Culture Doctrine")]
    [SettingPropertyBool("{=taom_mcm_CultureDoctrineMorale}Culture Morale", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CultureDoctrineMorale_hint}Each culture's soldiers hold under fear their own way: Dwarves, Elves, Isengard, Gundabad and Dol Guldur never rout (pride, or a hatred too great), the free peoples of Rohan, Gondor and Dale and the men of the East and South break as vanilla does, and every culture carries a bravery offset on its starting morale. Needs Enable Culture Doctrine. Applies from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineMorale { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_culture_doctrine}Culture Doctrine")]
    [SettingPropertyBool("{=taom_mcm_CultureDoctrineAggression}Culture Aggression", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_CultureDoctrineAggression_hint}Each culture's soldiers fight the melee their own way: orcs attack on sight and rarely cover with a shield, Dwarves and Gondor raise shields and hold, Elves shoot straighter, Rohan commits to a charge from further out. Multiplies the engine's per-soldier AI decision values after skill has set them. Needs Enable Culture Doctrine. Applies to soldiers spawned from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineAggression { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_culture_doctrine}Culture Doctrine")]
    [SettingPropertyBool("{=taom_mcm_CultureDoctrineHorseStand}Horse Stand", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_CultureDoctrineHorseStand_hint}Dwarves, Isengard and Dale, defending outnumbered against an army of mostly horse, stand where horse cannot ride them down: a choke point, a forest, a slope, or where they are. Where the riders sit on ordinary horses, the cavalry dismount and join the shield wall with polearms ready. The AI sides only; your own army is never touched. Needs Enable Culture Doctrine. Applies from the next battle; tuning in culture_doctrines.json.")]
    public bool CultureDoctrineHorseStand { get; set; } = true;

    // --- Performance / Dead Mount Cleanup ---
    // GroupOrder 49: 10, 15, 20-48 and 100 were taken as of 2026-09-03.

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_dead_mount_cleanup}Dead Mount Cleanup", GroupOrder = 49)]
    [SettingPropertyBool("{=taom_mcm_EnableDeadMountDespawn}Despawn Dead Mounts", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableDeadMountDespawn_hint}Remove killed horses, wargs and other mounts a few seconds after they die instead of leaving them on the field. A mount is a full agent with a skeleton and a live ragdoll, so a cavalry battle piles up corpses that cost frame time and do nothing. Field battles, sieges and sally-outs only; towns, training grounds and tournaments keep their bodies. Side effect: freeing agent slots lets reinforcements arrive slightly sooner than vanilla.")]
    public bool EnableDeadMountDespawn { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_dead_mount_cleanup}Dead Mount Cleanup")]
    [SettingPropertyFloatingInteger("{=taom_mcm_DeadMountDespawnDelaySeconds}Despawn Delay (seconds)", 3f, 30f, "#0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_DeadMountDespawnDelaySeconds_hint}Seconds between a mount's death and its fade. Default 5. Below 3 the corpse pops while the death animation is still playing, so values outside 3-30 fall back to 5.")]
    public float DeadMountDespawnDelaySeconds { get; set; } = 5f;

    // --- Performance / Battle Corpses (#701) ---
    // GroupOrder 55: 10, 15, 19-54 and 100 were taken as of 2026-10-01.

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_battle_corpses}Battle Corpses", GroupOrder = 55)]
    [SettingPropertyBool("{=taom_mcm_EnableBattleCorpseCleanup}Clean Up Battle Corpses", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableBattleCorpseCleanup_hint}Fade fallen soldiers after a set time and cap how many bodies a battle keeps, whatever your Number of Corpses option says. The engine walks its whole corpse list every frame, and players reported mid-battle freezes at high corpse settings. Field battles, sieges and sally-outs only; towns, training grounds and hideouts keep their bodies. Never raises your own Number of Corpses option.")]
    public bool EnableBattleCorpseCleanup { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_battle_corpses}Battle Corpses")]
    [SettingPropertyFloatingInteger("{=taom_mcm_BattleCorpseFadeSeconds}Corpse Fade Time (seconds)", 10f, 300f, "#0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_BattleCorpseFadeSeconds_hint}Seconds a body stays on the field, counted from when it settles, before it fades. Default 60. The engine's own default is an hour, so without this a body only goes when the corpse cap pushes it out. Values outside 10-300 fall back to 60.")]
    public float BattleCorpseFadeSeconds { get; set; } = 60f;

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_battle_corpses}Battle Corpses")]
    [SettingPropertyInteger("{=taom_mcm_BattleCorpseCap}Maximum Corpses", 0, 250, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_BattleCorpseCap_hint}Most bodies a battle keeps at once; the oldest fade first. Default 25, the vanilla Low setting. If your own Number of Corpses option is lower, yours is used.")]
    public int BattleCorpseCap { get; set; } = 25;

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_battle_corpses}Battle Corpses")]
    [SettingPropertyBool("{=taom_mcm_ShowBattleSettingsAdvice}Recommend Battle Settings", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_ShowBattleSettingsAdvice_hint}On the first main menu after the game starts, offer to lower Number of Ragdolls to 5 and Number of Corpses to Low when yours are higher. Nothing changes unless you click Apply.")]
    public bool ShowBattleSettingsAdvice { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_performance}Performance/{=taom_mcm_group_battle_corpses}Battle Corpses")]
    [SettingPropertyButton("{=taom_mcm_ApplyRecommendedBattleSettingsAction}Apply Recommended Battle Settings", Order = 4, RequireRestart = false,
        Content = "{=taom_mcm_ApplyRecommendedBattleSettingsAction_button}Apply",
        HintText = "{=taom_mcm_ApplyRecommendedBattleSettingsAction_hint}Lower Number of Ragdolls to 5 and Number of Corpses to Low (25) in your game options now. A setting already lower is kept. Change them back at any time in Options, Performance.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_companion_roles}Companion Roles", GroupOrder = 27)]
    [SettingPropertyBool("{=taom_mcm_EnableCompanionRoleTooltips}Enable Companion Role Tooltips", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCompanionRoleTooltips_hint}Append detected combat role (e.g., [BOW], [INF]) to companion/troop tooltips on the party screen.")]
    public bool EnableCompanionRoleTooltips { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_companion_roles}Companion Roles")]
    [SettingPropertyBool("{=taom_mcm_EnableOOBRoleDisplay}Enable OOB Role Display", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableOOBRoleDisplay_hint}Show role indicators on hero items in the Order of Battle screen.")]
    public bool EnableOOBRoleDisplay { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_companion_roles}Companion Roles")]
    [SettingPropertyBool("{=taom_mcm_CompanionRolesDebug}Companion Roles Debug Mode", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CompanionRolesDebug_hint}Show diagnostic [CompanionRoles] messages on the in-game HUD.")]
    public bool CompanionRolesDebug { get; set; } = false;

    // --- Battle Tactics / Formation Presets ---

    // Renamed from EnableFormationPresets (#1017): it was switched off in v2.0.8 (53a8cfc21) while loading a preset
    // was unwired, but a saved "on" survived under the same name. Loading a preset shipped in v2.0.34; the in-game
    // check is still owed, so the default stays off.
    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_formation_presets}Formation Presets", GroupOrder = 28)]
    [SettingPropertyBool("{=taom_mcm_EnableOobFormationPresets}Enable Formation Presets", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableOobFormationPresets_hint}Presets save and load the formation types, captains and hero troops of the Order of Battle, per campaign. Off by default until it is checked in game; opt in to try it. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public bool EnableOobFormationPresets { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_formation_presets}Formation Presets")]
    [SettingPropertyInteger("{=taom_mcm_MaxFormationPresets}Max Formation Presets", 1, 20, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_MaxFormationPresets_hint}Maximum saved formation presets per campaign. Save attempts beyond this limit are refused with a warning. Default: 10.")]
    public int MaxFormationPresets { get; set; } = 10;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_formation_presets}Formation Presets")]
    [SettingPropertyBool("{=taom_mcm_FormationPresetsDebug}Formation Presets Debug Mode", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FormationPresetsDebug_hint}Show diagnostic [FormationPresets] messages.")]
    public bool FormationPresetsDebug { get; set; } = false;

    // --- Battle Tactics / Battle Action Bar ---

    // Renamed from EnableBattleActionBar (default on) in #970. MCM keeps a saved value per property, so
    // flipping that default would have reached fresh installs only; a new property name is what reaches
    // every install. The old key in TAOM.json is ignored.
    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_battle_action_bar}Battle Action Bar", GroupOrder = 29)]
    [SettingPropertyBool("{=taom_mcm_ShowBattleActionBar}Show Battle Action Bar", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_ShowBattleActionBar_hint}Parked: show a bar of stance buttons during field battles. The buttons record a stance but do not change formation behavior yet, and they can be clicked only while a menu shows the cursor (for example Esc). Default: off.")]
    public bool ShowBattleActionBar { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_battle_action_bar}Battle Action Bar")]
    [SettingPropertyBool("{=taom_mcm_CancelStanceOnMove}Cancel Stance On Move", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CancelStanceOnMove_hint}Auto-clear stance when the formation receives a movement order.")]
    public bool CancelStanceOnMove { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_battle_action_bar}Battle Action Bar")]
    [SettingPropertyBool("{=taom_mcm_EnableVolleyFire}Enable Volley Fire", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableVolleyFire_hint}Include 'Volley Fire' as a ranged action option.")]
    public bool EnableVolleyFire { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_battle_action_bar}Battle Action Bar")]
    [SettingPropertyBool("{=taom_mcm_BattleActionBarDebug}Battle Action Bar Debug Mode", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_BattleActionBarDebug_hint}Show diagnostic [BattleActionBar] messages.")]
    public bool BattleActionBarDebug { get; set; } = false;

    // --- Map UI / Settlement Nameplates ---

    // --- World / Bandit Scaling ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling", GroupOrder = 35)]
    [SettingPropertyBool("{=taom_mcm_EnableBanditScaling}Enable Bandit Scaling", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableBanditScaling_hint}Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below. Turning it off restores vanilla's live numbers at once (including its 2-party minimum for a camp to count as infested) and stops scaling future spawns; it deletes no bandit party already on the map and culls no hideout above the cap, so a swamped campaign thins only as they are cleared.")]
    public bool EnableBanditScaling { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyFloatingInteger("{=taom_mcm_BanditDensityCurve}Density Curve", 0.0f, 5.0f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_BanditDensityCurve_hint}Multiplier on hideout count + parties-per-hideout at PlayerProgress=1.0. Curve: 1 + curve * progress. 0 = vanilla density throughout. 1.5 (default) = up to 2.5x density in endgame. Both caps below bind first.")]
    public float BanditDensityCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyFloatingInteger("{=taom_mcm_BanditPartySizeCurve}Party Size Curve", 0.0f, 5.0f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_BanditPartySizeCurve_hint}Multiplier on bandit party troop counts at PlayerProgress=1.0. Vanilla already scales 0.4 -> 1.2; this is a final multiplier on top. 1.5 (default) = up to 2.5x bandit party sizes in endgame. Each troop stack is capped at its party template's max_value, which binds before 2.5x is reached.")]
    public float BanditPartySizeCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyFloatingInteger("{=taom_mcm_BanditBossFightCurve}Hideout First Fight Curve", 0.0f, 5.0f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_BanditBossFightCurve_hint}Multiplier on the FIRST fight's troop count inside hideouts at PlayerProgress=1.0. 1.5 (default) = up to 2.5x bandits in the camp-clearing phase of a hideout assault in endgame. The boss fight is not scaled: it is exactly the boss plus Hideout Boss Bodyguards below.")]
    public float BanditBossFightCurve { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyInteger("{=taom_mcm_BanditMaxHideoutsPerFaction}Max Hideouts Per Faction Cap", 1, 100, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_BanditMaxHideoutsPerFaction_hint}Hard cap on hideouts per bandit faction regardless of scaling curve. Vanilla = 9. Default: 100 (effectively the physical hideout count per faction).")]
    public int BanditMaxHideoutsPerFaction { get; set; } = 100;

    // Renamed from BanditMaxPartiesPerHideout (#1017): its default went 3 to 6 in v2.0.29 under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 3, which pins vanilla.
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyInteger("{=taom_mcm_BanditPartiesPerHideoutCap}Max Parties Per Hideout Cap", 1, 20, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_BanditPartiesPerHideoutCap_hint}Hard cap on bandit parties per hideout regardless of scaling curve. Vanilla = 3, which is also the floor: a cap at or below 3 pins the value at vanilla and the Density Curve cannot move it. Default: 6. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int BanditPartiesPerHideoutCap { get; set; } = 6;

    // Renamed from BanditInitialHideoutsPerFaction (#1017): its default went 14 to 7 in v2.0.29 under the same
    // name, and MCM loads the saved value by name, so every player who saved before then kept 14.
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyInteger("{=taom_mcm_BanditStartingHideoutsPerFaction}Initial Hideouts Per Faction", 1, 30, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_BanditStartingHideoutsPerFaction_hint}Hideouts each bandit faction starts with on a NEW campaign; read once at world-gen, so changing it does nothing to a campaign already in progress. TAOM has 8 bandit factions, so this is a per-faction target, x8 on the map, bounded by each faction's physical hideout count (three have only 10). Vanilla = 7. Default: 7. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int BanditStartingHideoutsPerFaction { get; set; } = 7;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_scaling}Bandit Scaling")]
    [SettingPropertyInteger("{=taom_mcm_BanditHideoutBossBodyguards}Hideout Boss Bodyguards", 0, 10, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_BanditHideoutBossBodyguards_hint}Soldiers standing with the hideout boss in the final fight, on both the daytime assault and the night sneak-in. The fight is exactly the boss plus this many (0 = the boss alone), whether you take the duel or fight them all; the rest of the camp is cleared in the first phase. Applies with Enable Bandit Scaling off too. Vanilla fields 5 to 10 with the boss. Default: 4.")]
    public int BanditHideoutBossBodyguards { get; set; } = 4;

    // --- World / Recruitment Alignment ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_recruitment_alignment}Recruitment Alignment", GroupOrder = 36)]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentRecruitment}Enable Recruitment Alignment Block", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentRecruitment_hint}When enabled, a recruiter cannot recruit volunteers at a settlement controlled by an opposed-alignment kingdom (Free vs Evil). Alignment comes from execution/alignment.json, keyed by the kingdom you serve. Neutral factions (Umbar etc.) never block. When off, recruitment is vanilla. This is the default for new campaigns. With the Alignment rules start option on Standard, a campaign follows this switch live; Strict or Relaxed fixes all four alignment switches for that campaign. Older saves follow this setting.")]
    public bool EnableAlignmentRecruitment { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_recruitment_alignment}Recruitment Alignment")]
    [SettingPropertyBool("{=taom_mcm_AlignmentRecruitmentGoodRejectsEvilOnly}Only Good Rejects Evil", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_AlignmentRecruitmentGoodRejectsEvilOnly_hint}When ON, only a Free-aligned recruiter is blocked from Evil-controlled settlements; Evil recruiters may recruit anywhere, and Alignment Desertion then removes no Free troops from Evil lords or their garrisons at all, not only the ones they recruit. When OFF (default), the block is symmetric: Free and Evil each refuse the other, and opposed troops desert either way.")]
    public bool AlignmentRecruitmentGoodRejectsEvilOnly { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_recruitment_alignment}Recruitment Alignment")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentRecruitmentPlayer}Apply To Player", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentRecruitmentPlayer_hint}When ON (default), YOU are blocked from recruiting in opposed-alignment settlements. When OFF, you may recruit anyone regardless of alignment (AI lords are still gated if 'Apply To AI Lords' is on). The master 'Enable Recruitment Alignment Block' toggle off disables the whole feature for everyone.")]
    public bool EnableAlignmentRecruitmentPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_recruitment_alignment}Recruitment Alignment")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentRecruitmentAi}Apply To AI Lords", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentRecruitmentAi_hint}When ON (default), AI lords are also blocked from recruiting in opposed-alignment settlements. When OFF, AI recruits freely (you are still gated if 'Apply To Player' is on).")]
    public bool EnableAlignmentRecruitmentAi { get; set; } = true;

    // --- World / Marriage Alignment ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_marriage_alignment}Marriage Alignment", GroupOrder = 50)]
    [SettingPropertyBool("{=taom_mcm_EnableMarriageAlignment}Enable Marriage Alignment Block", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMarriageAlignment_hint}When enabled, a Free-aligned hero cannot marry an Evil-aligned one (no Gondorian weds an orc). Alignment comes from execution/alignment.json, keyed by the hero's CULTURE. Neutral cultures (Khand, Umbar, Shaghana, Abanissa) marry anyone; Dunland is Evil. When off, marriage is vanilla. This is the default for new campaigns. With the Alignment rules start option on Standard, a campaign follows this switch live; Strict or Relaxed fixes all four alignment switches for that campaign. Older saves follow this setting.")]
    public bool EnableMarriageAlignment { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_marriage_alignment}Marriage Alignment")]
    [SettingPropertyBool("{=taom_mcm_EnableMarriageAlignmentPlayer}Apply To Player", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMarriageAlignmentPlayer_hint}When ON (default), YOUR clan is also held to the rule — you and your siblings/children cannot marry across the Free/Evil line. When OFF, the player clan marries freely (AI lords are still gated if 'Apply To AI Lords' is on).")]
    public bool EnableMarriageAlignmentPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_marriage_alignment}Marriage Alignment")]
    [SettingPropertyBool("{=taom_mcm_EnableMarriageAlignmentAi}Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMarriageAlignmentAi_hint}When ON (default), AI lords are blocked from marrying across the Free/Evil line. When OFF, AI marries freely (you are still gated if 'Apply To Player' is on).")]
    public bool EnableMarriageAlignmentAi { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_marriage_alignment}Marriage Alignment")]
    [SettingPropertyBool("{=taom_mcm_MarriageAlignmentSteerAiSearch}Steer AI Partner Search", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_MarriageAlignmentSteerAiSearch_hint}When ON (default), AI lords look for a partner among compatible clans instead of drawing uniformly from every clan in the world. Vanilla's draw wastes the day whenever it lands on a blocked clan, which would make Free factions (Gondor, Rohan, Dale) marry far less often and run short of heirs. Turn OFF to keep the block but restore vanilla's draw.")]
    public bool MarriageAlignmentSteerAiSearch { get; set; } = true;

    // --- World / Wanderer Allegiance ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_wanderer_allegiance}Wanderer Allegiance", GroupOrder = 51)]
    [SettingPropertyBool("{=taom_mcm_EnableWandererAllegiance}Enable Wanderer Allegiance", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableWandererAllegiance_hint}When enabled, a wanderer will not take service across the Free/Evil line: Free-culture wanderers (Gondor, Rohan, the Elves, Erebor and the rest) refuse a player who serves Sauron, and Evil-culture wanderers refuse a player on the Free side. Your side is the kingdom you serve (execution/alignment.json), falling back to your clan's culture when you serve no kingdom. Neutral cultures serve anyone and a Neutral player is refused by nobody. Companions already in your clan are never affected. When off, hiring is vanilla.")]
    public bool EnableWandererAllegiance { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_wanderer_allegiance}Wanderer Allegiance")]
    [SettingPropertyDropdown("{=taom_mcm_WandererAllegianceScope}Who Refuses", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_WandererAllegianceScope_hint}All wanderers (default): every wanderer, by culture. Named companions only: just the lore characters from named_companions.xml (Aragorn, Legolas, Gimli and the rest); generic tavern wanderers hire as in vanilla.")]
    public Dropdown<string> WandererAllegianceScope { get; set; } = new Dropdown<string>(
        new[] { "All wanderers (by culture)", "Named companions only" }, 0);

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_wanderer_allegiance}Wanderer Allegiance")]
    [SettingPropertyBool("{=taom_mcm_EnableWandererSideTowns}Wanderers Stay On Their Side", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableWandererSideTowns_hint}When ON (default), a new or relocated tavern wanderer stands only in a town whose owner is on their side: an orc never appears in a Free town and a Gondor ranger never in an Evil one. Neutral wanderers and Neutral towns mix with everyone. A wanderer already standing in a wrong-side town stays until vanilla's daily swap picks them. When OFF, vanilla picks any town.")]
    public bool EnableWandererSideTowns { get; set; } = true;

    // --- World / Hard Questions ---

    // GroupOrder 57: 9, 10, 15, 19-56 and 100 were taken as of 2026-10-18.
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_hard_questions}Hard Questions", GroupOrder = 57)]
    [SettingPropertyBool("{=taom_mcm_EnableHardQuestions}Enable Hard Questions", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableHardQuestions_hint}When enabled, a lord held prisoner by your clan (in your party or in one of your settlements) can be questioned about his clan's or kingdom's lands, for a bribe or by force, and stripped of his arms and armour. Force and stripping cost relation, Honor and Mercy, and stripping costs renown. Each topic works once per captivity. When off, those conversation options do not appear.")]
    public bool EnableHardQuestions { get; set; } = true;

    // --- World / Lord Courtesies ---

    // GroupOrder 63: 0, 1, 2, 9, 10, 15, 19-62 and 100 were taken as of 2026-10-20 (rebase on trunk 599987653).
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_lord_courtesies}Lord Courtesies", GroupOrder = 63)]
    [SettingPropertyBool("{=taom_mcm_EnableTroopGifts}Enable Troop Gifts", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableTroopGifts_hint}When enabled, a conversation with a lord who leads a party offers a line to gift him troops through a donate-style party screen. The lord must not be in your clan or kingdom, not at war with you, and not on the opposite side of the Free/Evil divide (a Neutral lord may receive). Troops move from you to him only. The gift is paid by Bonds of Battle's hand-over rule (half the influence the gift is worth, at most 10 relation a day per relation owner), so the relation needs Bonds of Battle and its Troop Gifts Win Lords' Favour switch on; with either off the gift is still accepted but earns nothing. When off, the line does not appear.")]
    public bool EnableTroopGifts { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_lord_courtesies}Lord Courtesies")]
    [SettingPropertyBool("{=taom_mcm_EnableDomainReveal}Enable Domain Reveal", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableDomainReveal_hint}When enabled, you can ask a free lord about his clan's holdings. A lord who knows you well enough names them and they are marked on your map; a lord who does not refuses, and a lord whose clan holds nothing says so, each at a small cost in relation. Lords at war with you and your own clan never answer. When off, the line does not appear.")]
    public bool EnableDomainReveal { get; set; } = true;

    // --- World / Notable Services (#899) ---

    // GroupOrder 77: 0-5, 9, 10, 15, 19-71, 76 and 100 were taken as of 2026-10-24 (the highest below the Map Tools sentinel is 76).
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_notable_services}Notable Services", GroupOrder = 77)]
    [SettingPropertyBool("{=taom_mcm_EnableNotableServices}Enable Notable Services", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableNotableServices_hint}When enabled, a notable of the settlement you stand in (an artisan, gang leader, merchant, rural notable or headman, but not a preacher) offers three lines in conversation: give men to the settlement (troops raise its militia, prisoners its hearth in a village or its prosperity in a town; a castle takes troops only), pay to be introduced to the other notables there, and pay to have the nearest towns and castles marked on your map. Single player only; all three lines are hidden during a messenger conversation. At war with the settlement's realm only the gift line is hidden: the introduction still works and the holds question is refused. The gift line is also hidden while a party screen is open or when you hold no regular troop or prisoner. Amounts are in notable_services.json. When off, none of the lines appear. Default: on.")]
    public bool EnableNotableServices { get; set; } = true;

    // --- World / Bandit Voices (#900) ---

    // GroupOrder 78 is used by no other group (77 is Notable Services, 76 War of the Ring/Armies; checked 2026-10-26).
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bandit_voices}Bandit Voices", GroupOrder = 78)]
    [SettingPropertyBool("{=taom_mcm_EnableBanditGreetings}Enable Bandit Clan Greetings", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableBanditGreetings_hint}When enabled, if you attack on land a bandit party you have not dealt with before and it belongs to one of the eight talking clans (Dunlending, Rhun, Haradrim, Gundabad, Umbar, Gondor deserters, Blacklocks, Mirkwood Stalkers), there is a one in four chance that it greets you in its own words, and your ultimatum is vanilla's reply to that kind of greeting. With Encounter Lines also on, these clans greet you in a line chosen by your people and your side on half of these meetings, and keep vanilla's greeting on the other quarter. Feral Orcs and looters, which have no greetings of their own, greet you that way on three meetings in four. Spiders and wild trolls are unchanged. Applies to the next bandit conversation. Text only: nothing else about the encounter changes. When off, every greeting is vanilla's. The lines bandits use when they rob you are separate data; with Encounter Lines on, half of these robberies use its lines instead. Default: on.")]
    public bool EnableBanditGreetings { get; set; } = true;

    // --- World / Encounter Lines (Patch149) ---

    // GroupOrder 89 is used by no other group (78 is Bandit Voices; 0-5, 9, 10, 15, 19-88 and 100 were taken on 2026-11-13).
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_encounter_lines}Encounter Lines", GroupOrder = 89)]
    [SettingPropertyBool("{=taom_mcm_EnableEncounterLines}Enable Encounter Lines", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableEncounterLines_hint}When enabled, lords you meet on the map speak in the voice of their people and by who you are to them: their liege, an ally, a rival, a foe, or one of their own. Caravans and villagers greet you this way half of the time on a first meeting on land, and speak their own words when they surrender to a robbery. Bandit clans rob you with their own threats, and a hideout boss challenges you in the clan's own words. Bandit greetings when you attack a bandit party also need Enable Bandit Clan Greetings (World/Bandit Voices) on. Lords in your clan, companions, wanderers and notables keep vanilla's lines, and so do lords you speak to inside a settlement; the hideout boss still speaks the clan's own words. Applies to the next conversation. Text only: nothing else about the encounter changes, but TAOM's lines are not voiced, so a line that replaces a voiced vanilla line is silent. When off, every line is vanilla's. Default: on.")]
    public bool EnableEncounterLines { get; set; } = true;

    // --- World / Inn Rounds (#901) ---

    // Read live at each tavern keeper conversation, through InnRoundsSettingsProvider's one cached settings reference, so none
    // needs a restart. GroupOrder 79 is used by no other group (78 is Bandit Voices, 77 Notable Services; checked 2026-10-26); it
    // sits on the group's first property, because MCM ignores a GroupOrder on any later one.
    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_inn_rounds}Inn Rounds", GroupOrder = 79)]
    [SettingPropertyBool("{=taom_mcm_EnableInnRounds}Enable Inn Rounds", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableInnRounds_hint}When enabled, the tavern keeper of a town offers three more lines: stand a round for your whole party (morale and a little Charm experience), stand a round for everyone in the tavern (renown and a little Charm experience), and pay to have the nearest towns and castles marked on your map. A round costs the town's price for the local drink for each head, capped, and the gold goes to the town. At most a few rounds a day, set in inn_rounds.json. Single player only; the lines are hidden during a messenger conversation. The map question is refused while the town's realm is at war with yours or its owner does not know you well enough. When off, none of the lines appear. Default: on.")]
    public bool EnableInnRounds { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_inn_rounds}Inn Rounds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_InnRoundsMoraleScale}Inn Round Morale Scale", 0f, 3f, "0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_InnRoundsMoraleScale_hint}Multiplies the morale a party round gives (20 to 40 before the scale, set in inn_rounds.json). 0 gives none; the party's morale never goes past 100. Between 0 and 3. Default: 1.")]
    public float InnRoundsMoraleScale { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultScale;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_inn_rounds}Inn Rounds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_InnRoundsRenownScale}Inn Round Renown Scale", 0f, 3f, "0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_InnRoundsRenownScale_hint}Multiplies the renown a house round gives (1 to 3 before the scale, set in inn_rounds.json), rounded to a whole number. 0 gives none. Between 0 and 3. Default: 1.")]
    public float InnRoundsRenownScale { get; set; } = TAOM.Features.InnRounds.InnRoundsSettingsProvider.DefaultScale;

    // --- Prisoner Recruitment ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_prisoner_recruitment}Prisoner Recruitment", GroupOrder = 39)]
    [SettingPropertyBool("{=taom_mcm_EnablePrisonerRecruitmentWaiver}Waive Same-Side Prisoner Morale Cost", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnablePrisonerRecruitmentWaiver_hint}When enabled (default), recruiting a prisoner of your own faction or your own side of the war costs no morale — an Isengard captor loses nothing taking on Mordor, Gundabad or Dunland troops, since all serve Sauron. Alignment comes from execution/alignment.json. Neutral factions (Khand, Umbar) only waive their own culture, not each other. When off, prisoner morale is vanilla (-1 per troop, -2 per bandit).")]
    public bool EnablePrisonerRecruitmentWaiver { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_prisoner_recruitment}Prisoner Recruitment")]
    [SettingPropertyBool("{=taom_mcm_EnablePrisonerRecruitmentWaiverPlayer}Apply To Player", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnablePrisonerRecruitmentWaiverPlayer_hint}When ON (default), YOU pay no morale for same-side prisoners. When OFF, you pay the vanilla cost (AI lords still get the waiver if 'Apply To AI Lords' is on).")]
    public bool EnablePrisonerRecruitmentWaiverPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_prisoner_recruitment}Prisoner Recruitment")]
    [SettingPropertyBool("{=taom_mcm_EnablePrisonerRecruitmentWaiverAi}Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnablePrisonerRecruitmentWaiverAi_hint}When ON (default), AI lords also pay no morale for same-side prisoners, so allied factions absorb each other's captured troops more readily. When OFF, only you benefit (if 'Apply To Player' is on).")]
    public bool EnablePrisonerRecruitmentWaiverAi { get; set; } = true;

    // --- Naval Travel ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_naval_travel}Naval Travel", GroupOrder = 37)]
    [SettingPropertyBool("{=taom_mcm_EnableNavalTravel}Enable Naval Travel", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableNavalTravel_hint}Currently DISABLED — the map's navmesh isn't set up for naval travel yet, so this feature is parked in code and this toggle has no effect for now. (When re-enabled: parties sail across water on the campaign map — the engine's native naval travel, unlocked without the Naval DLC.)")]
    public bool EnableNavalTravel { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_naval_travel}Naval Travel")]
    [SettingPropertyBool("{=taom_mcm_NavalTravelApplyToPlayer}Apply To Player", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_NavalTravelApplyToPlayer_hint}When ON (default), YOUR party can sail. When OFF, you stay land-bound (AI still sails if 'Apply To AI Lords' is on). The master 'Enable Naval Travel' toggle off disables sailing for everyone.")]
    public bool NavalTravelApplyToPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_naval_travel}Naval Travel")]
    [SettingPropertyBool("{=taom_mcm_NavalTravelApplyToAi}Apply To AI Lords", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_NavalTravelApplyToAi_hint}When ON (default), AI lords' parties can also sail. When OFF, only the player sails and AI stays on land — the conservative option if AI naval routing looks odd.")]
    public bool NavalTravelApplyToAi { get; set; } = true;

    // --- World / Alignment Desertion ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion", GroupOrder = 38)]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentDesertion}Enable Alignment Desertion", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentDesertion_hint}When enabled, troops whose culture is opposed in alignment to their lord (Free vs Evil) desert each day: an Evil lord sheds Good troops and a Good lord sheds Evil troops. Alignment comes from execution/alignment.json. Neutral cultures (Umbar etc.) never desert. Troops that would desert cannot be recruited from prisoners, notables' volunteers or by garrison auto-recruit (tavern mercenaries are not checked). With Recruitment Alignment on and its 'Only Good Rejects Evil' on, Evil lords and their garrisons shed no Free troops. When off, vanilla. This is the default for new campaigns. With the Alignment rules start option on Standard, a campaign follows this switch live; Strict or Relaxed fixes all four alignment switches for that campaign. Older saves follow this setting.")]
    public bool EnableAlignmentDesertion { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion")]
    [SettingPropertyFloatingInteger("{=taom_mcm_AlignmentDesertionRate}Daily Desertion Rate", 0f, 1f, "#0%", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_AlignmentDesertionRate_hint}Fraction of each opposed-alignment troop type that deserts per day (minimum 1 per type). Default 0.50 = half per day. Higher clears mixed parties/garrisons faster.")]
    public float AlignmentDesertionRate { get; set; } = 0.5f;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentDesertionPlayer}Apply To Player", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentDesertionPlayer_hint}When ON (default), YOUR party and garrisons shed opposed-alignment troops. When OFF, you may keep mixed-alignment troops (AI is still affected if 'Apply To AI Lords' is on).")]
    public bool EnableAlignmentDesertionPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentDesertionAi}Apply To AI Lords", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentDesertionAi_hint}When ON (default), AI lords' parties and garrisons also shed opposed-alignment troops. When OFF, only the player is affected (if 'Apply To Player' is on).")]
    public bool EnableAlignmentDesertionAi { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentDesertionParties}Apply To Mobile Parties", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentDesertionParties_hint}When ON (default), lords' field parties (including army members) shed opposed troops. When OFF, only garrisons are affected (if 'Apply To Garrisons' is on).")]
    public bool EnableAlignmentDesertionParties { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_alignment_desertion}Alignment Desertion")]
    [SettingPropertyBool("{=taom_mcm_EnableAlignmentDesertionGarrisons}Apply To Garrisons", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_EnableAlignmentDesertionGarrisons_hint}When ON (default), settlement garrisons shed opposed troops (e.g. a conquered fief's old-culture defenders leave). When OFF, only mobile parties are affected (if 'Apply To Mobile Parties' is on).")]
    public bool EnableAlignmentDesertionGarrisons { get; set; } = true;

    // --- World / Lord Retinue (LordRetinue feature, #1122) ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_lord_retinue}Lord Retinue", GroupOrder = 40)]
    [SettingPropertyBool("{=taom_mcm_EnableLordRetinue}Enable Lord Retinue", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableLordRetinue_hint}When enabled, an AI lord party whose clan is at its party limit takes one idle adult clan member along and gives that member a party role (scout, surgeon, engineer or quartermaster) the member is better at than the lord. The heroes go back to a settlement when the clan drops below its party limit or when this is turned off. When off, no new heroes join. A party whose disband starts always frees every hero aboard but its leader, with this on or off.")]
    public bool EnableLordRetinue { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_lord_retinue}Lord Retinue")]
    [SettingPropertyInteger("{=taom_mcm_LordRetinueMaxExtraHeroes}Extra Heroes Per Party", 0, LordRetinue.LordRetinueSettingsProvider.MaxExtraHeroesCap, "0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_LordRetinueMaxExtraHeroes_hint}How many extra clan heroes one AI lord party may carry. Default 1. Heroes already riding along stay when you lower this. A hero who rides along costs the clan a daily wage and a party slot, and can be wounded, captured or killed in battle with the lord.")]
    public int LordRetinueMaxExtraHeroes { get; set; } = 1;

    // --- Combat Mechanics (CombatMechanics feature — crush-through, cleave, charge knockdown) ---

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics", GroupOrder = 24)]
    [SettingPropertyBool("{=taom_mcm_EnableCombatMechanics}Enable Combat Mechanics", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCombatMechanics_hint}Master toggle for the combat feel pack: skill-based crush-through, monster/orc crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers. When off, everything below is inert and combat behaves exactly as before this feature.")]
    public bool EnableCombatMechanics { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableSkillCrushThrough}Skill-Based Crush-Through", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSkillCrushThrough_hint}High-energy swings from an attacker who greatly outskills the defender have a chance to crush through the block (chance grows with skill gap and swing momentum; off-angle swings are penalized). Tuning constants live in combat_mechanics_config.json.")]
    public bool EnableSkillCrushThrough { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableMonsterCrushThrough}Monster Crush-Through", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableMonsterCrushThrough_hint}Troll, mumakil, war elephant and giant spider melee attacks can only be blocked with a SHIELD — a bare-weapon block is crushed through automatically.")]
    public bool EnableMonsterCrushThrough { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableOrcShieldCrush}Orc Shield Crush-Through", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableOrcShieldCrush_hint}AI orc/uruk swings can crush through even SHIELD blocks (energy- and skill-gated) — turtling against an orc horde is no longer safe. Never applies to the player's own attacks.")]
    public bool EnableOrcShieldCrush { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableCreatureCleave}Creature Cleave", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCreatureCleave_hint}Troll and mumakil swings slice through a victim and carry 30% momentum into the next — one swing can hit multiple soldiers, including through shield blocks that take damage (a zero-damage block still stops the swing).")]
    public bool EnableCreatureCleave { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableCreatureUnstoppable}Creature Stagger Immunity", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCreatureUnstoppable_hint}Big creatures shrug off hits below a per-creature damage threshold — no flinch-locking a troll with arrows. Thresholds live in combat_mechanics_config.json.")]
    public bool EnableCreatureUnstoppable { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableChargeKnockdown}Weight-Based Charge Knockdown", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_EnableChargeKnockdown_hint}Mount charges knock down based on relative weight (charger + rider vs victim), charge speed and the victim's race resistance: a mumakil bowls over infantry, a horse can't floor a troll, dwarves stand their ground.")]
    public bool EnableChargeKnockdown { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableShieldPenetration}Shield Penetration", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_EnableShieldPenetration_hint}Missiles from config-listed weapon classes/items can penetrate shields and damage the soldier behind. Off by default, and the shipped lists are empty, so javelins behave as in the base game: only a HERO with the Throwing Impale perk pierces a shield. Regular troops never do, in vanilla or here. Turning this on with an empty config changes nothing; add entries in combat_mechanics_config.json first.")]
    public bool EnableShieldPenetration { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableRaceCombatModifiers}Race Combat Modifiers", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRaceCombatModifiers_hint}Per-race combat flavor from combat_mechanics_config.json: dwarves resist knockdown and stagger, elves crush through better and strike true from any angle, orcs swing with brute momentum. When off, all races use neutral values.")]
    public bool EnableRaceCombatModifiers { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CrushThroughMaxChance}Crush-Through Max Chance", 0f, 1f, "#0%", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_CrushThroughMaxChance_hint}Ceiling for the skill-based crush-through chance, reached at a huge skill gap with a full-momentum swing. Default 50%.")]
    public float CrushThroughMaxChance { get; set; } = 0.5f;

    // Renamed from ChargeAutoKnockdownWeightRatio (#1017): its default went 8 to 6 in v2.0.30 under the same name,
    // and MCM loads the saved value by name, so every player who saved before then kept 8.
    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyInteger("{=taom_mcm_ChargeAutoKnockdownRatio}Auto-Knockdown Weight Ratio", 2, 30, Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_ChargeAutoKnockdownRatio_hint}Charger-to-victim weight ratio at which a charge ALWAYS knocks the victim down, from any angle, regardless of resistance (mumakil vs man is ~125, warg + orc vs man 8, horse + man vs man exactly 6). Default 6 since 2026-09-17, so a full-speed horse contact floors a man; raise it to 7 or 8 to give that back to wargs and chariots only. Values below the neutral weight ratio are treated as the neutral ratio. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public int ChargeAutoKnockdownRatio { get; set; } = 6;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ChargeNeutralWeightRatio}Charge Neutral Weight Ratio", 1f, 30f, "0.0", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_ChargeNeutralWeightRatio_hint}Charger-to-victim weight ratio at which a charge knocks down exactly as vanilla does (needs about 8 damage on a head-on hit). Heavier chargers than this ratio get MORE penetration, lighter ones less, down to the min factor. Default 6 = horse + man vs man. Lower it and every charge hits harder; raise it and only the heavy mounts do.")]
    public float ChargeNeutralWeightRatio { get; set; } = 6f;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ChargeHorsePenetration}Charge Penetration", 0f, 1f, "0.00", Order = 14, RequireRestart = false,
        HintText = "{=taom_mcm_ChargeHorsePenetration_hint}The base knockdown penetration of a charge at the neutral ratio and full speed. Default 0.4, vanilla's value, which cancels a troop's 0.4 base resistance so only Athletics protects him. 0.5 and up floors an ordinary man on any head-on hit that does damage; 0.3 needs a hard hit.")]
    public float ChargeHorsePenetration { get; set; } = 0.4f;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyFloatingInteger("{=taom_mcm_ChargeMinPenetrationFactor}Charge Min Penetration Factor", 0f, 2.5f, "0.00", Order = 15, RequireRestart = false,
        HintText = "{=taom_mcm_ChargeMinPenetrationFactor_hint}How far below vanilla the weight term may push a light charger's penetration against a heavy victim. Default 1.0 = never below vanilla (uruks and trolls fall like men; the troll, dwarf and Sauron race rows do the resisting). 0.25 restores the pre-2026-09-17 feel where a horse could not floor anything heavier than a man. The ceiling is the JSON's maxPenetrationFactor (shipped 2.5); a slider value above it is treated as that maximum.")]
    public float ChargeMinPenetrationFactor { get; set; } = 1f;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableCultureChargeDamage}Culture Charge Damage", Order = 16, RequireRestart = false,
        HintText = "{=taom_mcm_EnableCultureChargeDamage_hint}Cavalry charge damage scales by the rider's culture (elves 1.6x, Rohan 1.5x, Rhun 1.4x, Gondor 1.3x, Dale and the orc kingdoms 1.2x, Erebor 1.0x). The table lives in combat_mechanics_config.json; this switch turns the whole thing off.")]
    public bool EnableCultureChargeDamage { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableSignatureStrikes}Signature Strikes", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSignatureStrikes_hint}Configured heroes (shipped: Sauron and the Nine) get direction-mapped effects on any melee hit. Sauron: an overhead is a Slam (the struck foe always goes down, enemies in a ring around the impact are knocked down and take part of the damage, a burst of fear drains their morale; also fires on an overhead into the ground), a side swing is a Sweep (the struck foe and enemies in front are staggered back). The Nine: an overhead or a side swing is a Scream (enemies around the wraith take part of the damage, are staggered back and lose morale; the struck foe is staggered and frightened too). Never on a throw, a shot, a kick or bare hands. The same rules apply in a Custom Battle. Cooldowns and radii live in signature_strikes_config.json.")]
    public bool EnableSignatureStrikes { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SignatureStrikeCooldownMultiplier}Signature Strike Cooldown Multiplier", 0.5f, 5f, "0.0x", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_SignatureStrikeCooldownMultiplier_hint}Multiplies every signature-strike cooldown (shipped: Sauron's Slam 20 s and Sweep 12 s, the Nine's Scream 15 s). Raise it if Sauron or the Nazgul feel overpowered; the JSON values need a full restart, this slider applies live.")]
    public float SignatureStrikeCooldownMultiplier { get; set; } = 1f;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_mcm_EnableShieldBashKnockdown}Hammerhand (Shield Bash Knockdown)", Order = 17, RequireRestart = false,
        HintText = "{=taom_mcm_EnableShieldBashKnockdown_hint}A shield bash or a kick can floor a man. The chance grows with the attacker's Athletics lead over the victim; the victim's race and any race ability that toughens him cut it, a kick is weaker than a bash, and you resist more than a soldier does. Fighters who shrug off blows never fall: trolls while Creature Stagger Immunity is on, a soldier with a live race ability that shrugs off blows, and a hero with a career shrug-off passive. The numbers live in combat_mechanics_config.json.")]
    public bool EnableShieldBashKnockdown { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_cleave_mcm_enable}Cleaving Blows", Order = 18, RequireRestart = false,
        HintText = "{=taom_cleave_mcm_enable_hint}Your swings with a two-handed axe, mace, sword or polearm can cut through one enemy and strike the next. Each enemy after the first takes less damage. Armour slows the blade, and a kill or a clear skill lead over the victim carries it further. Cleaving Blows never applies to a shield block, a thrust or a swing from horseback. Only the character you control, unless the AI option below is on. Trolls and other big creatures keep their own cleave. The numbers live in combat_mechanics_config.json. Default: on.")]
    public bool EnableCleavingBlows { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyBool("{=taom_cleave_mcm_ai}Cleaving Blows for AI Two-Handers", Order = 19, RequireRestart = false,
        HintText = "{=taom_cleave_mcm_ai_hint}AI soldiers who swing a two-handed axe, mace or sword cut through enemies by the same rules. Every two-handed troop in both armies then hits a packed line harder. Needs Cleaving Blows on. Default: off.")]
    public bool EnableCleavingBlowsForAi { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_combat_mechanics}Combat Mechanics")]
    [SettingPropertyInteger("{=taom_cleave_mcm_max_targets}Cleaving Blows Max Targets", 3, 5, "0", Order = 20, RequireRestart = false,
        HintText = "{=taom_cleave_mcm_max_targets_hint}The most enemies one swing can strike, the first one included. Default 3. A swing often stops sooner: each enemy takes some of its force, and armour takes more.")]
    public int CleavingBlowsMaxTargets { get; set; } = 3;

    // --- Aura of Dread ---

    [SettingPropertyGroup("{=taom_mcm_group_aura_of_dread}Aura of Dread", GroupOrder = 34)]
    [SettingPropertyBool("{=taom_mcm_EnableDreadAura}Enable Aura of Dread", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableDreadAura_hint}Nazgul and Sauron drain the morale of enemy troops right beside them. The aura weakens them so they break sooner in the fighting, but it never routs anyone by itself. Takes effect immediately, but morale already drained is not restored when you switch it off.")]
    public bool EnableDreadAura { get; set; } = true;

    // Renamed from DreadAuraRadius on 2026-10-15, when the default dropped from 12 to 5: MCM keeps a
    // saved value per property name, so the old name would have kept every existing 12. Never rename
    // it back, because settings files not yet re-saved still hold DreadAuraRadius = 12.
    [SettingPropertyGroup("{=taom_mcm_group_aura_of_dread}Aura of Dread")]
    [SettingPropertyFloatingInteger("{=taom_mcm_DreadAuraCloseRadius}Dread Radius", 4f, 15f, "#0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_DreadAuraCloseRadius_hint}Metres over which a wraith projects dread. Full strength within the inner radius from dread_aura_config.json (default 2m), falling to nothing at this distance. Default 5. The ceiling is 15: beyond 15 m the engine's proximity search stops using its grid and checks every agent on the field, on every pulse.")]
    public float DreadAuraCloseRadius { get; set; } = 5f;

    [SettingPropertyGroup("{=taom_mcm_group_aura_of_dread}Aura of Dread")]
    [SettingPropertyFloatingInteger("{=taom_mcm_DreadAuraMoralePerSecond}Morale Drained Per Second", 0f, 20f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_DreadAuraMoralePerSecond_hint}Morale per second drained at full strength, before the target's tier/hero resistance and its racial resistance. The aura stops at a morale floor of 10 (dread_aura_config.json), so it never routs anyone by itself. At the default 5.0, in an even fight, a tier-3 human reaches the floor after about 22 seconds of contact, a tier-6 elite about 37, and an elven hero of level 16 or more never. Below about 1.0 the engine's own morale recovery outpaces the drain.")]
    public float DreadAuraMoralePerSecond { get; set; } = 5f;

    [SettingPropertyGroup("{=taom_mcm_group_aura_of_dread}Aura of Dread")]
    [SettingPropertyBool("{=taom_mcm_DreadAuraAffectsPlayerTroops}Affects Your Own Troops", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_DreadAuraAffectsPlayerTroops_hint}When off, your team is immune and dread only touches the AI. Left on, an enemy wraith weakens your soldiers so they rout sooner in combat, and routed troops are LOST from your party. Your own character is always immune either way: the engine gives player-controlled agents no morale component at all.")]
    public bool DreadAuraAffectsPlayerTroops { get; set; } = true;

    // --- Battle Tactics / Race Abilities ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_race_abilities}Race Abilities", GroupOrder = 56)]
    [SettingPropertyBool("{=taom_mcm_EnableRaceAbilities}Enable Race Abilities", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRaceAbilities_hint}AI soldiers fight with a battle ability for their race (or, for men, their culture) on a cooldown, fired when the moment comes and joined by nearby kin: berserkers go berserk when hurt in melee, Uruk-hai fall into bloodlust on a kill, dwarves stand fast against a charge or a crowd, elves quicken, orcs swarm, Rohan's riders spur on, Gondor closes ranks, and more. Your own character never fires one. Switching it on takes effect from the next battle; switching it off stops new abilities at once and lets running ones finish. Numbers live in race_abilities.json; the console command taom.print_race_abilities shows what each ability has done in the battle on screen.")]
    public bool EnableRaceAbilities { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_race_abilities}Race Abilities")]
    [SettingPropertyBool("{=taom_mcm_RaceAbilityWarCries}War Cries", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_RaceAbilityWarCries_hint}The soldier who fires an ability, and a few of the kin who join him, shout in their own voice.")]
    public bool RaceAbilityWarCries { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_race_abilities}Race Abilities")]
    [SettingPropertyBool("{=taom_mcm_RaceAbilityMessages}Show Ability Messages", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_RaceAbilityMessages_hint}One line in the message log when five or more soldiers on either side fire the same ability within two seconds. Never one line per soldier.")]
    public bool RaceAbilityMessages { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_race_abilities}Race Abilities")]
    [SettingPropertyBool("{=taom_mcm_RaceAbilityDebugLog}Race Ability Debug Log", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_RaceAbilityDebugLog_hint}Writes one log line for every wave of abilities and every phase change (active, spent, ready). Off, the log still gets a summary every 30 s of activity and at the end of each battle. For checking that the abilities work; leave off in normal play.")]
    public bool RaceAbilityDebugLog { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_race_abilities}Race Abilities")]
    [SettingPropertyBool("{=taom_mcm_RaceAbilityGlow}Ability Glow and Sparks", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_RaceAbilityGlow_hint}A soldier whose ability is active wears a coloured outline: red for fury, steel blue for a guard, violet for dread; the speed and aim abilities stay unlit. Sparks burst on the soldier who fires and on a few of the kin who join him. Only the soldiers nearest the camera are outlined (visuals.maxGlowing in race_abilities.json). Drawn on this client only, and hidden while Hide Battle UI is on.")]
    public bool RaceAbilityGlow { get; set; } = true;

    // --- Battle Tactics / Hero Auras (#1127) ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_hero_auras}Hero Auras", GroupOrder = 88)]
    [SettingPropertyBool("{=taom_hero_auras_mcm_enabled}Enable Hero Auras", Order = 0, RequireRestart = false,
        HintText = "{=taom_hero_auras_mcm_enabled_hint}Named heroes strengthen the allied soldiers near them, each in his own way: Dain's dwarves keep their feet, Aragorn's men hold their nerve, the elven lords' archers draw faster. Only the heroes listed in hero_auras.json carry an aura. Where an aura and a soldier's own race ability give the same thing, the stronger counts; they never add. Takes effect from the next aura pulse.")]
    public bool EnableHeroAuras { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_hero_auras}Hero Auras")]
    [SettingPropertyFloatingInteger("{=taom_hero_auras_mcm_strength}Hero Aura Strength", 0.0f, 2.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_hero_auras_mcm_strength_hint}Multiplies every aura's bonuses. 1.0 is as authored in hero_auras.json; 0 turns every aura off. Morale floors and on/off effects such as shrug-off do not scale. Default: 1.0.")]
    public float HeroAuraStrength { get; set; } = 1f;

    // --- World / Uncapturable Heroes ---

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_uncapturable_heroes}Uncapturable Heroes", GroupOrder = 48)]
    [SettingPropertyBool("{=taom_mcm_EnableUncapturableHeroes}Sauron and the Nazgûl Cannot Be Captured", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableUncapturableHeroes_hint}When you beat them, they escape the field as fugitives instead of going to your dungeon, and return with a new army within days. This does NOT make them immortal: one of the Nine can still die in a battle he could not be captured in. Edit uncapturable_heroes_config.json to change who this covers.")]
    public bool EnableUncapturableHeroes { get; set; } = true;

    // --- Map UI / Settlement Nameplates ---

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates", GroupOrder = 40)]
    [SettingPropertyBool("{=taom_mcm_EnableNameplateFade}Enable Settlement Nameplate Fade", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableNameplateFade_hint}Fade settlement nameplates with camera distance. When off, all nameplates display at full visibility regardless of distance (vanilla).")]
    public bool EnableNameplateFade { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyFloatingInteger("{=taom_mcm_NameplateFadeNearDistance}Fade Start Distance", 5f, 500f, "#0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_NameplateFadeNearDistance_hint}Camera distance at which fade begins. Nameplates closer than this stay fully opaque. Default 80.")]
    public float NameplateFadeNearDistance { get; set; } = 80f;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyFloatingInteger("{=taom_mcm_NameplateFadeFarDistance}Fade End Distance", 10f, 1000f, "#0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_NameplateFadeFarDistance_hint}Camera distance at which fade completes. Nameplates farther than this are fully hidden. Must be greater than Fade Start Distance. Default 200.")]
    public float NameplateFadeFarDistance { get; set; } = 200f;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyBool("{=taom_mcm_EnableNameplateRelationColors}Colour Nameplates by Relation", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableNameplateRelationColors_hint}Tint each settlement plate by your relation to its owner: green for your own faction, red for an enemy, blue for an allied kingdom, plain parchment for neutral. Off leaves every plate parchment. Applies immediately.")]
    public bool EnableNameplateRelationColors { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyFloatingInteger("{=taom_mcm_NameplateRelationTintStrength}Relation Tint Strength (%)", 0f, 100f, "#0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_NameplateRelationTintStrength_hint}How strongly the relation colour is applied to the bar, the name and the diamond frame. 100 is the full palette, 0 is plain parchment for everyone. Applies immediately. Default 100.")]
    public float NameplateRelationTintStrength { get; set; } = 100f;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyFloatingInteger("{=taom_mcm_NameplateNeutralPlateOpacity}Neutral Plate Opacity (%)", 10f, 100f, "#0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_NameplateNeutralPlateOpacity_hint}Opacity of a neutral settlement's plate. Vanilla uses 35, which can be faint over dark terrain. Tracked settlements keep vanilla's 80. Applies immediately. Default 35.")]
    public float NameplateNeutralPlateOpacity { get; set; } = 35f;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyFloatingInteger("{=taom_mcm_NameplateRelationPlateOpacity}Coloured Plate Opacity (%)", 10f, 100f, "#0", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_NameplateRelationPlateOpacity_hint}Opacity of a plate owned by your own faction, an enemy or an allied kingdom. Vanilla uses 50 for your own faction and 35 for the others. Tracked settlements keep vanilla's 80. Applies immediately. Default 50.")]
    public float NameplateRelationPlateOpacity { get; set; } = 50f;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_settlement_nameplates}Settlement Nameplates")]
    [SettingPropertyBool("{=taom_hroad_mcm_enable}Harvest Road notices", Order = 7, RequireRestart = false,
        HintText = "{=taom_hroad_mcm_enable_hint}Over a town's nameplate, show the small trade pop-ups the base game raises only for caravans, also when a villager delivers goods there and when a lord's party buys food there. Display only: the trades themselves are unchanged. Applies immediately. Default: on.")]
    public bool EnableHarvestRoadNotices { get; set; } = true;

    // --- Map UI / Text Input ---

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_text_input}Text Input", GroupOrder = 43)]
    [SettingPropertyBool("{=taom_mcm_ImeGuardEnabled}Pause the input method outside text fields", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_ImeGuardEnabled_hint}For Chinese, Japanese and Korean input methods. While the game is in front and no text field has focus, the input method is paused, so hotkeys on the map and in battle reach the game instead of opening its candidate window. A search, rename or other text field gets it back at once, and other programs are not affected. Applies immediately. Default: on.")]
    public bool ImeGuardEnabled { get; set; } = true;

    // --- Map UI / Party Icons ---

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_party_icons}Party Icons", GroupOrder = 41)]
    [SettingPropertyFloatingInteger("{=taom_mcm_MapFigureScale}Map Figure Scale", 0.05f, 1.0f, "#0.00", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_MapFigureScale_hint}Size of party-icon figures and their mounts on the campaign map. Vanilla = 0.30; default 0.15 = half (makes parties feel smaller relative to settlements). Applies on the next icon rebuild after changing.")]
    public float MapFigureScale { get; set; } = 0.15f;

    // --- Map UI / Hidden Hosts ---

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_hidden_hosts}Hidden Hosts", GroupOrder = 42)]
    [SettingPropertyBool("{=taom_mcm_HiddenHostsEnabled}Hide Exact Troop Counts", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_HiddenHostsEnabled_hint}Another party's troop count on the campaign map blurs with its distance from you: '1??', '12?', then exact as you close in, and '...' at the very edge of sight. Standing closer, a higher Scouting skill and hosts of your own realm all sharpen it. Wounded and prisoner counts show only when the count is exact. A count you read stays remembered with a '~' and fades by the hour, and reading an enemy host's count from afar trains your scout's Scouting; Off stops both. AI lords still see true numbers. A host's orders in its hover tooltip stay hidden until its count is exact. Off = vanilla counts and orders. Applies immediately. Default: on.")]
    public bool HiddenHostsEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_hidden_hosts}Hidden Hosts")]
    [SettingPropertyBool("{=taom_mcm_ForestSightPenaltyEnabled}Shorter Sight Under Trees", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_ForestSightPenaltyEnabled_hint}In vanilla, forest makes other parties harder for you to see and slows your party by 20%; it never hides your party from anyone. With this on, your own sight on the campaign map also shrinks by 30% of your base sight while your party travels through forest, and by half that if your scout has the Keen Sight perk. It does not apply inside a settlement. A raised lookout, perks and a Far Watch ride still add to your sight, and the penalties together never take more than half of your base sight. Only your own party is affected; AI parties see as before. The map bar view distance shows the shorter range, but its tooltip has no line for the forest. Off = vanilla sight in forest. Applies immediately. Default: on.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("{=taom_mcm_EnableFiefGrantRebalance}Enable Fief Grant Rebalance", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFiefGrantRebalance_hint}Rebalances who gets a captured town or castle. Off = exact vanilla, where the ruling clan's +60 merit and its unlimited King's Vote override let it take fief after fief. Does not change starting ownership. The ballot's fief count shows either way.")]
    public bool EnableFiefGrantRebalance { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantCapturerBonus}Siege Participation Bonus", 1.0f, 5.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantCapturerBonus_hint}Merit multiplier for the clan that carried the winning assault: the largest share of the winning besieging side's battle contribution, the same number the loot split uses. Every other clan of that kingdom that fielded a party there gets it in proportion, 1 + (bonus - 1) x its share of the top clan's contribution, so a clan with half the top clan's contribution gets half the bonus. Mercenaries and allies from other kingdoms cannot be granted the fief and are not counted. Vanilla's flat +30 for the assault leader's clan is untouched. 1.00 = off. Default: 2.50.")]
    public float FiefGrantCapturerBonus { get; set; } = 2.5f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantAbsentFromSiegeFactor}Absent From Siege Factor", 0.1f, 1.0f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantAbsentFromSiegeFactor_hint}Merit multiplier for a clan that had no party in the winning assault, so land goes to the houses that fought for it. Applies to your clan as well, exempt or not: this is about what a clan did, not what it holds. No effect when nothing is on record for the settlement, such as a fief that changed hands without an assault. 1.00 = off. Default: 0.50.")]
    public float FiefGrantAbsentFromSiegeFactor { get; set; } = 0.5f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantLandlessBonus}Landless Clan Bonus", 1.0f, 5.0f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantLandlessBonus_hint}Merit multiplier for a clan that holds no town or castle at all. Keeps poor clans able to field parties instead of withering and leaving the kingdom. Vanilla already favours landless clans (a flat +30 and nothing in its holdings divisor), so this stacks on a strong base. 1.00 = no bonus. Default: 2.00.")]
    public float FiefGrantLandlessBonus { get; set; } = 2.0f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantConcentrationPenalty}Concentration Penalty", 0.0f, 1.0f, "#0.00", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantConcentrationPenalty_hint}How hard each fief a clan already holds counts against its next claim, as 1/(1 + fiefs x penalty). Vanilla only divides by the VALUE it holds, so a clan sitting on many cheap castles is barely damped. 0.00 = off. Default: 0.35.")]
    public float FiefGrantConcentrationPenalty { get; set; } = 0.35f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantCultureMatchBonus}Culture Match Bonus", 1.0f, 3.0f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantCultureMatchBonus_hint}Merit multiplier when the clan's culture matches the settlement's. Vanilla has no equivalent, which is how an orc clan ends up holding an elven city. 1.00 = off. Default: 1.50.")]
    public float FiefGrantCultureMatchBonus { get; set; } = 1.5f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantCultureMismatchPenalty}Culture Mismatch Penalty", 0.1f, 1.0f, "#0.00", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantCultureMismatchPenalty_hint}Merit multiplier when it does not match. Lower means foreign clans rarely win a settlement of another culture. 1.00 = off. Default: 0.60.")]
    public float FiefGrantCultureMismatchPenalty { get; set; } = 0.6f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantRulingClanFactor}Ruling Clan Factor", 0.1f, 2.0f, "#0.00", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantRulingClanFactor_hint}Merit multiplier for the king's own clan, which vanilla already hands a flat +60 on top of its tier and strength. Below 1.00 damps the crown's claim. 1.00 = vanilla. Default: 0.75.")]
    public float FiefGrantRulingClanFactor { get; set; } = 0.75f;

    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyFloatingInteger("{=taom_mcm_FiefGrantKingsVoteFiefShareCap}King's Vote Fief Share Cap", 0.0f, 1.0f, "#0.00", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantKingsVoteFiefShareCap_hint}Share of the kingdom's towns and castles above which the ruling clan loses its right to overrule the council. Vanilla lets a king with enough influence override every grant, and his pick is always himself. 1.00 = vanilla. Default: 0.34.")]
    public float FiefGrantKingsVoteFiefShareCap { get; set; } = 0.34f;

    // Renamed from FiefGrantApplyPenaltiesToPlayerClan (default off, i.e. exempt) in #565. MCM keeps
    // a saved value per property, so flipping that default would have reached fresh installs only;
    // a new property name is what reaches every install. The old key in TAOM.json is ignored.
    [SettingPropertyGroup("{=taom_mcm_group_kingdom_politics}Kingdom Politics/{=taom_mcm_group_fief_grants}Fief Grants", GroupOrder = 42)]
    [SettingPropertyBool("{=taom_mcm_FiefGrantExemptPlayerClanFromPenalties}Exempt Your Clan From Penalties", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_FiefGrantExemptPlayerClanFromPenalties_hint}On = your clan keeps every bonus but is never damped for the fiefs it already holds or for a culture mismatch. Off = your clan is scored like any other. Replaces the old Apply Penalties To Your Clan switch, which shipped with the exemption on and let a landed player out-score every AI clan for fiefs they never fought for; the switch was renamed so existing installs pick up the new default. The Absent From Siege Factor applies either way. Default: off.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_character_preview}Character Preview/{=taom_mcm_group_dwarf_eye_height}Dwarf Eye Height", GroupOrder = 44)]
    [SettingPropertyBool("{=taom_mcm_EnableDwarfEyeHeight}Enable Dwarf Eye Height", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableDwarfEyeHeight_hint}Lower a dwarf eye height so the third-person camera sits at the head instead of floating above it. Applies to every dwarf, not just the player, so it also moves the aim origin for dwarf troops. Changing this takes effect the next time a dwarf visual is built (opening a character preview, or the next dwarf spawn), not the instant you flip it.")]
    public bool EnableDwarfEyeHeight { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_character_preview}Character Preview/{=taom_mcm_group_dwarf_eye_height}Dwarf Eye Height")]
    [SettingPropertyFloatingInteger("{=taom_mcm_DwarfEyeHeightAdjuster}Dwarf Eye Height Offset",
        HeroRace.EyeHeightAdjustment.MinAdjuster, HeroRace.EyeHeightAdjustment.MaxAdjuster, "#0.00",
        Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_DwarfEyeHeightAdjuster_hint}Metres relative to the human eye height. Negative lowers the camera. Default -0.20. The resulting height is clamped to a safe range, so extreme values cannot put the camera in the floor. Like the toggle above, a change lands the next time a dwarf visual is built.")]
    public float DwarfEyeHeightAdjuster { get; set; } = HeroRace.EyeHeightAdjustment.DefaultAdjuster;

    // --- Supply Lines ---
    //
    // Resupply convoys ordered from a town, castle or friendly lord (#505, port of the yotthani
    // SupplyLines module). Costs and speed are the simulation-relevant knobs; the route arrows are
    // presentation. Defaults match the source module so its author's tuning carries over.

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines", GroupOrder = 45)]
    [SettingPropertyBool("{=taom_mcm_EnableSupplyLines}Enable Supply Lines", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSupplyLines_hint}Order resupply convoys from a town, castle or friendly lord: goods from real market stock, troops from volunteers, delivered to you on the map. When off, the order option disappears and no new orders can be placed; convoys already on the road still arrive.")]
    public bool EnableSupplyLines { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SupplyGoodsMarkupFactor}Goods Markup Factor", 1.0f, 3.0f, "#0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyGoodsMarkupFactor_hint}Price multiplier on ordered goods over their market value. Default 1.05.")]
    public float SupplyGoodsMarkupFactor { get; set; } = 1.05f;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SupplyTransportFeePerDistance}Transport Fee Per Distance", 0f, 20f, "#0.0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyTransportFeePerDistance_hint}Delivery fee per map-distance unit between the source and your party. Default 2.")]
    public float SupplyTransportFeePerDistance { get; set; } = 2f;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SupplyMercenaryWagePerDistance}Mercenary Escort Wage Per Distance", 0f, 50f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyMercenaryWagePerDistance_hint}Cost of a mercenary escort per map-distance unit. Default 10.")]
    public float SupplyMercenaryWagePerDistance { get; set; } = 10f;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyInteger("{=taom_mcm_SupplyMercenaryGuardCount}Mercenary Guard Count", 0, 40, "0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyMercenaryGuardCount_hint}Guards added to the convoy when a mercenary escort is hired. Default 10.")]
    public int SupplyMercenaryGuardCount { get; set; } = 10;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyFloatingInteger("{=taom_mcm_SupplyCaravanHoursPerDistance}Convoy Hours Per Distance", 0.5f, 10f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyCaravanHoursPerDistance_hint}Convoy travel time per map-distance unit. Default 2. Lower is faster delivery.")]
    public float SupplyCaravanHoursPerDistance { get; set; } = 2f;

    [SettingPropertyGroup("{=taom_mcm_group_supply_lines}Supply Lines")]
    [SettingPropertyBool("{=taom_mcm_SupplyShowRouteVisual}Show Convoy Route", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_SupplyShowRouteVisual_hint}Draw the arrow trail from the source to your party while a convoy is on the road. Presentation only.")]
    public bool SupplyShowRouteVisual { get; set; } = true;

    // --- Field Camps ---
    //
    // Campaign-map camps (#506, port of the yotthani FieldCamp module). Every knob here reads
    // into campaign math (morale, food, ambush odds, placement), so all are coop
    // simulation-relevant. Defaults are the source author's tuning.

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps", GroupOrder = 46)]
    [SettingPropertyBool("{=taom_mcm_EnableFieldCamps}Enable Field Camps", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFieldCamps_hint}Pitch camps on the campaign map: field and fortified camps with morale and foraging, terrain-gated ambushes and lookouts. When off, the Make Camp button and camp menus disappear; an existing camp stays until you break it.")]
    public bool EnableFieldCamps { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampSetupHours}Camp Setup Hours", 0.5f, 24f, "#0.0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CampSetupHours_hint}Hours to raise a field camp. Ambushes and lookouts take a quarter of this; a fortified camp double. Default 4.")]
    public float CampSetupHours { get; set; } = 4f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampMoralePerHour}Camp Morale Per Hour", 0f, 5f, "#0.00", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CampMoralePerHour_hint}Party morale gained per camped hour once the camp is raised; fortified camps double it. Default 1.")]
    public float CampMoralePerHour { get; set; } = 1f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampForagePerTroopFactor}Forage Rate", 0f, 1f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_CampForagePerTroopFactor_hint}Foraging throughput per troop. Grain per hour also scales with terrain, the party's Scouting skill and the square root of troop count. Default 0.10.")]
    public float CampForagePerTroopFactor { get; set; } = 0.1f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampMaxAmbushRange}Max Ambush Range", 1f, 30f, "#0.0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_CampMaxAmbushRange_hint}Spotting distance inside which an armed ambush can spring on a passing hostile party. Default 10.")]
    public float CampMaxAmbushRange { get; set; } = 10f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampBaseAmbushChance}Base Ambush Chance", 0f, 1f, "#0.00", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_CampBaseAmbushChance_hint}Base chance an eligible passer-by springs the ambush; proximity and Scouting raise it. Default 0.50.")]
    public float CampBaseAmbushChance { get; set; } = 0.5f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampMinTownDistance}Min Town Distance", 0f, 50f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_CampMinTownDistance_hint}Keep-out radius from towns and castles for pitching a field or fortified camp. 0 disables the check; ambushes and lookouts are exempt. Default 10.")]
    public float CampMinTownDistance { get; set; } = 10f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyInteger("{=taom_mcm_CampFortifiedUpgradeCost}Fortified Upgrade Cost", 0, 10000, "0", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_CampFortifiedUpgradeCost_hint}Gold to fortify a raised field camp. Default 500.")]
    public int CampFortifiedUpgradeCost { get; set; } = 500;

    // Camp work (#860): the men hunt, fell timber or fish from a raised camp. Single-player only, so the three knobs are
    // player-local (CoopSettingsRelevance).

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampActivityHours}Camp Work Hours", 1f, 48f, "#0.0", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_CampActivityHours_hint}Hours a camp work (hunt, fell timber, fish) takes once the men are set to it. Default 10.")]
    public float CampActivityHours { get; set; } = 10f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampActivityYieldPerTenMen}Camp Work Yield Per Ten Men", 0f, 50f, "#0.0", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_CampActivityYieldPerTenMen_hint}Goods a finished camp work brings in for every ten healthy men, on the best ground for it; poorer ground brings in less. Default 10.")]
    public float CampActivityYieldPerTenMen { get; set; } = 10f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyInteger("{=taom_mcm_CampActivityYieldCap}Camp Work Yield Cap", 0, 200, "0", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_CampActivityYieldCap_hint}Most goods one finished camp work can bring in, however many men are sent. Default 30.")]
    public int CampActivityYieldCap { get; set; } = 30;

    // Rest healing and start in camp (#860). Single-player only, so all three are player-local (CoopSettingsRelevance).

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampRestHealRegularsPerDay}Camp Rest Healing: Troops Per Day", 0f, 50f, "#0.0", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_CampRestHealRegularsPerDay_hint}Extra wounded troops healed per day: a base amount that the usual healing percentages raise too. Works only while the camp panel is open (Camp Options, then Manage camp); the work menus do not count (set both to 0 to turn rest healing off, lodging perks included). Default 15.")]
    public float CampRestHealRegularsPerDay { get; set; } = 15f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CampRestHealHeroesPerDay}Camp Rest Healing: Heroes Per Day", 0f, 50f, "#0.0", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_CampRestHealHeroesPerDay_hint}Extra hit points per day for wounded heroes: a base amount that the usual healing percentages raise too. Works only while the camp panel is open (Camp Options, then Manage camp); the work menus do not count (set both to 0 to turn rest healing off, lodging perks included). Default 19.")]
    public float CampRestHealHeroesPerDay { get; set; } = 19f;

    [SettingPropertyGroup("{=taom_mcm_group_field_camps}Field Camps")]
    [SettingPropertyBool("{=taom_mcm_CampStartInCamp}Start New Campaigns In Camp", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_CampStartInCamp_hint}A new campaign begins with a field camp pitched where the party starts, if the ground allows it; otherwise a message says why and the game goes on as usual. Off by default.")]
    public bool CampStartInCamp { get; set; } = false;

    // --- Refuge ---
    //
    // Movable player bases raised from field camps (#507, port of the yotthani Refuge module).
    // Costs, distances, defence bonuses, militia and raids all read into campaign or battle math:
    // every knob is coop simulation-relevant. Defaults are the source author's tuning; raids ship
    // OFF as in the source (experimental).

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge", GroupOrder = 47)]
    [SettingPropertyBool("{=taom_mcm_EnableRefuges}Enable Refuges", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRefuges_hint}Raise a field or fortified camp into a persistent, garrisonable base. When off: no founding, no upgrading, and your party is not held in place during a build. Standing refuges persist, builds in progress still finish, and refuges can still be entered, managed and dismantled.")]
    public bool EnableRefuges { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyInteger("{=taom_mcm_RefugeFoundCost}Found Cost", 0, 50000, "0", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeFoundCost_hint}Gold to establish a refuge. Default 2000.")]
    public int RefugeFoundCost { get; set; } = 2000;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyInteger("{=taom_mcm_RefugeStrongholdUpgradeCost}Stronghold Upgrade Cost", 0, 100000, "0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeStrongholdUpgradeCost_hint}Gold to upgrade a refuge into a stronghold. Default 5000.")]
    public int RefugeStrongholdUpgradeCost { get; set; } = 5000;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeBuildHours}Build Hours", 1f, 48f, "#0.0", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeBuildHours_hint}Hours to raise a refuge after founding; the stronghold rebuild takes the same. Default 6.")]
    public float RefugeBuildHours { get; set; } = 6f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyInteger("{=taom_mcm_RefugeMaxCap}Max Refuges (hard cap)", 1, 10, "0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeMaxCap_hint}Ceiling on simultaneous refuges. The live limit is min(1 + clan tier / 2, this cap). Default 3.")]
    public int RefugeMaxCap { get; set; } = 3;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeManageRange}Manage Range", 1f, 15f, "#0.0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeManageRange_hint}How close your party must be to manage a refuge's garrison and stores. Default 4.")]
    public float RefugeManageRange { get; set; } = 4f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeMinTownDistance}Min Town Distance (found)", 0f, 60f, "#0.0", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeMinTownDistance_hint}Founding keep-out radius from towns and castles. Default 16.")]
    public float RefugeMinTownDistance { get; set; } = 16f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeStrongholdMinTownDistance}Min Town Distance (stronghold)", 0f, 80f, "#0.0", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeStrongholdMinTownDistance_hint}Stronghold-upgrade keep-out radius from towns and castles. Default 26.")]
    public float RefugeStrongholdMinTownDistance { get; set; } = 26f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeDefenseBonus}Refuge Defense Bonus", 0f, 0.9f, "#0.00", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeDefenseBonus_hint}Damage reduction for troops defending a refuge, in battle and in auto-resolve. Default 0.20.")]
    public float RefugeDefenseBonus { get; set; } = 0.2f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeStrongholdDefenseBonus}Stronghold Defense Bonus", 0f, 0.9f, "#0.00", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeStrongholdDefenseBonus_hint}Damage reduction for troops defending a stronghold. Default 0.35.")]
    public float RefugeStrongholdDefenseBonus { get; set; } = 0.35f;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyInteger("{=taom_mcm_RefugeMilitiaBase}Militia Base", 0, 50, "0", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeMilitiaBase_hint}Base temporary militia rallying to a refuge under attack; age of the refuge and garrison size add more. Default 6.")]
    public int RefugeMilitiaBase { get; set; } = 6;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyInteger("{=taom_mcm_RefugeMilitiaMax}Militia Max", 0, 100, "0", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeMilitiaMax_hint}Ceiling on the temporary defence militia. Default 40.")]
    public int RefugeMilitiaMax { get; set; } = 40;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyBool("{=taom_mcm_RefugeEnableRaids}Enable Enemy Raids", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeEnableRaids_hint}Experimental: hostile parties can attack your refuges (auto-resolved when you are away). Off by default.")]
    public bool RefugeEnableRaids { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_refuge}Refuge")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RefugeRaidRange}Raid Range", 1f, 20f, "#0.0", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_RefugeRaidRange_hint}Search radius around a refuge for raid candidates when raids are enabled. Default 6.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_player_switcher}Player Switcher")]
    [SettingPropertyBool("{=taom_mcm_EnablePlayerSwitcher}Enable Player Switcher", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnablePlayerSwitcher_hint}Adds a panel to the character creation face generator listing the existing lords of your chosen culture. Pick one and you play the campaign as that lord, with their face, gear, skills, clan, fiefs and kingdom. The character you build is set aside. Off means the panel never loads and character creation is untouched. When the Themed Faction & Hero Picker (Menus & Loading Screens) is shown, it takes this panel's place for that character creation, and a lord picked there is taken over through this feature, so with this off that picker copies the lord's look, gear and skills onto your own character instead.")]
    public bool EnablePlayerSwitcher { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_player_switcher}Player Switcher")]
    [SettingPropertyBool("{=taom_mcm_PlayerSwitcherIncludeWanderers}Include Wanderers", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_PlayerSwitcherIncludeWanderers_hint}Also lists the wanderers of your culture. A wanderer has no clan, so instead of taking over a house they are adopted into the clan you named, with the banner you designed, and you lead it. Only 20 of 39 cultures have any wanderers.")]
    public bool PlayerSwitcherIncludeWanderers { get; set; } = true;

    // Renamed from PlayerSwitcherAllowLoreLockedHeroes (#1067): a default flipped under the same name
    // would reach fresh installs only, because MCM loads the saved value by name.
    [SettingPropertyGroup("{=taom_mcm_group_player_switcher}Player Switcher")]
    [SettingPropertyBool("{=taom_mcm_PlayerSwitcherAllowDarkLords}Allow Sauron and the Nazgul", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_PlayerSwitcherAllowDarkLords_hint}Offers the lore-locked dark lords as playable, here and on the Themed Faction & Hero Picker. On by default. The catch: TAOM's capture immunity defers to vanilla for the player character, so a player-controlled Sauron or Witch-king CAN be captured and ransomed like any other lord. Turn this off to keep that lore promise: this list then hides them, and a pick of one on the Themed Faction & Hero Picker only copies his look onto a new character. Saved values from before November 2026 were reset to this default; if you had chosen your own, set it again.")]
    public bool PlayerSwitcherAllowDarkLords { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_player_switcher}Player Switcher")]
    [SettingPropertyBool("{=taom_mcm_PlayerSwitcherTransferStartingGold}Carry Over Starting Gold", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_PlayerSwitcherTransferStartingGold_hint}When you take over a lord, adds the starting gold your created character was given on top of the lord's own treasury. An adopted wanderer starts with a new character's gold either way, and an Advanced Start type other than the default sets its own gold. Off by default: an established lord is already funded, so stacking a startup package on them is a balance change rather than a fix.")]
    public bool PlayerSwitcherTransferStartingGold { get; set; } = false;

    // --- Menus & Loading Screens (#704, Kysaro's themed front end) ---

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomMainMenu}Themed Main Menu", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomMainMenu_hint}Kysaro's Middle-earth main menu in place of the vanilla one. Takes effect the next time the main menu opens.")]
    public bool FrontEndCustomMainMenu { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndHideGameVersion}Hide Game Version", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndHideGameVersion_hint}Hides the Bannerlord version number in the corner of the main menu. The TAOM log still records it. Takes effect after a restart: the game builds the version line once, at startup.")]
    public bool FrontEndHideGameVersion { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndMenuVideo}Main Menu Video", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndMenuVideo_hint}Plays the Sauron video behind the main menu in place of the vanilla background video. Takes effect the next time the main menu opens.")]
    public bool FrontEndMenuVideo { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndMuteMenuMusic}Mute Menu Music Under The Video", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndMuteMenuMusic_hint}Keeps the vanilla menu theme off the main menu while the menu video plays its own soundtrack, so it needs Main Menu Video on. Vanilla's music returns once you leave the main menu. Takes effect the next time the main menu opens.")]
    public bool FrontEndMuteMenuMusic { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomLoadingImages}Themed Loading Screens", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomLoadingImages_hint}Shows Kysaro's Middle-earth paintings on loading screens, never the same one twice in a row. Only the picture last shown is kept in memory. Takes effect on the next loading screen.")]
    public bool FrontEndCustomLoadingImages { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens")]
    [SettingPropertyBool("{=taom_mcm_FrontEndSkillIcons}Themed Skill Icons", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndSkillIcons_hint}Kysaro's skill icons in place of the vanilla ones, everywhere in the game. Turning them on applies the next time the main menu opens; turning them off takes effect after a restart, because the vanilla icons are not kept once replaced.")]
    public bool FrontEndSkillIcons { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndFactionScreen}Themed Faction & Hero Picker", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndFactionScreen_hint}Kysaro's faction screen with painted portraits, a map and a hero picker, in place of TAOM's faction map. Picking one of that faction's lords or ladies makes you that character, with their clan, fiefs and kingdom: character creation goes straight to the career choice and then into the game (Player Switcher's settings apply). Picking a legend or any other character starts you with its look, gear and skills. When it is shown it takes the place of Player Switcher's panel for that character creation. Takes effect the next time character creation reaches the culture screen.")]
    public bool FrontEndFactionScreen { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomFaceGen}Themed Face Generator", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomFaceGen_hint}Kysaro's reskin of the character creation face generator. Outside character creation the face generator stays vanilla. Takes effect the next time it opens.")]
    public bool FrontEndCustomFaceGen { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomNarrativeStage}Themed Backstory Screens", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomNarrativeStage_hint}Kysaro's reskin of the family, childhood, adolescence, youth, adulthood and age screens. Takes effect the next time one of them opens.")]
    public bool FrontEndCustomNarrativeStage { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomReviewStage}Themed Review Screen", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomReviewStage_hint}Kysaro's reskin of the final review screen, where you name your character. Takes effect the next time it opens.")]
    public bool FrontEndCustomReviewStage { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomBannerEditor}Themed Banner Editor", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomBannerEditor_hint}The themed, layered banner editor, both during character creation and in a running campaign. Takes effect the next time it opens.")]
    public bool FrontEndCustomBannerEditor { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomClanNaming}Themed Clan Naming Screen", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomClanNaming_hint}Kysaro's reskin of the clan naming screen. Takes effect the next time it opens.")]
    public bool FrontEndCustomClanNaming { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_menus_loading_screens}Menus & Loading Screens/{=taom_mcm_group_character_creation}Character Creation")]
    [SettingPropertyBool("{=taom_mcm_FrontEndCustomOptionsStage}Themed Options Screen", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_FrontEndCustomOptionsStage_hint}Kysaro's reskin of the difficulty and campaign options screen at the end of character creation. Takes effect the next time it opens.")]
    public bool FrontEndCustomOptionsStage { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_map_tools}Map Tools/{=taom_mcm_group_distance_cache_rebuild}Distance Cache Rebuild", GroupOrder = 100)]
    [SettingPropertyButton("{=taom_mcm_RebuildDistanceCacheAction}Rebuild Settlement Distance Cache",
        RequireRestart = false,
        Content = "{=taom_mcm_RebuildDistanceCacheAction_button}Rebuild Now",
        HintText = "{=taom_mcm_RebuildDistanceCacheAction_hint}Spawns a 10-30 minute background task that recomputes the settlement distance cache against the live map scene. Requires an active campaign. Game stays playable but pathfinding queries during the rebuild may be inconsistent — best run from main menu after loading a save.")]
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

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits", GroupOrder = 53)]
    [SettingPropertyBool("{=taom_mcm_CreatureBanditSpawnBroods}Spawn Spider Broods", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditSpawnBroods_hint}Whether spider broods keep spawning around Mirkwood and Dol Guldur on the campaign map (one a day, up to twenty). Off stops new broods; broods already on the map stay. Broods exist only in a campaign started with v2.0.32 or later.")]
    public bool CreatureBanditSpawnBroods { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnBroods;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyBool("{=taom_mcm_CreatureBanditSpawnTrollBands}Spawn Troll Bands", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditSpawnTrollBands_hint}Whether wild troll bands keep spawning on the campaign map, about one per kingdom near its towns, castles and villages (one new band a day, two to four trolls each). Trolls are never taken prisoner or recruited. Off stops new bands; bands already on the map stay. Troll bands need a new campaign. The options below tune the spiders only.")]
    public bool CreatureBanditSpawnTrollBands { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditsConfig.DefaultSpawnTrollBands;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CreatureBanditHitPoints}Creature Hit Points", 50f, 1000f, "0", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditHitPoints_hint}Hit points of a riderless creature bandit (the giant spiders of the Mirkwood broods). The ridden spider mount keeps its own. Applies to creatures spawned after the change.")]
    public float CreatureBanditHitPoints { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultHitPoints;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditBiteTargets}Bite: Max Soldiers Hit", 1, 10, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditBiteTargets_hint}How many soldiers the standing front bite can strike at once (the nearest ones in its arc).")]
    public int CreatureBanditBiteTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultBiteTargets;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditPounceTargets}Pounce: Max Soldiers Hit", 1, 10, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditPounceTargets_hint}How many soldiers the running pounce can strike at once.")]
    public int CreatureBanditPounceTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultPounceTargets;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditSwipeTargets}Swipe: Max Soldiers Hit", 1, 10, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditSwipeTargets_hint}How many soldiers a left or right swipe can strike at once.")]
    public int CreatureBanditSwipeTargets { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultSwipeTargets;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditBiteDamagePercent}Bite Damage %", 0, 300, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditBiteDamagePercent_hint}Bite damage as a percentage of the spider's base bite (100 = the ridden spider's damage).")]
    public int CreatureBanditBiteDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultBiteDamagePercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditPounceDamagePercent}Pounce Damage %", 0, 300, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditPounceDamagePercent_hint}Pounce damage as a percentage of the spider's base bite.")]
    public int CreatureBanditPounceDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultPounceDamagePercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditSwipeDamagePercent}Swipe Damage %", 0, 300, Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditSwipeDamagePercent_hint}Swipe damage as a percentage of the spider's base bite. Low by default: the swipe reaches several soldiers.")]
    public int CreatureBanditSwipeDamagePercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultSwipeDamagePercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyBool("{=taom_mcm_CreatureBanditKnockdownOnCritOnly}Only Crits Knock Down", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditKnockdownOnCritOnly_hint}When on, a creature's normal hit only staggers a soldier and only a critical hit knocks him down. When off, any hit of 30 or more knocks down, as the ridden spider's do.")]
    public bool CreatureBanditKnockdownOnCritOnly { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultKnockdownOnCritOnly;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CreatureBanditPounceCooldownSeconds}Pounce Cooldown (s)", 0.5f, 30f, "0.0", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditPounceCooldownSeconds_hint}Seconds between a creature's pounces or bites. Applies to creatures spawned after the change.")]
    public float CreatureBanditPounceCooldownSeconds { get; set; } = (float)TAOM.Features.Spider.SpiderConfig.PounceCooldownSeconds;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyFloatingInteger("{=taom_mcm_CreatureBanditSwipeCooldownSeconds}Swipe Cooldown (s)", 0.5f, 30f, "0.0", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditSwipeCooldownSeconds_hint}Seconds between a creature's swipes. Applies to creatures spawned after the change.")]
    public float CreatureBanditSwipeCooldownSeconds { get; set; } = (float)TAOM.Features.Spider.SpiderConfig.SideAttackCooldownSeconds;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditMissileTakenPercent}Missile Damage Taken %", 0, 200, Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditMissileTakenPercent_hint}Share of damage a creature takes from arrows, bolts, javelins and thrown weapons (100 = normal). Applies at once.")]
    public int CreatureBanditMissileTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMissileTakenPercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditCutTakenPercent}Cut Damage Taken %", 0, 200, Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditCutTakenPercent_hint}Share of damage a creature takes from cutting melee blows: swords, axes (100 = normal).")]
    public int CreatureBanditCutTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditPierceTakenPercent}Pierce Damage Taken %", 0, 200, Order = 14, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditPierceTakenPercent_hint}Share of damage a creature takes from piercing melee blows: spears, thrusts (100 = normal).")]
    public int CreatureBanditPierceTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    [SettingPropertyGroup("{=taom_mcm_group_creature_bandits}Creature Bandits")]
    [SettingPropertyInteger("{=taom_mcm_CreatureBanditBluntTakenPercent}Blunt Damage Taken %", 0, 200, Order = 15, RequireRestart = false,
        HintText = "{=taom_mcm_CreatureBanditBluntTakenPercent_hint}Share of damage a creature takes from blunt melee blows: maces, hammers, charges (100 = normal).")]
    public int CreatureBanditBluntTakenPercent { get; set; } = TAOM.Features.CreatureBandits.CreatureBanditTuning.DefaultMeleeTakenPercent;

    // --- Realm Borders (#698) ---
    // Client-side presentation only: listed under Presentation in CoopSettingsRelevance. The keys (toggle
    // and map mode) are native rebindable game keys under Options > Keybindings > Campaign Map, not MCM.

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders", GroupOrder = 54)]
    [SettingPropertyBool("{=taom_mcm_EnableRealmBorders}Show Realm Borders", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRealmBorders_hint}Draws each kingdom's border on the campaign map, following rivers, coasts and mountain ranges, and redraws it when a fief changes hands. While this is on, the Show Realm Borders key (M by default) hides and shows them, and the map mode key (G) switches what they show.")]
    public bool EnableRealmBorders { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersHeraldicBands}Heraldic Bands Instead of Atlas Look", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersHeraldicBands_hint}Off: the Atlas look, a watercolour wash inside each border with a dash-dot ink line. On: two solid bands in each realm's colour with a gap on the line.")]
    public bool RealmBordersHeraldicBands { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersGildPlayerRealm}Gold Cord on Your Realm", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersGildPlayerRealm_hint}Draws your own kingdom's frontier as a gold cord, so you can find it at a glance.")]
    public bool RealmBordersGildPlayerRealm { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RealmBordersWidthScale}Border Width", 0.5f, 3f, "0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersWidthScale_hint}Multiplies the width of every border. Default: 1.")]
    public float RealmBordersWidthScale { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultWidthScale;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RealmBordersFadeStartDistance}Fade In From Camera Distance", 0f, 500f, "0", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersFadeStartDistance_hint}Camera distance at which the borders start to appear; closer than this they are hidden. Must be below Full Opacity Distance, or both revert to their defaults. Default: 45.")]
    public float RealmBordersFadeStartDistance { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFadeStartDistance;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RealmBordersFullOpacityDistance}Full Opacity Distance", 0f, 500f, "0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersFullOpacityDistance_hint}Camera distance at which the borders are fully drawn. Default: 110.")]
    public float RealmBordersFullOpacityDistance { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFullOpacityDistance;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersDrawThroughTerrain}Draw Borders Through Hills", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersDrawThroughTerrain_hint}On: borders always show, drawn over hills and map figures. Off: borders lie on the ground and hide behind ridges.")]
    public bool RealmBordersDrawThroughTerrain { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersNames}Realm Names", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersNames_hint}Letters each kingdom's name across its land when the camera is zoomed out.")]
    public bool RealmBordersNames { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyFloatingInteger("{=taom_realm_borders_mcm_name_size}Realm Name Size", 0.5f, 2.5f, "0%", Order = 8, RequireRestart = false,
        HintText = "{=taom_realm_borders_mcm_name_size_hint}Makes the kingdom names on the map larger or smaller. Default: 100%.")]
    public float RealmBordersNameScale { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultNameScale;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersCrossingNotices}Border-Crossing Notices", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersCrossingNotices_hint}Tells you when your party rides into another kingdom's lands.")]
    public bool RealmBordersCrossingNotices { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersFillLands}Colour Realm Lands", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersFillLands_hint}Tints each kingdom's land in its colour between the borders, like a map in an atlas.")]
    public bool RealmBordersFillLands { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyFloatingInteger("{=taom_mcm_RealmBordersFillStrength}Realm Colour Strength", 0.05f, 0.8f, "0.00", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersFillStrength_hint}How strongly the land is tinted: 0.05 is a faint wash, 0.8 nearly paints over the map. Default: 0.5.")]
    public float RealmBordersFillStrength { get; set; } = TAOM.Features.RealmBorders.RealmBordersSettingsProvider.DefaultFillStrength;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyBool("{=taom_mcm_RealmBordersParchmentMap}Parchment Map at Full Zoom-Out", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersParchmentMap_hint}As you zoom all the way out, a parchment map of Middle-earth fades in over the campaign map, with the borders, realm colours and names drawn on it. It shows only while the borders do.")]
    public bool RealmBordersParchmentMap { get; set; } = true;

    // Dropdowns persist by index: never reorder either list (RealmBordersProviderTests pins both).
    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyDropdown("{=taom_mcm_RealmBordersBlendMode}Border Blend Mode", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersBlendMode_hint}Advanced: how the borders mix with the map beneath. 'Material default' keeps the engine material's own. If the borders glow and their dark ink line is missing, try Modulate (Factor turns blending off).")]
    public Dropdown<string> RealmBordersBlendMode { get; set; } = new Dropdown<string>(
        TAOM.Features.RealmBorders.RealmBordersSettingsProvider.BlendModeChoices, 0);

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders")]
    [SettingPropertyDropdown("{=taom_mcm_RealmBordersMaterial}Border Material", Order = 14, RequireRestart = false,
        HintText = "{=taom_mcm_RealmBordersMaterial_hint}Advanced: the engine material the borders are drawn from. 'Automatic' tries vertex_color_blend_after_postfx_mat first, which blends normally so the dark ink line shows, then the others in list order. Every choice draws over the parchment map.")]
    public Dropdown<string> RealmBordersMaterial { get; set; } = new Dropdown<string>(
        TAOM.Features.RealmBorders.RealmBordersSettingsProvider.MaterialChoices, 0);

    // --- Realm Borders: one colour per realm; blank keeps the palette's (ModuleData/realm_borders/palette.json) ---

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourGondor}Gondor", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourGondor_hint}Gondor's colour on the map, written #RRGGBB. Blank uses the default, #3F76B8.")]
    public string RealmColourGondor { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourRohan}Rohan", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourRohan_hint}Rohan's colour on the map, written #RRGGBB. Blank uses the default, #206803.")]
    public string RealmColourRohan { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourDunland}Dunland", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourDunland_hint}Dunland's colour on the map, written #RRGGBB. Blank uses the default, #7A4A28.")]
    public string RealmColourDunland { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourIsengard}Isengard", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourIsengard_hint}Isengard's colour on the map, written #RRGGBB. Blank uses the default, #3D3D40.")]
    public string RealmColourIsengard { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourMordor}Mordor", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourMordor_hint}Mordor's colour on the map, written #RRGGBB. Blank uses the default, #9E1A12.")]
    public string RealmColourMordor { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourHarad}Harad", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourHarad_hint}Harad's colour on the map, written #RRGGBB. Blank uses the default, #DF2100.")]
    public string RealmColourHarad { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourUmbar}Umbar", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourUmbar_hint}Umbar's colour on the map, written #RRGGBB. Blank uses the default, #2F3C58.")]
    public string RealmColourUmbar { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourShaghana}Shaghâna", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourShaghana_hint}Shaghâna's colour on the map, written #RRGGBB. Blank uses the default, #DC7FA8.")]
    public string RealmColourShaghana { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourAbanissa}Âbanissa", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourAbanissa_hint}Âbanissa's colour on the map, written #RRGGBB. Blank uses the default, #2A8C8C.")]
    public string RealmColourAbanissa { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourKhand}Khand", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourKhand_hint}Khand's colour on the map, written #RRGGBB. Blank uses the default, #E8841E.")]
    public string RealmColourKhand { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourRhun}Rhûn", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourRhun_hint}Rhûn's colour on the map, written #RRGGBB. Blank uses the default, #8A1236.")]
    public string RealmColourRhun { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourDale}Dale", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourDale_hint}Dale's colour on the map, written #RRGGBB. Blank uses the default, #43B4DC.")]
    public string RealmColourDale { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourErebor}Erebor", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourErebor_hint}Erebor's colour on the map, written #RRGGBB. Blank uses the default, #2A4DA8.")]
    public string RealmColourErebor { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourRivendell}Rivendell", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourRivendell_hint}Rivendell's colour on the map, written #RRGGBB. Blank uses the default, #530CAA.")]
    public string RealmColourRivendell { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourLothlorien}Lothlórien", Order = 14, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourLothlorien_hint}Lothlórien's colour on the map, written #RRGGBB. Blank uses the default, #E6E27A.")]
    public string RealmColourLothlorien { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourMirkwood}Mirkwood", Order = 15, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourMirkwood_hint}Mirkwood's colour on the map, written #RRGGBB. Blank uses the default, #185C2A.")]
    public string RealmColourMirkwood { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourLindon}Lindon", Order = 16, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourLindon_hint}Lindon's colour on the map, written #RRGGBB. Blank uses the default, #4FC4A0.")]
    public string RealmColourLindon { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourDolGuldur}Dol Guldur", Order = 17, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourDolGuldur_hint}Dol Guldur's colour on the map, written #RRGGBB. Blank uses the default, #4E2E6E.")]
    public string RealmColourDolGuldur { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourGundabad}Gundabad", Order = 18, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourGundabad_hint}Gundabad's colour on the map, written #RRGGBB. Blank uses the default, #5A6367.")]
    public string RealmColourGundabad { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourMistyMountainOrcs}Misty Mountain Orcs", Order = 19, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourMistyMountainOrcs_hint}Misty Mountain Orcs's colour on the map, written #RRGGBB. Blank uses the default, #7E7A50.")]
    public string RealmColourMistyMountainOrcs { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourGoblins}Goblins", Order = 20, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourGoblins_hint}Goblins's colour on the map, written #RRGGBB. Blank uses the default, #AFC93A.")]
    public string RealmColourGoblins { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourBlueCraig}Goblins of Blue Craig", Order = 21, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourBlueCraig_hint}Goblins of Blue Craig's colour on the map, written #RRGGBB. Blank uses the default, #6A8AA8.")]
    public string RealmColourBlueCraig { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourArthedain}Arthedain", Order = 22, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourArthedain_hint}Arthedain's colour on the map, written #RRGGBB. Blank uses the default, #A0B4D8.")]
    public string RealmColourArthedain { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourCardolan}Cardolan", Order = 23, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourCardolan_hint}Cardolan's colour on the map, written #RRGGBB. Blank uses the default, #28745C.")]
    public string RealmColourCardolan { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourRhudaur}Rhudaur", Order = 24, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourRhudaur_hint}Rhudaur's colour on the map, written #RRGGBB. Blank uses the default, #E0735A.")]
    public string RealmColourRhudaur { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourEredLuin}Ered Luin", Order = 25, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourEredLuin_hint}Ered Luin's colour on the map, written #RRGGBB. Blank uses the default, #7B68EE.")]
    public string RealmColourEredLuin { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourAnduinVale}Anduin Vale", Order = 26, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourAnduinVale_hint}Anduin Vale's colour on the map, written #RRGGBB. Blank uses the default, #654D37.")]
    public string RealmColourAnduinVale { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourAngaladh}Angáladh", Order = 27, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourAngaladh_hint}Angáladh's colour on the map, written #RRGGBB. Blank uses the default, #587A4A.")]
    public string RealmColourAngaladh { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourDorwinion}Dorwinion", Order = 28, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourDorwinion_hint}Dorwinion's colour on the map, written #RRGGBB. Blank uses the default, #722721.")]
    public string RealmColourDorwinion { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourSouthRhovanion}South Rhovanion", Order = 29, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourSouthRhovanion_hint}South Rhovanion's colour on the map, written #RRGGBB. Blank uses the default, #4B3F0F.")]
    public string RealmColourSouthRhovanion { get; set; } = string.Empty;

    [SettingPropertyGroup("{=taom_mcm_group_realm_borders}Realm Borders/{=taom_mcm_group_realm_colours}Realm Colours")]
    [SettingPropertyText("{=taom_mcm_RealmColourYourRealm}Your Realm", Order = 30, RequireRestart = false,
        HintText = "{=taom_mcm_RealmColourYourRealm_hint}The colour of your realm when it is none of the kingdoms above: your clan's land while it serves no kingdom, then a kingdom you found. Written #RRGGBB. Blank gives it a free colour. In one of the kingdoms above, that kingdom's own field applies.")]
    public string RealmColourYourRealm { get; set; } = string.Empty;

    // --- Animation Variety (orc and goblin gait, docs/features/animation-variety.md) ---

    [SettingPropertyGroup("{=taom_mcm_group_animation_variety}Animation Variety", GroupOrder = 9)]
    [SettingPropertyBool("{=taom_mcm_EnableRandomStepSize}Random step size per soldier", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRandomStepSize_hint}Orc and goblin warriors in a battle line no longer walk in lockstep: each soldier's step size is scaled by a random factor between the minimum below and 1.0, fixed for that soldier. Applied when a soldier spawns, so it takes effect in the next battle.")]
    public bool EnableRandomStepSize { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_animation_variety}Animation Variety")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GaitStepSizeMinFactor}Step size minimum factor", 0.80f, 1.00f, "0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_GaitStepSizeMinFactor_hint}Each soldier's step size is vanilla's step size times a random factor between this value and 1.00. Vanilla's own step size is 0.80 at Athletics 0 and rises to 1.00 at Athletics 300, so the factor is not the final step size: at 0.92 an Athletics 0 soldier ends at 0.74 to 0.80 and an Athletics 300 soldier at 0.92 to 1.00, and at 0.80 the lowest step size is 0.64. 1.00 turns the spread off. A value outside 0.80 to 1.00 falls back to 0.92.")]
    public float GaitStepSizeMinFactor { get; set; } = 0.92f;

    [SettingPropertyGroup("{=taom_mcm_group_animation_variety}Animation Variety")]
    [SettingPropertyBool("{=taom_mcm_EnableRandomGaitSet}Random gait set per soldier", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRandomGaitSet_hint}Each orc and goblin warrior walks with one of several gait sets, picked at random and fixed for that soldier. A gait set that is not installed is skipped, so with none installed this does nothing. Applied when a soldier spawns, so it takes effect in the next battle.")]
    public bool EnableRandomGaitSet { get; set; } = true;

    // On by default for the swing tests (Mike, 2026-10-18). MCM keeps each value in TAOM.json, so turning it
    // off for players later means renaming this setting, never flipping the default (orientation.md
    // "Persisted MCM defaults").
    [SettingPropertyGroup("{=taom_mcm_group_animation_variety}Animation Variety")]
    [SettingPropertyBool("{=taom_mcm_EnableSwingAlternativeProbe}Swing alternative probe (diagnostic)", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_EnableSwingAlternativeProbe_hint}Tests alternative swing clips: when a mission has act_taom_ swing codes loaded, it counts which clip each swing plays and where in the clip a hit lands, and only logs; it changes nothing in the game. While such codes exist it costs a per-frame sweep of every person in every mission, towns included; with none loaded it writes one log line per mission. Read at the start of each mission.")]
    public bool EnableSwingAlternativeProbe { get; set; } = true;

    // --- World / Watchful Holds ---
    // Both change what the campaign simulates (who is slowed, who loses men), so neither is listed in
    // CoopSettingsRelevance. Both are new, so a fresh TAOM.json carries the defaults.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_watchful_holds}Watchful Holds", GroupOrder = 58)]
    [SettingPropertyBool("{=taom_mcm_EnableWatchfulHolds}Enable Watchful Holds", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableWatchfulHolds_hint}When enabled (default), a castle or town whose realm is at war with yours harasses a lord party that marches within a few map units of its walls, if the fort is not besieged and holds enough troops (30 by default). Your party is slowed (disorganized: it moves slower, and its troops start a battle with lower morale until it wears off) and loses a few men, some food and morale. An army you lead loses cohesion, but never below 10, and each party marching with you takes its own losses. When off, forts never harass anyone. Applies at once, no restart.")]
    public bool EnableWatchfulHolds { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_watchful_holds}Watchful Holds")]
    [SettingPropertyBool("{=taom_mcm_WatchfulHoldsHarassAiLords}Harass AI lords", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_WatchfulHoldsHarassAiLords_hint}When enabled (default), AI lords marching past an enemy fort are slowed (disorganized) too, at a lower chance than yours, and lose nothing else. Turn it off to protect the campaign pace: the effect then stays on your own party and the army you lead (AI lords marching with your army still take their share). Does nothing while Enable Watchful Holds is off.")]
    public bool WatchfulHoldsHarassAiLords { get; set; } = true;

    // --- Fire and Tribute (raiding hosts, docs/features/fire-and-tribute.md, #870) ---
    //
    // The switches are read live on every hourly tick while a host rides, so RequireRestart = false. GroupOrder 66 is
    // unused; it is the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one).
    // A host alive when its switch goes off is sent away at its next hourly tick, or once its battle ends if it is fighting.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_fire_and_tribute}Fire and Tribute", GroupOrder = 66)]
    [SettingPropertyBool("{=taom_mcm_EnableFireAndTribute}Enable Fire and Tribute", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableFireAndTribute_hint}When enabled (default), raiding hosts ride once the War of the Ring is in Full War, or from day 89 when the War of the Ring is switched off. They ride out of their homelands to burn the villages of the opposed side: Orcs of Gundabad, Easterlings of Rhûn, Haradrim, Dunlendings and the Blacklocks of Erebor against the Free Peoples; outlaw bands of Gondor and Mirkwood against the Shadow; Corsairs of Umbar against either. A host raids the villages of the strongest enemy realm, fights weaker parties of that realm it meets, and goes home after its quota. When off, no host rides and a host already out is sent away at its next hourly tick; one still fighting is sent away once its battle ends. Single player only. Applies at once, no restart.")]
    public bool EnableFireAndTribute { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_fire_and_tribute}Fire and Tribute")]
    [SettingPropertyBool("{=taom_mcm_FireAndTributeFreeHosts}Free Peoples Hosts", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FireAndTributeFreeHosts_hint}When enabled (default), the outlaw bands of Gondor and Mirkwood may ride against the Shadow's realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeFreeHosts { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_fire_and_tribute}Fire and Tribute")]
    [SettingPropertyBool("{=taom_mcm_FireAndTributeNeutralHosts}Neutral Hosts", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FireAndTributeNeutralHosts_hint}When enabled (default), the Corsair Host of Umbar may ride against Free or Shadow realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeNeutralHosts { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_fire_and_tribute}Fire and Tribute")]
    [SettingPropertyBool("{=taom_mcm_FireAndTributeEvilHosts}Shadow Hosts", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FireAndTributeEvilHosts_hint}When enabled (default), the hosts of Gundabad, Rhûn, Harad and Dunland, and the Blacklocks of Erebor, may ride against the Free Peoples' realms. Does nothing while Enable Fire and Tribute is off.")]
    public bool FireAndTributeEvilHosts { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_fire_and_tribute}Fire and Tribute")]
    [SettingPropertyBool("{=taom_mcm_FireAndTributeEnvoys}Tribute Envoys", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FireAndTributeEnvoys_hint}When enabled (default), a host that rides against your kingdom sends an envoy a day or two later, if your clan owns villages and you are not enlisted. The envoy asks for gold, at a price that grows with your villages and with each envoy you have seized. Pay and the host raids none of your clan's villages and never attacks your party; haggle once (a Charm roll) when the tribute is large; ask for time; refuse; or seize the envoy, after which that side's next host rides against your kingdom first. Shown only while the map is free. When off, no envoy is scheduled or shown. Single player only. Applies at once, no restart.")]
    public bool FireAndTributeEnvoys { get; set; } = true;

    // --- Help Unlooked-For (docs/features/unlooked-for-aid.md, #872) ---
    //
    // Read live on the AI worker thread and the main thread, once per avoid question, through the settings object
    // the provider captured on the main thread when the model was built; RequireRestart = false. GroupOrder 68 is
    // unused; it is the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one).

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_help_unlooked_for}Help Unlooked-For", GroupOrder = 68)]
    [SettingPropertyBool("{=taom_mcm_EnableUnlookedForAid}Lords stop fleeing sieges", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableUnlookedForAid_hint}When enabled (default), an AI lord stops running from an army that besieges a settlement when the lord, with its army if it leads one or is attached to it, is at least half as strong as the entire siege camp (0.5 by default, unlooked_for_aid.json). Lords can then pass a siege or gather near it. A lord shut inside a besieged settlement is never fled from, whatever the strengths. A weaker lord still flees, so it is not caught when the siege lifts beside it. At a siege you lead, AI lords keep the base game behaviour. Caravans, villagers, bandits, patrols and garrisons are unchanged. When off, lords flee exactly as in the base game. Applies at once, no restart.")]
    public bool EnableUnlookedForAid { get; set; } = true;

    // --- The Red Arrow (docs/features/red-arrow.md, #873) ---
    //
    // Read live from the Army Management screen (main thread), so RequireRestart = false. GroupOrder 76 is unused; it is
    // the group's first property, so it carries the order (MCM ignores a GroupOrder on any later one). The group is meant to hold the other army features too.

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_armies}Armies", GroupOrder = 76)]
    [SettingPropertyBool("{=taom_mcm_EnableRedArrow}Call armies during your siege", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableRedArrow_hint}When enabled (default), the Army Management screen works during a siege you lead as the besieger: you can call parties into your army and release them while the siege goes on, at the usual influence cost. The base game's other limits still apply (a party that is fighting, too weak, disbanding, too far away or at sea is refused, and so is a party already at the siege outside your army). Defending a siege, having no army and being in a map event keep the base game block. Singleplayer only: in a co-op session this does nothing. When off, the block is exactly the base game's. Takes effect the next time the screen opens, no restart.")]
    public bool EnableRedArrow { get; set; } = true;

    // --- The King's Muster (docs/features/kings-muster.md, #874) ---
    //
    // One switch, read live on the AI's hourly army think (main thread), through the settings object the provider took when the
    // army-management model was built, so RequireRestart = false. It joins the "War of the Ring/Armies" group above, which
    // already carries its GroupOrder on EnableRedArrow (MCM ignores a GroupOrder on any later property).

    [SettingPropertyGroup("{=taom_mcm_group_war_of_the_ring}War of the Ring/{=taom_mcm_group_armies}Armies")]
    [SettingPropertyBool("{=taom_mcm_EnableKingsMusterDiscount}Rulers call hosts for less influence", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_EnableKingsMusterDiscount_hint}When enabled (default), an AI kingdom ruler, Good or Evil, pays half the influence (0.5 by default, RulerCallCostFactor in army_targeting.json) for each party called into an army, after culture feats. Cheaper calls also make the influence an army spends on keeping itself together cheaper for the ruler, so those hosts hold longer and the ruler's clan keeps more influence for votes. Your own calls, as a ruler or a vassal, cost what they always did. Singleplayer only: in a co-op session this does nothing. When off, the ruler pays the usual cost; culture feats and the Realms liege army discount still apply. The two discounts do not stack: when both apply to a call, the larger one is used. Applies at once, no restart.")]
    public bool EnableKingsMusterDiscount { get; set; } = true;

    // --- Bonds of Battle (relations earned on campaign, docs/features/bonds-of-battle.md) ---
    //
    // Every value is read live where it is used (at a battle's end, a troop hand-over, a barter), so nothing is applied once
    // at start-up and RequireRestart = false. GroupOrder 59 is unused; it is the group's first
    // property, so it carries the order (MCM ignores a GroupOrder on any later one). Defaults live in
    // BondsOfBattleSettingsProvider so the fallback before MCM is up cannot drift from them.

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle", GroupOrder = 59)]
    [SettingPropertyBool("{=taom_mcm_BondsOfBattleEnabled}Enable Bonds of Battle", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_BondsOfBattleEnabled_hint}Relation with lords and notables is earned on campaign: fighting beside a lord, clearing bandits near a settlement, handing a lord troops, and overpaying in a barter. Off restores vanilla on all four. Does nothing while you are enlisted. Default: on.")]
    public bool BondsOfBattleEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyInteger("{=taom_mcm_BondsAllyBonus}Relation With Allied Lords", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinAllyBonus, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxAllyBonus, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_BondsAllyBonus_hint}Relation gained with each lord who fought on your side in a battle, once per lord per battle (once per clan when Personal Relations is off, because a clan then shares one relation). Your own clan, an army you lead and lords leading no party earn nothing; after a win, the side leader you came to help is thanked by vanilla's own conversation instead, but only when vanilla pays him something (a small contribution earns him this bonus). 0 = none. Default: 1.")]
    public int BondsAllyBonus { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultAllyBonus;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyBool("{=taom_mcm_BondsEnemyPenaltyEnabled}Lose Relation With Enemy Lords", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_BondsEnemyPenaltyEnabled_hint}Each lord who fought against you in a battle thinks less of you, once per lord per battle (once per clan when Personal Relations is off, because a clan then shares one relation). Bandits are never counted. Default: off.")]
    public bool BondsEnemyPenaltyEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnemyPenaltyEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyInteger("{=taom_mcm_BondsEnemyPenalty}Relation Lost With Enemy Lords", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinEnemyPenalty, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxEnemyPenalty, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_BondsEnemyPenalty_hint}How much relation each enemy lord loses when the option above is on. 0 = none. Default: 5.")]
    public int BondsEnemyPenalty { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultEnemyPenalty;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyBool("{=taom_mcm_BondsBanditGratitudeEnabled}Notables Thank You For Clearing Bandits", Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_BondsBanditGratitudeEnabled_hint}In the field, against bandits alone (cleared hideouts are thanked by vanilla): after you win, one random notable in each settlement near the battle gains relation with you, one point for every ten bandits killed, wounded or routed, at least 1 and at most 10. A village in the other side's land (Free against Evil) does not thank you. Default: on.")]
    public bool BondsBanditGratitudeEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultBanditGratitudeEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyFloatingInteger("{=taom_mcm_BondsBanditGratitudeRadius}Bandit Gratitude Distance", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinBanditGratitudeRadius, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxBanditGratitudeRadius, "#0", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_BondsBanditGratitudeRadius_hint}How far from the battle, in map units, a settlement still thanks you. A settlement is usually about 17 units from its nearest neighbour. Default: 25.")]
    public float BondsBanditGratitudeRadius { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultBanditGratitudeRadius;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyBool("{=taom_mcm_BondsTroopHandoverEnabled}Troop Gifts Win Lords' Favour", Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_BondsTroopHandoverEnabled_hint}When you hand troops to another lord on the Donate Troops screen, that lord gains relation with you: half of the influence the gift is worth, at most 10, give or take one. A gift worth less than 2 earns nothing. Hand-overs stop paying a lord once 10 relation has landed with him that day; the Done that reaches 10 asks only for what is left, and Charm can raise it. A lord gains at most 10 a day from hand-overs, however the gift is split (Charm can raise the last Done). The same rule pays for troops given to a lord through a Lord Courtesies gift. Default: on.")]
    public bool BondsTroopHandoverEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultTroopHandoverEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_bonds_of_battle}Bonds of Battle")]
    [SettingPropertyInteger("{=taom_mcm_BondsOverpayRelationCap}Most Relation From One Barter Overpay", TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MinOverpayRelationCap, TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.MaxOverpayRelationCap, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_BondsOverpayRelationCap_hint}The most relation one barter can buy by offering more than the goods are worth. The loop buys at most 3 points in vanilla (the setting here), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change. Points cost more the further relation is from 0, so only a large overpay reaches the cap. Default: 15.")]
    public int BondsOverpayRelationCap { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultOverpayRelationCap;

    [SettingPropertyGroup("{=taom_mcm_group_battle_gear_loot}Battle Gear Loot", GroupOrder = 91)]
    [SettingPropertyBool("{=taom_mcm_EnableBattleGearLoot}Enable Battle Gear Loot", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableBattleGearLoot_hint}When on, the loot from enemies your party beats in a battle your side wins is the gear they fought in: each weapon, armour piece, horse and harness drops on its own roll, at the chance below. It replaces the usual random loot from fallen enemies; captured baggage, gold, prisoners and renown are unchanged. Heavy, elite and lord armour held back by Armour Acquisition, named weapons and banners never drop. Singleplayer only: in a co-op session the usual loot applies. Default: off.")]
    public bool EnableBattleGearLoot { get; set; } = TAOM.Features.BattleGearLoot.BattleGearLootSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_battle_gear_loot}Battle Gear Loot")]
    [SettingPropertyInteger("{=taom_mcm_BattleGearLootChancePercent}Gear Drop Chance (%)", TAOM.Features.BattleGearLoot.BattleGearLootSettingsProvider.MinChancePercent, TAOM.Features.BattleGearLoot.BattleGearLootSettingsProvider.MaxChancePercent, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_BattleGearLootChancePercent_hint}The chance, from 0 to 100%, that each item of each beaten enemy drops. In a battle fought beside allies you loot your share of the enemies, as usual. Default: 10.")]
    public int BattleGearLootChancePercent { get; set; } = TAOM.Features.BattleGearLoot.BattleGearLootSettingsProvider.DefaultChancePercent;

    // --- Personal Relations (docs/features/personal-relations.md, #862) ---
    //
    // The master switch is read on every relation read (it gates TaomDiplomacyModel.GetHeroesForEffectiveRelation, on the
    // main thread), through PersonalRelationSettingsProvider's one cached settings reference. The disband values are read
    // when a disband costs relation. All RequireRestart = false: nothing is applied once at start-up. GroupOrder 60 is
    // unused. Defaults live in PersonalRelationSettingsProvider so the fallback before MCM is up cannot drift from them.

    [SettingPropertyGroup("{=taom_mcm_group_personal_relations}Personal Relations", GroupOrder = 60)]
    [SettingPropertyBool("{=taom_mcm_PersonalRelationsEnabled}Enable Personal Relations", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_PersonalRelationsEnabled_hint}Your relation with a lord stays with that lord instead of passing to his clan leader, so a friendship with Faramir is not a friendship with Denethor. Affects only your own relations: relations between AI lords stay vanilla. Single player only: in a co-op session or on a dedicated server it stays vanilla. A save made before this was on is seeded once: on the first day this is on, each living lord whose personal relation with you is 0 takes his clan leader's value, and it is seeded again when this has been off for at least one in-game day. Off restores vanilla. Default: on.")]
    public bool PersonalRelationsEnabled { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_personal_relations}Personal Relations")]
    [SettingPropertyBool("{=taom_mcm_PersonalDisbandCostEnabled}Disbanding An Army Costs Relation", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_PersonalDisbandCostEnabled_hint}Paying influence to disband another lord's army costs you relation with the army's leader by the amounts below (vanilla is 4); each other lord in it costs vanilla's 1. Applies only in single player, and only while Enable Personal Relations is on. Default: on.")]
    public bool PersonalDisbandCostEnabled { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultDisbandCostEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_personal_relations}Personal Relations")]
    [SettingPropertyInteger("{=taom_mcm_PersonalDisbandLeaderMin}Disband Cost, Army Leader, Least", TAOM.Features.Relations.PersonalRelationSettingsProvider.MinCost, TAOM.Features.Relations.PersonalRelationSettingsProvider.MaxCost, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_PersonalDisbandLeaderMin_hint}The least relation you lose with an army's leader when you pay influence to disband it. The cost is a random whole number between the least and the most. If the least is above the most, the two swap. Applies only in single player, and only while Enable Personal Relations is on. Default: 28.")]
    public int PersonalDisbandLeaderMin { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultLeaderMin;

    [SettingPropertyGroup("{=taom_mcm_group_personal_relations}Personal Relations")]
    [SettingPropertyInteger("{=taom_mcm_PersonalDisbandLeaderMax}Disband Cost, Army Leader, Most", TAOM.Features.Relations.PersonalRelationSettingsProvider.MinCost, TAOM.Features.Relations.PersonalRelationSettingsProvider.MaxCost, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_PersonalDisbandLeaderMax_hint}The most relation you lose with an army's leader when you pay influence to disband it. Applies only in single player, and only while Enable Personal Relations is on. Default: 32.")]
    public int PersonalDisbandLeaderMax { get; set; } = TAOM.Features.Relations.PersonalRelationSettingsProvider.DefaultLeaderMax;

    // --- Battle Tactics / Fell Foes (#893) ---
    // The first property carries the GroupOrder, because MCM ignores one on any later property; no other group may
    // share it (FellFoesSettingsProviderTests pins that). Defaults live in FellFoesTunables so the fallback before
    // MCM is up cannot drift from them. All RequireRestart = false: the service reads them each second.

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes", GroupOrder = 61)]
    [SettingPropertyBool("{=taom_mcm_FellFoesCorneredRouters}Cornered Routers Fight", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesCorneredRouters_hint}A routing enemy soldier who has just been hit, with a few of your men right beside him and enough health left, turns and fights instead of running on, and routers close to him turn with him (a few at most). They keep fighting only while one of your men is still near them and some of their side still stand; once the whole side has broken they run again, so the battle still ends in a rout. Soldiers of cultures that never rout, and soldiers ordered to retreat, are not touched. Field battles, sieges and sally-outs. Default: on.")]
    public bool FellFoesCorneredRouters { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyInteger("{=taom_mcm_FellFoesCorneredMaxFoes}Cornered By No More Than", 1, 10, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesCorneredMaxFoes_hint}A routing soldier turns to fight only while this many of your men or fewer are within a few steps of him. Default: 3.")]
    public int FellFoesCorneredMaxFoes { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultCorneredMaxFoes;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyBool("{=taom_mcm_FellFoesArchersTargetPlayer}Idle Archers Target You", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesArchersTargetPlayer_hint}Enemy archers that have nothing to shoot at, are steady, and are within bow range of you may fix on you and shoot, for a few seconds at a time. A cap keeps the volley small. Archers of a team that is making a stand against horse are left to that stand. Field battles, sieges and sally-outs. Default: on.")]
    public bool FellFoesArchersTargetPlayer { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyFloatingInteger("{=taom_mcm_FellFoesArcherChance}Archer Chance Each Second", 0f, 1f, "#0.00", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesArcherChance_hint}The chance each second that an idle archer in range fixes on you. 0 never, 1 always. Default: 0.25.")]
    public float FellFoesArcherChance { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultArcherChance;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyInteger("{=taom_mcm_FellFoesArcherMaxHolding}Archers On You At Once", 1, 30, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesArcherMaxHolding_hint}The most enemy archers that hold you as their target at the same moment. Default: 8.")]
    public int FellFoesArcherMaxHolding { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultArcherMaxHolding;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyBool("{=taom_mcm_FellFoesStragglersLeave}Last Stragglers Leave The Field", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesStragglersLeave_hint}In a large field battle, when only a few enemy soldiers are left, none of them a hero and none still to arrive, the survivors are sent off the field after a pause so the battle ends in your win and they can be taken prisoner. Sieges and sally-outs are not changed. Default: on.")]
    public bool FellFoesStragglersLeave { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyInteger("{=taom_mcm_FellFoesStragglerMaxCount}Stragglers, Most", 1, 20, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesStragglerMaxCount_hint}The cleanup starts when this many enemy soldiers or fewer are left. Default: 3.")]
    public int FellFoesStragglerMaxCount { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultStragglerMaxCount;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyFloatingInteger("{=taom_mcm_FellFoesStragglerHoldSeconds}Straggler Pause (seconds)", 5f, 300f, "#0", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesStragglerHoldSeconds_hint}Seconds the battle must stay down to the last few before the survivors are sent off, one a second. Default: 30.")]
    public float FellFoesStragglerHoldSeconds { get; set; } = TAOM.Features.FellFoes.Domain.FellFoesTunables.DefaultStragglerHoldSeconds;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_fell_foes}Fell Foes")]
    [SettingPropertyBool("{=taom_mcm_FellFoesDebug}Fell Foes Debug Log", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_FellFoesDebug_hint}Writes a [FellFoes] line each second something is stopped, held or sent off, and whether held archers really are aiming at you. For checking the feature; leave off in normal play. Default: off.")]
    public bool FellFoesDebug { get; set; } = false;
    // --- Tokens of Esteem (docs/features/tokens-of-esteem.md, #863) ---
    //
    // Gifts of goods and gold for relation. Every value is read when a conversation line is shown or a gift is given, through
    // GiftSettingsProvider's one cached settings reference, so none needs a restart. GroupOrder 62 follows the
    // Fell Foes group at 61; 100 is the Map Tools group. Defaults
    // and ranges live in GiftSettingsProvider (the purse range in the gift band, GiftService.PurseGoldPerPoint and
    // PurseMaxPoints) so the fallback before MCM is up and the hint texts cannot drift from them.

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem", GroupOrder = 62)]
    [SettingPropertyBool("{=taom_mcm_GiftsEnabled}Enable Tokens of Esteem", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_GiftsEnabled_hint}Adds \"I have a gift for you.\" to the talk with the companions in your party and, on the diplomacy branch, with lords who are not children, not prisoners, not in your own clan and not at war with you. Goods the catalog lists, from your inventory, win relation; companions also take gold purses. A co-op client, or a peer running a co-op mod TAOM cannot probe, sees no gift line. Default: on.")]
    public bool GiftsEnabled { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyInteger("{=taom_mcm_GiftCooldownDays}Days Between Gifts To One Hero", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinCooldownDays, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxCooldownDays, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_GiftCooldownDays_hint}After a gift, the same hero takes no other for this many days, so goods cannot be turned straight into relation. Whenever Personal Relations is not engaged (switched off, a co-op host, or a save not yet seeded) a clan shares one relation, so the days count for the whole clan. The count is saved with the campaign. Default: 7.")]
    public int GiftCooldownDays { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultCooldownDays;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyInteger("{=taom_mcm_GiftRelationCap}Most Relation One Gift Asks For", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinRelationCap, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxRelationCap, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_GiftRelationCap_hint}The most relation one gift asks for, after the taste bonus and the scaling by relation (above relation 0 a gift adds (90 minus relation) / 90 of its worth, so a 1-point gift stops working near relation 45 and nothing helps at 90). A companion's gift starts at 1 to 5 by the item's value; a lord's starts at the catalog's value for the goods (1 to 10), and a hero who prizes the goods adds a bonus. Your Charm can add to what lands. Default: 15.")]
    public int GiftRelationCap { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultRelationCap;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyBool("{=taom_mcm_GiftAlignmentRefusal}Free Lords Refuse Gifts From Evil Players", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_GiftAlignmentRefusal_hint}A lord of a Free realm will not take a gift from a player who sides with the Shadow. The lord refuses with no penalty. Realms and cultures that side with neither never refuse. Default: on.")]
    public bool GiftAlignmentRefusal { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultAlignmentRefusalEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyInteger("{=taom_mcm_GiftPurseSmall}Small Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_GiftPurseSmall_hint}The gold in the smallest purse you can give a companion. The gold leaves you and goes to the companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold), before the scaling and the cap. Between 500 and 5000 gold. Default: 500.")]
    public int GiftPurseSmall { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseSmall;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyInteger("{=taom_mcm_GiftPurseMedium}Medium Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_GiftPurseMedium_hint}The gold in the middle purse you can give a companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold). Between 500 and 5000 gold. Default: 2000.")]
    public int GiftPurseMedium { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseMedium;

    [SettingPropertyGroup("{=taom_mcm_group_tokens_of_esteem}Tokens of Esteem")]
    [SettingPropertyInteger("{=taom_mcm_GiftPurseLarge}Large Gold Purse", TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MinPurse, TAOM.Features.TokensOfEsteem.GiftSettingsProvider.MaxPurse, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_GiftPurseLarge_hint}The gold in the largest purse you can give a companion. A purse is worth 1 relation per 500 gold, at most 10 (5000 gold). Between 500 and 5000 gold. Default: 5000.")]
    public int GiftPurseLarge { get; set; } = TAOM.Features.TokensOfEsteem.GiftSettingsProvider.DefaultPurseLarge;

    // --- Battle Tactics / Lost Footing (#894, phase 1: the census only) ---
    // The first property carries the GroupOrder, because MCM ignores one on any later property; no other group may
    // share it (LostFootingSettingsProviderTests pins that). GroupOrder 64 follows the groups at 62 and 63. The scan numbers are
    // compiled defaults in LostFootingTunables, not settings: nobody needs to tune a measurement.

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_lost_footing}Lost Footing", GroupOrder = 64)]
    [SettingPropertyBool("{=taom_mcm_LostFootingCensus}Lost Footing Census", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_LostFootingCensus_hint}Counts soldiers, mounts and creatures that are stuck in a battle, standing off the walkable ground or wading in water without moving, and writes what it finds to the log with a [LostFooting] tag, with a summary when the battle ends. It only watches: it never moves or changes anything in the battle. With the rescue on, the start line and the summary are still written. Field battles, sieges and sally-outs. Default: on.")]
    public bool LostFootingCensus { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_lost_footing}Lost Footing")]
    [SettingPropertyBool("{=taom_mcm_LostFootingRescue}Lost Footing Rescue", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_LostFootingRescue_hint}Moves a soldier who stays stuck back to where he can walk: off the walkable ground or still in water for 10 seconds, he steps to the nearest good spot within 6 m at his own height; still stuck after 30 seconds, he moves to his place in his formation while you are at least 25 m away. A riderless horse gets the short step only. Never the player, fleeing men, creatures, men working a ladder or machine, or men holding a set post or waiting at a ladder. Field battles, sieges and sally-outs, not co-op. Default: on.")]
    public bool LostFootingRescue { get; set; } = true;
    // --- Good Deeds (docs/features/good-deeds.md, #864) ---
    //
    // Relation earned by releasing a captured lord, donating a hero prisoner to a settlement and winning a town's tournament.
    // Every value is read when the deed happens, through GoodDeedsSettingsProvider's one cached settings reference, so none
    // needs a restart. GroupOrder 65 follows Lost Footing at 64; 100 is the Map Tools group. Defaults and ranges live
    // in GoodDeedsSettingsProvider so the fallback before MCM is up and the hint texts cannot drift from them.

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds", GroupOrder = 65)]
    [SettingPropertyBool("{=taom_mcm_GoodDeedsEnabled}Enable Good Deeds", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsEnabled_hint}Turns the three rewards on or off together: relation for releasing a captured lord, for donating a hero prisoner to a settlement and for winning a town's tournament. Each reward also has its own switch below. Applies on the host in co-op. Default: on.")]
    public bool GoodDeedsEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyBool("{=taom_mcm_GoodDeedsReleaseEnabled}Reward Releasing A Lord", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseEnabled_hint}Releasing a captured lord from your party (in conversation or on the party screen) or from your own dungeon earns relation with the lord, with the lord's living spouse and with the lord's clan leader. The post-battle \"free to go\" choice, ransom, peace, escape and any AI release earn nothing. Default: on.")]
    public bool GoodDeedsReleaseEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyBool("{=taom_mcm_GoodDeedsDonationEnabled}Reward Donating A Hero Prisoner", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsDonationEnabled_hint}Handing a hero prisoner to a settlement adds a bonus to the relation vanilla gives with the settlement owner's clan leader. Default: on.")]
    public bool GoodDeedsDonationEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyBool("{=taom_mcm_GoodDeedsTournamentEnabled}Reward Winning A Tournament", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsTournamentEnabled_hint}Winning a town's tournament earns relation with the owner's clan leader (unless your clan owns the town) and with every notable in the town. Default: on.")]
    public bool GoodDeedsTournamentEnabled { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyInteger("{=taom_mcm_GoodDeedsReleaseLordAmount}Release: Relation With The Lord", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseLordAmount_hint}Relation asked for with the lord you release, before the alignment multiplier. Vanilla pays another 4 in the conversation. Your Charm can add to what lands. Default: 5.")]
    public int GoodDeedsReleaseLordAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseLordAmount;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyInteger("{=taom_mcm_GoodDeedsReleaseSpouseAmount}Release: Relation With The Lord's Spouse", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseSpouseAmount_hint}Relation asked for with the lord's living spouse, before the alignment multiplier. Default: 2.")]
    public int GoodDeedsReleaseSpouseAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseSpouseAmount;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyInteger("{=taom_mcm_GoodDeedsReleaseLeaderAmount}Release: Relation With The Lord's Clan Leader", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseLeaderAmount_hint}Relation asked for with the leader of the lord's clan, before the alignment multiplier. While Personal Relations is off, the lord and the clan leader share one relation, so only the larger of the two is paid. Default: 2.")]
    public int GoodDeedsReleaseLeaderAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseLeaderAmount;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyInteger("{=taom_mcm_GoodDeedsDonationAmount}Donation: Bonus Relation", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxAmount, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsDonationAmount_hint}Relation added to what vanilla gives with the settlement owner's clan leader for each hero prisoner donated, before the alignment multiplier. Vanilla itself pays nothing when the captive likes the owner. Default: 5.")]
    public int GoodDeedsDonationAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationAmount;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyInteger("{=taom_mcm_GoodDeedsTournamentAmount}Tournament: Relation Per Person", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinAmount, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxTournamentAmount, Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsTournamentAmount_hint}Relation asked for with the town owner's clan leader and with each notable in the town when you win its tournament, before the alignment multiplier. Between 0 and 10. Default: 1.")]
    public int GoodDeedsTournamentAmount { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentAmount;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsReleaseSame}Release: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseSame_hint}Multiplier on every release reward when you and the lord are on the same side (both Free, or both Shadow). Results round half away from zero, and 0 grants nothing. Default: 1.00.")]
    public float GoodDeedsReleaseSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseSame;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsReleaseNeutral}Release: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseNeutral_hint}Multiplier on every release reward when you or the lord is on neither side. Default: 0.75.")]
    public float GoodDeedsReleaseNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseNeutral;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsReleaseOpposite}Release: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 11, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsReleaseOpposite_hint}Multiplier on every release reward when you and the lord are on opposite sides, so freeing an enemy of your cause earns little. Default: 0.50.")]
    public float GoodDeedsReleaseOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultReleaseOpposite;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsDonationSame}Donation: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 12, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsDonationSame_hint}Multiplier on the donation relation when the captive and the settlement owner are on the same side. Zero means no reward, and no vanilla relation either, for jailing a friend of the owner. Default: 0.00.")]
    public float GoodDeedsDonationSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationSame;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsDonationNeutral}Donation: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 13, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsDonationNeutral_hint}Multiplier on the donation relation when the captive or the settlement owner is on neither side. Default: 1.00.")]
    public float GoodDeedsDonationNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationNeutral;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsDonationOpposite}Donation: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 14, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsDonationOpposite_hint}Multiplier on the donation relation (vanilla's gain plus the bonus) when the captive is on the opposite side to the settlement owner, such as a Mordor captive handed to Minas Tirith. Default: 2.00.")]
    public float GoodDeedsDonationOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultDonationOpposite;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsTournamentSame}Tournament: Same Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 15, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsTournamentSame_hint}Multiplier on the tournament relation with an owner or notable on your side. Default: 1.00.")]
    public float GoodDeedsTournamentSame { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentSame;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsTournamentNeutral}Tournament: Neutral Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 16, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsTournamentNeutral_hint}Multiplier on the tournament relation with an owner or notable who, or whose realm, is on neither side. Default: 1.00.")]
    public float GoodDeedsTournamentNeutral { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentNeutral;

    [SettingPropertyGroup("{=taom_mcm_group_good_deeds}Good Deeds")]
    [SettingPropertyFloatingInteger("{=taom_mcm_GoodDeedsTournamentOpposite}Tournament: Opposite Side Multiplier", TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MinMultiplier, TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.MaxMultiplier, "0.00", Order = 17, RequireRestart = false,
        HintText = "{=taom_mcm_GoodDeedsTournamentOpposite_hint}Multiplier on the tournament relation with an owner or notable on the opposite side, who does not cheer a champion of the other cause. Default: 0.00.")]
    public float GoodDeedsTournamentOpposite { get; set; } = TAOM.Features.GoodDeeds.GoodDeedsSettingsProvider.DefaultTournamentOpposite;

    // --- Long Memory (docs/features/long-memory.md, #865) ---
    //
    // Extra relation lost with a village owner's clan leader and the village's notables for raiding it or forcing it to give
    // goods or recruits, on top of vanilla's own loss. Every value is read when the wrong happens, through
    // LongMemorySettingsProvider's one cached settings reference, so none needs a restart. The sliders hold magnitudes (the
    // relation to lose), and the service negates them. GroupOrder 67 follows Fire and Tribute at 66.
    // Defaults and ranges live in LongMemorySettingsProvider so the fallback before MCM is up and the hint texts cannot drift.

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory", GroupOrder = 67)]
    [SettingPropertyBool("{=taom_mcm_LongMemoryEnabled}Enable Long Memory", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryEnabled_hint}Turns on the extra relation loss, on top of vanilla's own, that a village's owner and notables hold against you when you raid the village or force it to give goods or recruits. Villages your clan owns are never affected, and neither are raids you do not lead. Applies on the host in co-op. Default: on.")]
    public bool LongMemoryEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyBool("{=taom_mcm_LongMemoryRaidEnabled}Remember Raids", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryRaidEnabled_hint}A raid you lead costs relation with the owner's clan leader and with every notable of the village, in proportion to the damage the raid did. A raid that did none costs nothing. Default: on.")]
    public bool LongMemoryRaidEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyBool("{=taom_mcm_LongMemoryCoercionEnabled}Remember Forced Takings", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryCoercionEnabled_hint}Forcing a village to give goods or recruits costs relation with the owner's clan leader and with every notable, whether the villagers resist or give way. A resisting village that wins the fight costs nothing. Default: on.")]
    public bool LongMemoryCoercionEnabled { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyInteger("{=taom_mcm_LongMemoryRaidOwnerAmount}Raid: Relation Lost With The Owner's Clan Leader", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxRaidAmount, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryRaidOwnerAmount_hint}Relation lost with the clan leader who owns the village after a whole raid, before the alignment multiplier. Vanilla takes its own share as well. Between 0 and 100. Default: 50.")]
    public int LongMemoryRaidOwnerAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidOwnerAmount;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyInteger("{=taom_mcm_LongMemoryRaidNotableAmount}Raid: Relation Lost With Each Notable", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxRaidAmount, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryRaidNotableAmount_hint}Relation lost with every notable of the village after a whole raid, before the alignment multiplier. Vanilla takes its own share as well. Between 0 and 100. Default: 50.")]
    public int LongMemoryRaidNotableAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultRaidNotableAmount;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyInteger("{=taom_mcm_LongMemoryCoercionOwnerAmount}Forced Taking: Relation Lost With The Owner's Clan Leader", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxCoercionAmount, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryCoercionOwnerAmount_hint}Relation lost with the clan leader who owns the village each time you force it to give goods or recruits, before the alignment multiplier. Between 0 and 50. Default: 20.")]
    public int LongMemoryCoercionOwnerAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionOwnerAmount;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyInteger("{=taom_mcm_LongMemoryCoercionNotableAmount}Forced Taking: Relation Lost With Each Notable", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinAmount, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxCoercionAmount, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryCoercionNotableAmount_hint}Relation lost with every notable of the village each time you force it to give goods or recruits, before the alignment multiplier. Between 0 and 50. Default: 25.")]
    public int LongMemoryCoercionNotableAmount { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultCoercionNotableAmount;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyFloatingInteger("{=taom_mcm_LongMemorySame}Same Side Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemorySame_hint}Multiplier on the loss when you and the village owner's realm are on the same side, both Free or both Evil: the harshest pairing. Default: 1.00.")]
    public float LongMemorySame { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultSame;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyFloatingInteger("{=taom_mcm_LongMemoryNeutral}Neutral Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryNeutral_hint}Multiplier on the loss when you or the village owner's realm is on neither side. Default: 0.50.")]
    public float LongMemoryNeutral { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultNeutral;

    [SettingPropertyGroup("{=taom_mcm_group_long_memory}Long Memory")]
    [SettingPropertyFloatingInteger("{=taom_mcm_LongMemoryOpposite}Opposite Side Multiplier", TAOM.Features.LongMemory.LongMemorySettingsProvider.MinMultiplier, TAOM.Features.LongMemory.LongMemorySettingsProvider.MaxMultiplier, "0.00", Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_LongMemoryOpposite_hint}Multiplier on the loss when you and the village owner's realm are on opposite sides, such as an Evil warband raiding a Free village, where raiding is expected. Default: 0.10.")]
    public float LongMemoryOpposite { get; set; } = TAOM.Features.LongMemory.LongMemorySettingsProvider.DefaultOpposite;

    // --- Hearth Roll (docs/features/hearth-roll.md, #906) ---
    //
    // GroupOrder 68 follows Long Memory at 67. The switch is read through HearthRollSettingsProvider's one cached settings
    // reference each time a clan member row refreshes, so it needs no restart.

    [SettingPropertyGroup("{=taom_mcm_group_hearth_roll}Hearth Roll", GroupOrder = 69)]
    [SettingPropertyBool("{=taom_mcm_EnableUnhappyCompanionTint}Tint Unhappy Companions", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_EnableUnhappyCompanionTint_hint}On the Members tab of the clan screen, gives the row of a companion of your clan a faint red tint when their relation with you has fallen below zero, a warning before they leave. The age beside each child's name is always shown. Applies the next time the clan screen opens. Default: on.")]
    public bool EnableUnhappyCompanionTint { get; set; } = true;
    // --- Prisoner Banners (docs/features/prisoner-banners.md, #902) ---
    //
    // The switch is read through PrisonerBannersSettingsProvider's one cached settings
    // reference each time a party screen row refreshes, so it needs no restart. A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_prisoner_banners}Prisoner Banners", GroupOrder = 75)]
    [SettingPropertyBool("{=taom_mcm_PrisonerBannersEnabled}Show Prisoner Banners", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_PrisonerBannersEnabled_hint}On the party screen, a captive lord's row shows the clan's banner at the top left of the portrait, the kingdom's banner at the top right (when the clan belongs to a kingdom), and a gold noble icon at the bottom left when the lord leads the clan. Applies the next time the party screen opens. Default: on.")]
    public bool PrisonerBannersEnabled { get; set; } = TAOM.Features.PrisonerBanners.PrisonerBannersSettingsProvider.DefaultEnabled;
    // --- Warden's Tally (docs/features/wardens-tally.md, #903) ---
    //
    // Read once each time a town or castle dungeon menu opens, through WardensTallySettingsProvider's one cached settings
    // reference, so it needs no restart. GroupOrder 80 is used by no other group (79 is Inn Rounds; 100 is Map Tools).
    // A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_warden_s_tally}Warden's Tally", GroupOrder = 80)]
    [SettingPropertyBool("{=taom_mcm_WardensTallyEnabled}Show Warden's Tally", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_WardensTallyEnabled_hint}In the dungeon menu of a town or castle, adds under the engine's own line how many prisoners the dungeon holds against its capacity, how many of them are lords, each lord's chance to escape each day with the terms behind it (your clan holding him halves it; the governor's perks change it), and a line for a lord whose realm has a War of the Ring escape boost, or whom nothing lets escape. It only shows the odds the game already uses and changes no rule. Applies the next time the menu opens. Default: on.")]
    public bool WardensTallyEnabled { get; set; } = TAOM.Features.WardensTally.WardensTallySettingsProvider.DefaultEnabled;
    // --- Sworn Anew (docs/features/sworn-anew.md, #869) ---
    //
    // Read at each conversation question (the diplomacy option and the ruler's prisoner line), through
    // SwornAnewSettingsProvider's one cached settings reference, so it needs no restart. GroupOrder 71 is used by no other group.
    // A new setting, so the default (on) reaches every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_sworn_anew}Sworn Anew", GroupOrder = 71)]
    [SettingPropertyBool("{=taom_mcm_SwornAnewEnabled}Enable Sworn Anew", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_SwornAnewEnabled_hint}When enabled (default), a captured lord can be asked to join your kingdom, in a conversation or from the party screen, and serves it instead of being ransomed. The lord must hold a clan of another kingdom (not its ruler, and not a mercenary or minor clan), must be held by your party or in a settlement your clan owns, and must not be a lord who can never be captured, such as Sauron and the Nazgul. A Free Peoples lord is never asked to join an Evil realm, and an Evil lord is never asked to join a Free one. The base game's blood feud rule still applies. When off, the base game's rules apply: only a kingdom's ruler can recruit a captured lord, through its 'join us, and be set free' line, which checks no realm. When on, both lines pass this check, and the ruler-only line also needs the lord held by your party or in your clan's dungeon. Applies at once, no restart.")]
    public bool SwornAnewEnabled { get; set; } = TAOM.Features.SwornAnew.SwornAnewSettingsProvider.DefaultEnabled;
    // --- Kingdom Allegiance (docs/features/kingdom-allegiance.md, #1111) ---
    //
    // Read at each AI join score and once at new-game relation seeding, through KingdomAllegianceSettingsProvider's one cached
    // settings reference, so it needs no restart. GroupOrder 86 is used by no other group. A new setting, so the default (on)
    // reaches every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_kingdom_allegiance}Kingdom Allegiance", GroupOrder = 86)]
    [SettingPropertyBool("{=taom_kingdom_allegiance_mcm_enabled}Enable Kingdom Allegiance", Order = 0, RequireRestart = false,
        HintText = "{=taom_kingdom_allegiance_mcm_enabled_hint}When enabled (default), lords keep to their side of the war. An AI clan of the Free Peoples never joins, defects to or serves as mercenaries for an Evil realm, and an Evil clan never does so for a Free realm. Neutral clans and realms may deal with either side. A new campaign also starts with no friendships between Free and Evil lords: each one becomes enmity of the same strength. Your own clan and a realm you lead keep the base game's rules. Clans that already serve a realm of the other side stay where they are. When off, the base game's rules apply. Applies at once, no restart; the friendships part applies to new campaigns only.")]
    public bool KingdomAllegianceEnabled { get; set; } = TAOM.Features.KingdomAllegiance.KingdomAllegianceSettingsProvider.DefaultEnabled;

    // --- Hostile Peace (docs/features/hostile-peace.md) ---
    //
    // Read live at each question through HostilePeaceSettingsProvider, so none needs a restart. MCM wins over
    // ModuleData/diplomacy/hostile_peace.json when MCM is loaded. GroupOrder 101 is used by no other group. New settings, so the
    // defaults (on) reach every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_hostile_peace}Hostile Peace", GroupOrder = 101)]
    [SettingPropertyBool("{=taom_hostile_peace_mcm_caravans}No Caravan Trade With Enemy Realms", Order = 0, RequireRestart = false,
        HintText = "{=taom_hostile_peace_mcm_caravans_hint}When enabled (default), caravans of a Free realm do not trade in the towns of a realm of the Shadow, and the reverse, while the two are at peace before the War of the Ring. Realms that are not sworn enemies trade as before. When off, the base game's rules apply. Applies at once, no restart.")]
    public bool HostilePeaceBlockCaravanTrade { get; set; } = TAOM.Features.HostilePeace.HostilePeaceSettingsProvider.DefaultBlockCaravanTrade;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_hostile_peace}Hostile Peace", GroupOrder = 101)]
    [SettingPropertyBool("{=taom_hostile_peace_mcm_agreements}No Trade Agreements With Enemy Realms", Order = 1, RequireRestart = false,
        HintText = "{=taom_hostile_peace_mcm_agreements_hint}When enabled (default), a Free realm and a realm of the Shadow never sign a trade agreement, neither by the AI nor by your own kingdom's vote. An agreement signed before this was switched on runs to its end. When off, the base game's rules apply. Applies at once, no restart.")]
    public bool HostilePeaceBlockTradeAgreements { get; set; } = TAOM.Features.HostilePeace.HostilePeaceSettingsProvider.DefaultBlockTradeAgreements;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_hostile_peace}Hostile Peace", GroupOrder = 101)]
    [SettingPropertyBool("{=taom_hostile_peace_mcm_access}Enemy Realms Close Their Gates", Order = 2, RequireRestart = false,
        HintText = "{=taom_hostile_peace_mcm_access_hint}When enabled (default), the towns and castles of a sworn enemy realm treat you as they would in war, even while your realms are at peace: you enter a town only in disguise, a castle not at all, and you cannot trade, recruit or rest in its villages. Your own fiefs are not affected. When off, the base game's rules apply. Applies at once, no restart.")]
    public bool HostilePeaceLimitTownAccess { get; set; } = TAOM.Features.HostilePeace.HostilePeaceSettingsProvider.DefaultLimitTownAccess;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_hostile_peace}Hostile Peace", GroupOrder = 101)]
    [SettingPropertyBool("{=taom_hostile_peace_mcm_alliances}Allow Player Alliances With Anyone", Order = 3, RequireRestart = false,
        HintText = "{=taom_hostile_peace_mcm_alliances_hint}When off (default), a kingdom you rule cannot ally with a realm on the other side of the war: a Free kingdom does not ally with the Shadow, and the reverse. Realms of the Neutral side can ally with anyone. A kingdom you founded takes the side of its culture. Alliances you already have are kept. When on, your kingdom may ally with any realm, as before. AI realms keep their own rules either way. Applies at once, no restart. This is the default for new campaigns. With the Alignment rules start option on Standard, a campaign follows this switch live; Strict or Relaxed fixes all four alignment switches for that campaign. Older saves follow this setting.")]
    public bool AllowPlayerAlliancesWithAnyone { get; set; } = TAOM.Features.HostilePeace.HostilePeaceSettingsProvider.DefaultAllowPlayerAlliancesWithAnyone;

    // --- Realms (docs/features/realms.md) ---
    //
    // Feudal titles, claims, vassal duties and offices. Prices and payments are percentages of realms_titles.xml, which stays
    // the baseline. Read when a price is quoted, on the daily and weekly ticks and when a name is built, through
    // RealmsSettingsProvider's one cached settings reference, so none needs a restart. GroupOrder 87 is used by no other
    // group. New settings, so the defaults reach every install.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms", GroupOrder = 87)]
    [SettingPropertyInteger("{=taom_realms_mcm_claim_price}Claim Price (%)", TAOM.Features.Realms.RealmsSettingsProvider.MinCostPercent, TAOM.Features.Realms.RealmsSettingsProvider.MaxCostPercent, Order = 0, RequireRestart = false,
        HintText = "{=taom_realms_mcm_claim_price_hint}The price of buying a claim on a title, as a percentage of its price in realms_titles.xml (40 for a Barony up to 500 for a Kingdom, in your people's own resource). AI lords pay for theirs in gold, at the same percentage. Between 10 and 500. Default: 100.")]
    public int RealmsClaimPricePercent { get; set; } = TAOM.Features.Realms.RealmsSettingsProvider.DefaultPercent;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyInteger("{=taom_realms_mcm_influence_cost}Title Influence Cost (%)", TAOM.Features.Realms.RealmsSettingsProvider.MinCostPercent, TAOM.Features.Realms.RealmsSettingsProvider.MaxCostPercent, Order = 1, RequireRestart = false,
        HintText = "{=taom_realms_mcm_influence_cost_hint}The influence it costs to usurp, form, restore or revoke a title, as a percentage of its cost in realms_titles.xml. Restoring Gondor's throne for Aragorn costs 2500 at 100. Applies to AI lords too. Between 10 and 500. Default: 100.")]
    public int RealmsInfluenceCostPercent { get; set; } = TAOM.Features.Realms.RealmsSettingsProvider.DefaultPercent;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_ai_claims}AI Lords Press Claims", Order = 2, RequireRestart = false,
        HintText = "{=taom_realms_mcm_ai_claims_hint}When on, AI lords buy claims, usurp the titles they claim, form vacant titles their side holds and restore thrones, each week. When off, only you do; claims they already hold stay. Default: on.")]
    public bool RealmsAiClaims { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_tribute_recv}Tribute From Vassals", Order = 3, RequireRestart = false,
        HintText = "{=taom_realms_mcm_tribute_recv_hint}When on, each clan whose lords hold titles directly below your clan's, in your realm, pays your clan a share of its daily income from fiefs. It shows in the expected gold change. Applies to AI lords too. Receiving and paying are separate: with only this one on, lieges gain the gold without their vassals losing it. Default: on.")]
    public bool RealmsTributeReceive { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyInteger("{=taom_realms_mcm_tribute_recv_pct}Tribute From Vassals (%)", TAOM.Features.Realms.RealmsSettingsProvider.MinTributePercent, TAOM.Features.Realms.RealmsSettingsProvider.MaxTributePercent, Order = 4, RequireRestart = false,
        HintText = "{=taom_realms_mcm_tribute_recv_pct_hint}The share of a vassal clan's daily income from fiefs that its liege clan receives. A clan with more than one liege clan splits it among them. Between 1 and 25. Default: 5.")]
    public int RealmsTributeReceivePercent { get; set; } = TAOM.Features.Realms.RealmsSettingsProvider.DefaultTributePercent;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_tribute_pay}Tribute To Liege", Order = 5, RequireRestart = false,
        HintText = "{=taom_realms_mcm_tribute_pay_hint}When on, your clan pays each liege clan, the clan whose lord holds a title directly above one of yours in your realm, a share of its daily income from fiefs. It shows in the expected gold change. Applies to AI lords too. A clan that cannot pay its whole tribute pays nothing that day. With only this one on, vassals lose the gold without their lieges gaining it. Default: on.")]
    public bool RealmsTributePay { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyInteger("{=taom_realms_mcm_tribute_pay_pct}Tribute To Liege (%)", TAOM.Features.Realms.RealmsSettingsProvider.MinTributePercent, TAOM.Features.Realms.RealmsSettingsProvider.MaxTributePercent, Order = 6, RequireRestart = false,
        HintText = "{=taom_realms_mcm_tribute_pay_pct_hint}The share of a clan's daily income from fiefs it pays its liege clans, split among them when it has more than one. Between 1 and 25. Default: 5.")]
    public int RealmsTributePayPercent { get; set; } = TAOM.Features.Realms.RealmsSettingsProvider.DefaultTributePercent;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyInteger("{=taom_realms_mcm_office_influence}Office Influence (%)", TAOM.Features.Realms.RealmsSettingsProvider.MinPaymentPercent, TAOM.Features.Realms.RealmsSettingsProvider.MaxPaymentPercent, Order = 7, RequireRestart = false,
        HintText = "{=taom_realms_mcm_office_influence_hint}The influence an office such as the Steward of Gondor or a Marshal of the Mark pays its holder's clan each week, as a percentage of its amount in realms_titles.xml. 0 stops it. Between 0 and 300. Default: 100.")]
    public int RealmsOfficeInfluencePercent { get; set; } = TAOM.Features.Realms.RealmsSettingsProvider.DefaultPercent;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_resentment}Resentment Over Fiefs", Order = 8, RequireRestart = false,
        HintText = "{=taom_realms_mcm_resentment_hint}When on, a lord who holds the title of a town or castle that another clan owns likes its owner a little less each week, and the town loses a little loyalty. Default: on.")]
    public bool RealmsResentment { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_liege_pull}Liege's Pull", Order = 9, RequireRestart = false,
        HintText = "{=taom_realms_mcm_liege_pull_hint}When on, a vassal of another clan likes the holder of their liege title a little more each week, up to a relation of 30. Default: on.")]
    public bool RealmsLiegePull { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_army_discount}Liege Army Discount", Order = 10, RequireRestart = false,
        HintText = "{=taom_realms_mcm_army_discount_hint}When on, calling a vassal to an army costs their liege half the influence. It does not stack with the King's Muster ruler discount: when both apply, the larger one is used. When off, other discounts such as culture feats and the King's Muster still apply. Default: on.")]
    public bool RealmsLiegeArmyDiscount { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_family_grants}Lands for Sons and Daughters", Order = 11, RequireRestart = false,
        HintText = "{=taom_realms_mcm_family_grants_hint}When on, AI lords hand lesser titles to their grown children. When off, they keep them; titles already handed down stay, and so do the grants made when a campaign starts, such as Boromir's West Osgiliath. Default: on.")]
    public bool RealmsFamilyGrants { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_title_styles}Lords Styled by Their Titles", Order = 12, RequireRestart = false,
        HintText = "{=taom_realms_mcm_title_styles_hint}When on, lord parties and the name above a conversation carry the lord's style and title, such as Steward Denethor of Gondor. When off, they show the plain name; the encyclopedia still lists every title. Party names change within an in-game hour. Default: on.")]
    public bool RealmsTitleStyles { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_rename_own}Your Realm Takes Your Title's Name", Order = 13, RequireRestart = false,
        HintText = "{=taom_realms_mcm_rename_own_hint}When on, a realm you rule is named after the higher title you hold, as Erebor becomes Durin's Folk, and takes its own name back when you lose that title. When off, your realm keeps its own name; one already renamed takes it back within a day. Default: on.")]
    public bool RealmsRenameOwnRealm { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_rename_ai}AI Realms Take Their Titles' Names", Order = 14, RequireRestart = false,
        HintText = "{=taom_realms_mcm_rename_ai_hint}When on, a realm ruled by an AI lord is named after the higher title its ruler holds. When off, every such realm keeps its own name; one already renamed takes it back within a day. Default: on.")]
    public bool RealmsRenameAiRealms { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyDropdown("{=taom_realms_mcm_own_banner}Your Realm's Banner", Order = 15, RequireRestart = false,
        HintText = "{=taom_realms_mcm_own_banner_hint}The banner of a realm you rule. Title banner (default): the banner of your primary title when it has one, such as the Kingdom of Gondor's, else the realm's own. Realm's own banner: the realm keeps the banner it has; a realm you founded keeps the banner you made. Your house banner: the realm flies your clan's own banner on its towns, in battle and on the clan screen, even a realm you inherit, and follows when you edit it. Your parties on the map fly your house banner either way. Only the title banner choice lets a coronation give your clan and realm the crowned house's banner. Changes show within a day.")]
    public Dropdown<string> RealmsOwnRealmBanner { get; set; } = new Dropdown<string>(
        new[] { "Title banner", "Realm's own banner", "Your house banner" }, 0);

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_realms}Realms")]
    [SettingPropertyBool("{=taom_realms_mcm_ai_banners}AI Realms Fly Title Banners", Order = 16, RequireRestart = false,
        HintText = "{=taom_realms_mcm_ai_banners_hint}When on, a realm ruled by an AI lord flies the banner of its ruler's primary title when it has one. When off, every such realm keeps its own banner; one already changed takes it back within a day. Default: on.")]
    public bool RealmsAiRealmBanners { get; set; } = true;

    // --- Provender-master (docs/features/provender.md, #908) ---
    //
    // Standing orders and morale options for the hero in the party's Quartermaster role. Every value is read when an order is
    // judged, through ProvenderSettingsProvider's one cached settings reference, so none needs a restart. GroupOrder 70
    // follows Hearth Roll at 69; 100 is the Map Tools group. Defaults and ranges live in ProvenderSettingsProvider so the
    // fallback before MCM is up and the hint texts cannot drift from them. These are new settings, so no existing TAOM.json
    // holds an older default for any of them.

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master", GroupOrder = 70)]
    [SettingPropertyBool("{=taom_mcm_ProvenderEnabled}Enable Provender-master", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderEnabled_hint}Lets the hero you name Quartermaster (never you, and only while that hero is in your party) take standing orders in conversation: buy food at markets, slaughter livestock when the stores run low, lift the host's spirits with pay or drink, and sell your goods at a nearby town for a cut. Orders stand down for an enlisted player and for a co-op client. Default: on.")]
    public bool ProvenderEnabled { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderLowFoodDays}Low Food Days", TAOM.Features.Provender.ProvenderSettingsProvider.MinLowFoodDays, TAOM.Features.Provender.ProvenderSettingsProvider.MaxLowFoodDays, Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderLowFoodDays_hint}When the food in the stores, not counting the herd, lasts fewer days than this, the slaughter order turns livestock into meat, a head at a time, until it lasts this long again. Between 1 and 30. Default: 3.")]
    public int ProvenderLowFoodDays { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLowFoodDays;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderKeepPerType}Livestock Kept Per Type", TAOM.Features.Provender.ProvenderSettingsProvider.MinKeepPerType, TAOM.Features.Provender.ProvenderSettingsProvider.MaxKeepPerType, Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderKeepPerType_hint}The slaughter order always leaves this many head of each livestock type alive. The game itself still slaughters livestock when every other food is gone. Between 0 and 20. Default: 2.")]
    public int ProvenderKeepPerType { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultKeepPerType;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderSmallPayGold}Small Pay: Gold Per Soldier", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayGold, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayGold, Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderSmallPayGold_hint}Gold paid to each soldier (not you) by the small pay option, in a settlement or a ready field camp. Between 10 and 500. Default: 50.")]
    public int ProvenderSmallPayGold { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSmallPayGold;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderSmallPayMorale}Small Pay: Morale Gained", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayMorale, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayMorale, Order = 4, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderSmallPayMorale_hint}Morale the party gains from the small pay option. Between 1 and 30. Default: 5.")]
    public int ProvenderSmallPayMorale { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSmallPayMorale;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderLargePayGold}Large Pay: Gold Per Soldier", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayGold, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayGold, Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderLargePayGold_hint}Gold paid to each soldier (not you) by the large pay option. Never read below the small pay's gold. Between 10 and 500. Default: 100.")]
    public int ProvenderLargePayGold { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLargePayGold;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderLargePayMorale}Large Pay: Morale Gained", TAOM.Features.Provender.ProvenderSettingsProvider.MinPayMorale, TAOM.Features.Provender.ProvenderSettingsProvider.MaxPayMorale, Order = 6, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderLargePayMorale_hint}Morale the party gains from the large pay option. Never read below the small pay's morale. Between 1 and 30. Default: 10.")]
    public int ProvenderLargePayMorale { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultLargePayMorale;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderWageDaysKept}Wage Days Kept", TAOM.Features.Provender.ProvenderSettingsProvider.MinWageDaysKept, TAOM.Features.Provender.ProvenderSettingsProvider.MaxWageDaysKept, Order = 7, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderWageDaysKept_hint}The food order never spends the gold your party needs for this many days of wages, so buying food cannot leave your troops unpaid. 0 lets it spend the whole purse. Morale purchases are not limited: they show their price. Between 0 and 30. Default: 3.")]
    public int ProvenderWageDaysKept { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultWageDaysKept;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderSaleMaxFeePercent}Remote Sale: Largest Cut (%)", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleFeePercent, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleFeePercent, Order = 8, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderSaleMaxFeePercent_hint}The Provender-master's cut, in percent of a town's price, when you send goods to be sold there and the Provender-master has no Trade skill. It falls to the smallest cut at Trade 250, and each town further down the list adds 5. Between 0 and 90. Default: 30.")]
    public int ProvenderSaleMaxFeePercent { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleMaxFeePercent;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderSaleMinFeePercent}Remote Sale: Smallest Cut (%)", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleFeePercent, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleFeePercent, Order = 9, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderSaleMinFeePercent_hint}The Provender-master's cut at Trade 250. Never read above the largest cut. Between 0 and 90. Default: 10.")]
    public int ProvenderSaleMinFeePercent { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleMinFeePercent;

    [SettingPropertyGroup("{=taom_mcm_group_provender_master}Provender-master")]
    [SettingPropertyInteger("{=taom_mcm_ProvenderSaleTownCount}Remote Sale: Towns Offered", TAOM.Features.Provender.ProvenderSettingsProvider.MinSaleTownCount, TAOM.Features.Provender.ProvenderSettingsProvider.MaxSaleTownCount, Order = 10, RequireRestart = false,
        HintText = "{=taom_mcm_ProvenderSaleTownCount_hint}How many of the nearest towns that will buy from you the Provender-master offers. A town at war with you, of the opposing alignment, besieged, barring you or short of gold is never offered. Between 1 and 5. Default: 3.")]
    public int ProvenderSaleTownCount { get; set; } = TAOM.Features.Provender.ProvenderSettingsProvider.DefaultSaleTownCount;

    // --- Bond of Hire (docs/features/bond-of-hire.md, #867) ---
    //
    // Read when a clan signs on as a mercenary (the engine's service-started event), through BondOfHireSettingsProvider's one
    // cached settings reference, so RequireRestart = false. GroupOrder 72 is unused; it is the group's first property, so it
    // carries the order (MCM ignores a GroupOrder on any later one). The default and the range live in BondOfHireSettingsProvider
    // so the fallback before MCM is up and the hint cannot drift.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_bond_of_hire}Bond of Hire", GroupOrder = 72)]
    [SettingPropertyInteger("{=taom_mcm_BondOfHireMinimumDays}Minimum Mercenary Contract (Days)", TAOM.Features.BondOfHire.BondOfHireSettingsProvider.MinDays, TAOM.Features.BondOfHire.BondOfHireSettingsProvider.MaxDays, Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_BondOfHireMinimumDays_hint}The fewest days a clan that signs on as a mercenary stays with its kingdom. Until then it cannot leave a ruler who is in debt, be hired away by another kingdom, or leave in its daily consideration of whether to stay. A clan with more time left is not shortened. Your own clan is not bound, and neither is dismissing or hiring away a mercenary yourself. 0 turns it off. Applies when a clan signs on; clans already serving get it at their next signing. Between 0 and 120. Default: 30.")]
    public int BondOfHireMinimumDays { get; set; } = TAOM.Features.BondOfHire.BondOfHireSettingsProvider.DefaultMinimumDays;

    // --- Seasoned Hosts (docs/features/seasoned-hosts.md, #890) ---
    //
    // The skill XP multiplier is read at every skill XP grant and the training bonus for every troop of every AI lord party
    // each day, through SeasonedHostsSettingsProvider's one cached settings reference, so none needs a restart. Four new
    // settings, so their defaults reach every install. GroupOrder 73 is used by no other group.

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_seasoned_hosts}Seasoned Hosts", GroupOrder = 73)]
    [SettingPropertyFloatingInteger("{=taom_seasoned_hosts_training_bonus}AI Lord Party Training Bonus", TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MinTrainingBonus, TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MaxTrainingBonus, "0.00", Order = 0, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_training_bonus_hint}Extra daily and battle training XP for the troops of AI lord parties, as a fraction: 0.25 is 25 percent more. Applies to a lord party that is not yours, not in your clan and not in an army you lead; caravans and garrisons are left out. In a co-op session only the host's value applies; while a co-op mod TAOM cannot probe (such as BannerlordTogether) is enabled, it is off for every player. 0 is vanilla. Between 0 and 1. Default: 0.25.")]
    public float SeasonedHostsTrainingBonus { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultTrainingBonus;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_seasoned_hosts}Seasoned Hosts")]
    [SettingPropertyFloatingInteger("{=taom_seasoned_hosts_skill_xp}AI Lord Skill XP Multiplier", TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MinSkillXpMultiplier, TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.MaxSkillXpMultiplier, "0.00", Order = 1, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_skill_xp_hint}Multiplies the skill XP that AI lords earn, so their skills climb faster. Skills only: a lord's hero level is not sped up. Never applies to you or to any lord of your clan, spouse and family included. In a co-op session only the host's value applies; while a co-op mod TAOM cannot probe (such as BannerlordTogether) is enabled, it is off for every player. 1 is vanilla. Between 1 and 2. Default: 1.25.")]
    public float SeasonedHostsSkillXpMultiplier { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultSkillXpMultiplier;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_seasoned_hosts}Seasoned Hosts")]
    [SettingPropertyBool("{=taom_seasoned_hosts_learning_floor}Learning Never Stops", Order = 2, RequireRestart = false,
        HintText = "{=taom_seasoned_hosts_learning_floor_hint}Past a skill's learning limit the game lowers the learning rate until it reaches zero and the skill stops growing. When on, the rate never falls below 0.05, so a skill keeps growing very slowly. This applies to every hero, you included. When off, the game's own rule applies. Default: on.")]
    public bool SeasonedHostsLearningNeverStops { get; set; } = TAOM.Features.SeasonedHosts.SeasonedHostsSettingsProvider.DefaultLearningNeverStops;

    [SettingPropertyGroup("{=taom_mcm_group_world}World/{=taom_mcm_group_seasoned_hosts}Seasoned Hosts")]
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

    [SettingPropertyGroup("{=taom_mcm_group_smithing}Smithing", GroupOrder = 74)]
    [SettingPropertyBool("{=taom_sm_mcm_cap}Cap Smelting Yield", Order = 0, RequireRestart = false,
        HintText = "{=taom_sm_mcm_cap_hint}Smelting a weapon returns at most the number set below of each iron grade, of iron ore and of hardwood, and no hardwood at all when any metal comes out. Charcoal use is unchanged. This curbs the loop of selling looted weapons as iron. Default: on.")]
    public bool SmithsMeasureCapSmelting { get; set; } = TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.DefaultCapSmelting;

    [SettingPropertyGroup("{=taom_mcm_group_smithing}Smithing")]
    [SettingPropertyInteger("{=taom_sm_mcm_cap_value}Smelting Yield Cap", TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.MinYieldCap, TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.MaxYieldCap, Order = 1, RequireRestart = false,
        HintText = "{=taom_sm_mcm_cap_value_hint}The most of each iron grade, of iron ore and of hardwood that one smelt returns while the cap is on. Between 1 and 10; a value outside that range counts as 1. Default: 1.")]
    public int SmithsMeasureYieldCap { get; set; } = TAOM.Features.SmithsMeasure.SmithsMeasureSettingsProvider.DefaultYieldCap;

    [SettingPropertyGroup("{=taom_mcm_group_smithing}Smithing")]
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

    [SettingPropertyGroup("{=taom_mcm_group_smithing}Smithing")]
    [SettingPropertyBool("{=taom_mh_mcm_bulk}Bulk Smelting and Refining", Order = 10, RequireRestart = false,
        HintText = "{=taom_mh_mcm_bulk_hint}In the smithy, hold Shift while you smelt or refine to do five at once, or Ctrl to do the whole stack (Ctrl wins when both are held). It stops when your stamina or the charcoal or materials run out, and each unit costs what a single one would. Applies at once, no restart. Default: on.")]
    public bool ManyHammersBulkEnabled { get; set; } = TAOM.Features.ManyHammers.ManyHammersSettingsProvider.DefaultBulkEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_smithing}Smithing")]
    [SettingPropertyBool("{=taom_mh_mcm_hide_locked}Hide Locked Items from Smelting", Order = 11, RequireRestart = false,
        HintText = "{=taom_mh_mcm_hide_locked_hint}Weapons you locked in the inventory no longer appear in the smithy's smelting list, so they cannot be smelted by accident. Unlock one in the inventory to smelt it. A weapon you lock inside the smithy leaves the list at your next smelt or tab switch. No restart. Default: on.")]
    public bool ManyHammersHideLockedEnabled { get; set; } = TAOM.Features.ManyHammers.ManyHammersSettingsProvider.DefaultHideLockedEnabled;

    // --- Seat of Seeing (docs/features/seat-of-seeing.md, stage S1) ---

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing", GroupOrder = 82)]
    [SettingPropertyBool("{=taom_seat_mcm_enabled}Enable the Seat of Seeing", Order = 0, RequireRestart = false,
        HintText = "{=taom_seat_mcm_enabled_hint}Lets one key (Take the Seat, default Q, rebindable under Keybindings > Action) lift your view off the hero so you can fly over the field, and bring it back with a short glide. Your hero stands fast while you are up, or fights on if that setting is on. Off, the key does nothing, the mission time keys (Quicken, Slow and Stay time) stop too, and the next press of one cancels any change they made. Default: on.")]
    public bool SeatEnabled { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_begin}Begin Battles in the Seat", Order = 1, RequireRestart = false,
        HintText = "{=taom_seat_mcm_begin_hint}When a battle's deployment ends, your view goes into the Seat at the camera you were using, with no blend, instead of back to your hero. Press the key to return to your hero. Default: off.")]
    public bool BeginBattlesInSeat { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultBeginBattlesInSeat;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_cursor}Show a cursor with the order panel", Order = 2, RequireRestart = false,
        HintText = "{=taom_seat_mcm_cursor_hint}While you are in the Seat and the order panel is open, a cursor shows and orders land where it points. Hold the middle mouse button to look around with the cursor hidden. A click on one of the panel's buttons does not press it yet: it gives a Move order on the ground behind it. Default: on.")]
    public bool SeatCursorWithPanel { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultCursorWithPanel;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_keep_panel}Keep the order panel open", Order = 3, RequireRestart = false,
        HintText = "{=taom_seat_mcm_keep_panel_hint}While you are in the Seat, the order panel opens again after an order. Closing it yourself (Escape, the right mouse button, or the panel key) keeps it closed. With the game option that slows time (on by default) while orders are given, the whole stay in the Seat runs at quarter speed apart from one frame per order. Default: off.")]
    public bool SeatKeepPanelOpen { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultKeepPanelOpen;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_fight_on}Your hero fights on in the Seat", Order = 4, RequireRestart = false,
        HintText = "{=taom_seat_mcm_fight_on_hint}While you are in the Seat in a battle, your hero fights on with the company, driven by the game's AI, and you get the controls back when you leave. It acts the next time you take the Seat. Not in an online session. Default: off.")]
    public bool SeatHeroFightsOn { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultHeroFightsOn;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_take}Take a soldier after your hero falls", Order = 5, RequireRestart = false,
        HintText = "{=taom_seat_mcm_take_hint}After your hero falls in a battle, the key Take this soldier (Delete unless rebound, under Options > Keybindings > Action) in the Seat takes the soldier at the middle of the screen, or the nearest one of the company you keep to, who then carries your orders. Off, the key does nothing. Not in an online session. Default: on.")]
    public bool SeatTakeSoldierAfterFall { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultTakeSoldierAfterFall;

    [SettingPropertyGroup("{=taom_mcm_group_seat_of_seeing}Seat of Seeing")]
    [SettingPropertyBool("{=taom_seat_mcm_rise}Rise to give orders", Order = 6, RequireRestart = false,
        HintText = "{=taom_seat_mcm_rise_hint}With your view on your hero in third person and the order panel open, the view lifts (7 metres in a field battle, 3 in a siege or a sally out) so you can see over the ranks, and it sinks when the panel closes. The lift drops the target lock, and no new lock starts until the view has sunk. Under a roof the view rises through it. The Seat itself does not use this. It works with the Seat turned off too. Default: off.")]
    public bool SeatRiseWithPanel { get; set; } = TAOM.Features.SeatOfSeeing.SeatSettingsProvider.DefaultRiseWithPanel;

    // --- Marshal's Word (docs/features/marshals-word.md) ---

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word", GroupOrder = 83)]
    [SettingPropertyBool("{=taom_marshal_mcm_enabled}Enable Marshal's Word", Order = 0, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_enabled_hint}Richer orders for your companies in a battle. Once the battle has begun, hold Give the Order in Turn (default Left Shift, rebindable under Keybindings > Action) while you give an order with the order panel open, and the company carries it out after its current orders. Off, none of this acts from the next battle on, except Spacing Repair, which is decided when the game starts and needs this on at that start. Default: on.")]
    public bool MarshalEnabled { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultEnabled;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_keep_array}Keep the Array", Order = 1, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_keep_array_hint}Once the battle has begun, a click or a drag on the ground moves several selected companies as one body: they keep their places relative to each other, and a drag turns and stretches the whole group. Hold the keep-the-widths key (default Left Alt) to turn the group without stretching it. Off, the companies line up side by side as in the base game. Default: on.")]
    public bool MarshalKeepArray { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultKeepArray;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_arrive_together}Arrive Together", Order = 2, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_arrive_together_hint}Companies ordered to move together as one body walk at paces that bring them to their places at the same time: the nearer ones slow down. Needs Keep the Array. Default: on.")]
    public bool MarshalArriveTogether { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultArriveTogether;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_outlines}Outlines", Order = 3, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_outlines_hint}While the order panel is open, or while the show-indicators key (default Left Alt) is held, the soldiers of your selected companies wear a gold outline and an enemy company that one of them marks or charges wears a red one. Hold Mark, Do Not Move (default Left Ctrl) while you order a charge to mark an enemy company without moving: a mark steers archers and other ranged soldiers, and soldiers fighting hand to hand choose their own foe. This setting was renamed when its default changed to off, so a value you saved earlier was reset: turn it on again if you want it. Default: off.")]
    // Renamed from MarshalOutlines: MCM keeps a saved value per property name, so a flipped default under the old name would reach fresh installs only.
    public bool MarshalShowOutlines { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultOutlines;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_hollow_square}Hollow Square", Order = 4, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_hollow_square_hint}Once the battle has begun, a Square order on a company of yours that you command forms the plain square of the base game, which keeps the side you give it and grows only when its soldiers no longer fit, instead of the packed schiltron. A company of foot and ranged soldiers mixed keeps its mixed layout and ignores the side. Default: on.")]
    public bool MarshalHollowSquare { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultHollowSquare;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_spacing_repair}Spacing Repair", Order = 5, RequireRestart = true,
        HintText = "{=taom_marshal_mcm_spacing_repair_hint}A company of foot soldiers with riders under a tenth of its soldiers keeps the spacing limits of a foot company, as it already keeps their spacing, instead of the limits of cavalry. A change takes a restart of the game, and it applies only if Enable Marshal's Word is on when the game starts. It stays off in a co-op session and on a dedicated server, so that two machines never compute different widths. Default: on.")]
    public bool MarshalSpacingRepair { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultSpacingRepair;

    [SettingPropertyGroup("{=taom_mcm_group_marshal_s_word}Marshal's Word")]
    [SettingPropertyBool("{=taom_marshal_mcm_throwers_first}Throwers Throw First", Order = 6, RequireRestart = false,
        HintText = "{=taom_marshal_mcm_throwers_first_hint}On an Advance, a company of yours whose soldiers mostly carry javelins or other throwing weapons stops short of the enemy and throws, then goes in to melee when most have thrown, when the enemy comes close or after 40 seconds standing. A company holding the line, holding its fire, on a fire mode or in a shield wall, circle, square or column advances as before. Default: on.")]
    public bool MarshalThrowersFirst { get; set; } = TAOM.Features.MarshalsWord.MarshalSettingsProvider.DefaultThrowersFirst;

    // --- Battle Tactics / Dwarf Attacks (#1087) ---

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_dwarf_attacks}Dwarf Attacks", GroupOrder = 84)]
    [SettingPropertyBool("{=taom_dwarf_attacks_mcm_enabled}Enable Dwarf Attacks", Order = 0, RequireRestart = false,
        HintText = "{=taom_dwarf_attacks_mcm_enabled_hint}AI dwarves on foot head-butt an enemy right in front of them and drop-kick one a few steps away, each on its own cooldown. The head butt staggers; the drop kick knocks the enemy down unless a shield blocks it. Your own character never does either. Off, no new attack starts. Default: on.")]
    public bool DwarfAttacksEnabled { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_battle_tactics}Battle Tactics/{=taom_mcm_group_dwarf_attacks}Dwarf Attacks")]
    [SettingPropertyInteger("{=taom_dwarf_attacks_mcm_damage}Dwarf Attack Damage (%)", 0, TAOM.Features.DwarfAttacks.DwarfAttacksConfig.MaxDamagePercent, Order = 1, RequireRestart = false,
        HintText = "{=taom_dwarf_attacks_mcm_damage_hint}The damage of the head butt and the drop kick, as a percentage of their normal damage. Armour does not reduce it. At 0 the attacks still play but hurt no one and knock no one down. Default: 100.")]
    public int DwarfAttacksDamagePercent { get; set; } = TAOM.Features.DwarfAttacks.DwarfAttacksConfig.DefaultDamagePercent;

    // --- Special Resources (#1108) ---

    [SettingPropertyGroup("{=taom_mcm_group_special_resources}Special Resources")]
    [SettingPropertyBool("{=taom_specres_mcm_gold_only}Elite Troops Cost Gold Only", Order = 0, RequireRestart = false,
        HintText = "{=taom_specres_mcm_gold_only_hint}When on, your troops cost no special resource to upgrade, recruit or keep, and none of them desert for lack of it. Gold and wages still apply. The Elite Emissary sells for gold, to a buyer of its own resource group only. You still earn the resource, and the armoury still takes it. When you turn this off, the charges return, and elite troops desert again at a zero balance. Default: off.")]
    public bool EliteTroopsCostGoldOnly { get; set; } = false;

    [SettingPropertyGroup("{=taom_mcm_group_special_resources}Special Resources")]
    [SettingPropertyInteger("{=taom_specres_mcm_gold_rate}Emissary Gold Per Resource", SpecialResources.SpecialResourceSettingsProvider.MinGoldPerResource, SpecialResources.SpecialResourceSettingsProvider.MaxGoldPerResource, Order = 1, RequireRestart = false,
        HintText = "{=taom_specres_mcm_gold_rate_hint_base}With Elite Troops Cost Gold Only on, the emissary's gold price is the troop's base emissary cost times this number. The resource price is a multiple of the same base cost. Default: 200.")]
    public int EmissaryGoldPerResource { get; set; } = SpecialResources.SpecialResourceSettingsProvider.DefaultGoldPerResource;

    // --- Accessibility (#1110) ---

    [SettingPropertyGroup("{=taom_mcm_group_accessibility}Accessibility", GroupOrder = 85)]
    [SettingPropertyBool("{=taom_arachnophobia_mcm_mode}Arachnophobia Mode", Order = 0, RequireRestart = false,
        HintText = "{=taom_arachnophobia_mcm_mode_hint}Takes the giant spiders out of the game. Spider riders ride wargs instead. New volunteers, upgrades and the Elite Emissary no longer lead to a spider troop, and spider troops leave the encyclopedia and the Custom Battle troop list. Spider broods leave the campaign map, most within a day. The rest applies the next time you start or load a game. Spider troops that already exist stay, on wargs: in parties, garrisons, prisons and volunteer slots. Fords of War still offers its Spider Riders, on wargs. Lore text, and cobwebs in some scenes, stay. Default: off.")]
    public bool ArachnophobiaMode { get; set; } = false;

    // Persists by index: never reorder or remove a choice (BattleHudColourPaletteTests pins them). The battle
    // HUD reads it again while a battle runs, so an applied change shows in the current battle.
    [SettingPropertyGroup("{=taom_mcm_group_accessibility}Accessibility")]
    [SettingPropertyDropdown("{=taom_battlehud_mcm_colour_mode}Battle HUD Colours", Order = 1, RequireRestart = false,
        HintText = "{=taom_battlehud_mcm_colour_mode_hint}Changes only three rings of the round battle HUD: your player health, mount health and shield. Default keeps their stone look and colours. Each other choice draws those rings in flat colours that are easier to tell apart for that type of colour vision, on a dark track. The rings keep their order and fill as before, so their place and length also tell them apart. The career ability centre, the ammo count and all other screens do not change. A change applies in the current battle, with no restart.")]
    public Dropdown<string> BattleHudColourMode { get; set; } = new Dropdown<string>(
        TAOM.Features.BattleHud.UI.BattleHudColourPalette.Choices, 0);

    // --- Co-op (docs/features/coop-compat.md) ---

    // Read by TAOM.CoopCompat in the co-op server process only, through CoopSettingsProvider; a player's own value is never
    // read. On a dedicated server TaomSettings.Instance comes from taom-server-settings.json, loaded once per process
    // (ServerSettingsSource). Instrumentation in CoopSettingsRelevance: it gates log lines and one notice to the joining player.
    [SettingPropertyGroup("{=taom_mcm_group_coop}Co-op", GroupOrder = 8)]
    [SettingPropertyBool("{=taom_mcm_CoopLoadOrderWarning}Warn players whose load order differs from the host's", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_CoopLoadOrderWarning_hint}When a player joins a co-op campaign, the co-op server compares the order of TAOM and of every mod that adds items, troops or factions with its own, and tells a player whose order differs which two mods are the other way round. Nothing is blocked. The co-op server process reads this switch each time a player joins; a player's own value is not read. A dedicated server takes it from taom-server-settings.json (CoopLoadOrderWarning), which it reads once at start, so change it there and restart the server. Default: on.")]
    public bool CoopLoadOrderWarning { get; set; } = TAOM.Features.CoopInterop.CoopSettingsProvider.DefaultLoadOrderWarning;

    // Read by CareerSyncHost (Main/Features/CoopInterop/CareerSync) in the co-op server process, through
    // CoopSettingsProvider, for each report and each career change the server makes. In the fingerprint: it changes what
    // the server stores and sends.
    [SettingPropertyGroup("{=taom_mcm_group_coop}Co-op", GroupOrder = 8)]
    [SettingPropertyBool("{=taom_mcm_CoopCareerSync}Sync careers for co-op players", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_CoopCareerSync_hint}When on, a career a co-op player picks or changes goes to the co-op server, which checks it with the career rules, keeps it in its save and shows it to the other players, so the career is still there after the player joins again. The co-op server process reads this switch each time a player reports a career; a player's own value is not read. A dedicated server takes it from taom-server-settings.json (CoopCareerSync), which it reads once at start, so change it there and restart the server. Off: each player's career stays only on that player's PC and is lost when the player joins again. Default: on.")]
    public bool CoopCareerSync { get; set; } = TAOM.Features.CoopInterop.CoopSettingsProvider.DefaultCareerSync;

    // Read by the co-op server process through IArmourAcquisitionSettingsProvider (the guest armoury in
    // Main/Features/CoopInterop/GuestTrade). In the fingerprint: it changes what the server does to a player's party.
    [SettingPropertyGroup("{=taom_mcm_group_coop}Co-op", GroupOrder = 8)]
    [SettingPropertyBool("{=taom_mcm_CoopGuestLadder}Lords' Ladder for co-op players", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_CoopGuestLadder_hint}When on, the co-op server runs the lord's gear ladder for each co-op player's hero: a player's armoury lists the rung the server keeps for that hero, takes the lord's materials for it and fits the claimed piece, and a battle a co-op player wins can yield lord's materials, given to the party once it is free and out of battle. The rung quests are not offered to co-op players. The co-op server process reads this switch each time a player uses an armoury or a battle ends; a player's own value is not read. A dedicated server takes it from taom-server-settings.json (CoopGuestLadder), which it reads once at start, so change it there and restart the server. Off: co-op players see no ladder at the armoury and find no new lord's materials after battles; materials already found are still given. Default: on.")]
    public bool CoopGuestLadder { get; set; } = TAOM.Features.ArmourAcquisition.ArmourAcquisitionSettingsProvider.DefaultCoopGuestLadder;

    // --- Cheat Menu (docs/features/cheat-menu.md) ---

    [SettingPropertyGroup("{=taom_cheat_mcm_group}Cheat Menu", GroupOrder = 90)]
    [SettingPropertyBool("{=taom_cheat_mcm_enable}Enable Cheat Menu", Order = 0, RequireRestart = false,
        HintText = "{=taom_cheat_mcm_enable_hint}Adds a Cheats button to the bottom bar of the campaign map. It opens a menu that teleports your party to a settlement and gives gold, troops, your special resource, influence, renown and relations, and heals your party. In a mission, the Esc menu has a Cheats item (or press H) that heals you or your side, routs or kills the enemy, or adds 10 allied or enemy troops. Cheats change your save, and most cannot be undone. The button appears the next time the campaign map opens, for example after loading a save. The H key on the campaign map also opens the menu (change either key under Options, Keybindings). Single-player only. Default: off.")]
    public bool EnableCheatMenu { get; set; } = false;
}
