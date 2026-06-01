# Codex Adversarial Review - Faction-Map CC Page Rewrite Phase 2

Date: 2026-06-01

Scope: `Main/_Module/ModuleData/factionmap/factions.json`, `taom_module_strings.xml`, faction-map localization/display path, `TaomCulturalFeats.cs`, XSLT-wrapped vanilla cultural feats, and troop-name cross-checks.

## Vanilla Code

`TaleWorlds.Localization.TextObject` resolves `{=KEY}default` through `MBTextManager.GetLocalizedText(Value)` before grammar/language processing:

```csharp
internal List<MBTextToken> GetCachedTokens()
{
	if (Value != null)
	{
		if (cachedTokens == null || cachedTextLanguageId != MBTextManager.GetActiveTextLanguageIndex())
		{
			string localizedText = MBTextManager.GetLocalizedText(Value);
			cachedTokens = MBTextManager.Tokenizer.Tokenize(localizedText);
			cachedTextLanguageId = MBTextManager.GetActiveTextLanguageIndex();
		}
		return cachedTokens;
	}
	return null;
}

public override string ToString()
{
	string result;
	try
	{
		result = MBTextManager.ProcessTextToString(this, shouldClear: true);
	}
	catch (Exception ex)
	{
		result = "Error at id: " + GetID() + ". Lang: " + MBTextManager.ActiveTextLanguage;
		Debug.Print(ex.Message);
	}
	return result;
}
```

`TaleWorlds.Localization.MBTextManager.GetLocalizedText` is the installed v1.4.5 localization body backing `TextObject.ToString()`:

```csharp
internal static string GetLocalizedText(string text)
{
	if (text != null && text.Length > 2 && text[0] == '{' && text[1] == '=')
	{
		if (_idStringBuilder == null)
		{
			_idStringBuilder = new StringBuilder(8);
		}
		else
		{
			_idStringBuilder.Clear();
		}
		if (_targetStringBuilder == null)
		{
			_targetStringBuilder = new StringBuilder(100);
		}
		else
		{
			_targetStringBuilder.Clear();
		}
		for (int i = 2; i < text.Length; i++)
		{
			if (text[i] != '}')
			{
				_idStringBuilder.Append(text[i]);
				continue;
			}
			for (i++; i < text.Length; i++)
			{
				_targetStringBuilder.Append(text[i]);
			}
			string text2 = "";
			if (_activeTextLanguageId == "English")
			{
				text2 = _targetStringBuilder.ToString();
				return RemoveComments(text2);
			}
			if ((_idStringBuilder.Length == 1 && _idStringBuilder[0] == '*') || (_idStringBuilder.Length == 1 && _idStringBuilder[0] == '!'))
			{
				break;
			}
			if (_activeTextLanguageId != "English")
			{
				text2 = LocalizedTextManager.GetTranslatedText(_activeTextLanguageId, _idStringBuilder.ToString());
			}
			if (text2 == null)
			{
				break;
			}
			return RemoveComments(text2);
		}
		return _targetStringBuilder.ToString();
	}
	return text;
}
```

The requested `TaleWorlds.Localization.GameTextManager` FQN does not exist in the installed v1.4.5 DLLs. The installed type is `TaleWorlds.Core.GameTextManager`; its lookup returns a copied `TextObject`, adds the id to the value on success, and returns an `{=!}ERROR...` `TextObject` on miss:

```csharp
public bool TryGetText(string id, string variation, out TextObject text)
{
	text = null;
	_gameTexts.TryGetValue(id, out var value);
	if (value != null)
	{
		if (variation == null)
		{
			text = value.DefaultText;
		}
		else
		{
			text = value.GetVariation(variation);
		}
		if (text != null)
		{
			text = text.CopyTextObject();
			text.AddIDToValue(id);
			return true;
		}
	}
	return false;
}

public TextObject FindText(string id, string variation = null)
{
	if (TryGetText(id, variation, out var text))
	{
		return text;
	}
	if (variation == null)
	{
		return new TextObject("{=!}ERROR: Text with id " + id + " doesn't exist!");
	}
	return new TextObject("{=!}ERROR: Text with id " + id + " doesn't exist! Variation: " + variation);
}
```

Installed v1.4.5 `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultCulturalFeats`:

```csharp
using TaleWorlds.Core;

namespace TaleWorlds.CampaignSystem.CharacterDevelopment;

public class DefaultCulturalFeats
{
	private FeatObject _aseraiTraderFeat;
	private FeatObject _aseraiDesertSpeedFeat;
	private FeatObject _aseraiWageFeat;
	private FeatObject _battaniaForestSpeedFeat;
	private FeatObject _battaniaMilitiaFeat;
	private FeatObject _battaniaConstructionFeat;
	private FeatObject _empireGarrisonWageFeat;
	private FeatObject _empireArmyInfluenceFeat;
	private FeatObject _empireVillageHearthFeat;
	private FeatObject _khuzaitCheaperRecruitsFeat;
	private FeatObject _khuzaitAnimalProductionFeat;
	private FeatObject _khuzaitDecreasedTaxFeat;
	private FeatObject _sturgianGrainProductionFeat;
	private FeatObject _sturgianArmyInfluenceCostFeat;
	private FeatObject _sturgianDecisionPenaltyFeat;
	private FeatObject _vlandianRenownIncomeFeat;
	private FeatObject _vlandianVillageProductionFeat;
	private FeatObject _vlandianArmyInfluenceCostFeat;

	private static DefaultCulturalFeats Instance => Campaign.Current.DefaultFeats;

	public static FeatObject AseraiTraderFeat => Instance._aseraiTraderFeat;
	public static FeatObject AseraiDesertFeat => Instance._aseraiDesertSpeedFeat;
	public static FeatObject AseraiIncreasedWageFeat => Instance._aseraiWageFeat;
	public static FeatObject BattanianForestSpeedFeat => Instance._battaniaForestSpeedFeat;
	public static FeatObject BattanianMilitiaFeat => Instance._battaniaMilitiaFeat;
	public static FeatObject BattanianConstructionFeat => Instance._battaniaConstructionFeat;
	public static FeatObject EmpireGarrisonWageFeat => Instance._empireGarrisonWageFeat;
	public static FeatObject EmpireArmyInfluenceFeat => Instance._empireArmyInfluenceFeat;
	public static FeatObject EmpireVillageHearthFeat => Instance._empireVillageHearthFeat;
	public static FeatObject KhuzaitRecruitUpgradeFeat => Instance._khuzaitCheaperRecruitsFeat;
	public static FeatObject KhuzaitAnimalProductionFeat => Instance._khuzaitAnimalProductionFeat;
	public static FeatObject KhuzaitDecreasedTaxFeat => Instance._khuzaitDecreasedTaxFeat;
	public static FeatObject SturgianGrainProductionFeat => Instance._sturgianGrainProductionFeat;
	public static FeatObject SturgianArmyInfluenceCostFeat => Instance._sturgianArmyInfluenceCostFeat;
	public static FeatObject SturgianDecisionPenaltyFeat => Instance._sturgianDecisionPenaltyFeat;
	public static FeatObject VlandianRenownMercenaryFeat => Instance._vlandianRenownIncomeFeat;
	public static FeatObject VlandianCastleVillageProductionFeat => Instance._vlandianVillageProductionFeat;
	public static FeatObject VlandianArmyInfluenceFeat => Instance._vlandianArmyInfluenceCostFeat;

	public DefaultCulturalFeats()
	{
		RegisterAll();
	}

	private void RegisterAll()
	{
		_aseraiTraderFeat = Create("aserai_cheaper_caravans");
		_aseraiDesertSpeedFeat = Create("aserai_desert_speed");
		_aseraiWageFeat = Create("aserai_increased_wages");
		_battaniaForestSpeedFeat = Create("battanian_forest_speed");
		_battaniaMilitiaFeat = Create("battanian_militia_production");
		_battaniaConstructionFeat = Create("battanian_slower_construction");
		_empireGarrisonWageFeat = Create("empire_decreased_garrison_wage");
		_empireArmyInfluenceFeat = Create("empire_army_influence");
		_empireVillageHearthFeat = Create("empire_slower_hearth_production");
		_khuzaitCheaperRecruitsFeat = Create("khuzait_cheaper_recruits_mounted");
		_khuzaitAnimalProductionFeat = Create("khuzait_increased_animal_production");
		_khuzaitDecreasedTaxFeat = Create("khuzait_decreased_town_tax");
		_sturgianGrainProductionFeat = Create("sturgian_increased_grain_production");
		_sturgianArmyInfluenceCostFeat = Create("sturgian_decreased_army_influence_cost");
		_sturgianDecisionPenaltyFeat = Create("sturgian_increased_decision_penalty");
		_vlandianRenownIncomeFeat = Create("vlandian_renown_mercenary_income");
		_vlandianVillageProductionFeat = Create("vlandian_villages_production_bonus");
		_vlandianArmyInfluenceCostFeat = Create("vlandian_increased_army_influence_cost");
		InitializeAll();
	}

	private FeatObject Create(string stringId)
	{
		return Game.Current.ObjectManager.RegisterPresumedObject(new FeatObject(stringId));
	}

	private void InitializeAll()
	{
		_aseraiTraderFeat.Initialize("{=!}aserai_cheaper_caravans", "{=7kGGgkro}Caravans are 30% cheaper to build. 10% less trade penalty.", 0.7f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_aseraiDesertSpeedFeat.Initialize("{=!}aserai_desert_speed", "{=6aFTN1Nb}No speed penalty on desert.", 1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_aseraiWageFeat.Initialize("{=!}aserai_increased_wages", "{=GacrZ1Jl}Daily wages of troops in the party are increased by 5%.", 0.05f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
		_battaniaForestSpeedFeat.Initialize("{=!}battanian_forest_speed", "{=38W2WloI}50% less speed penalty and 15% sight range bonus in forests.", 0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_battaniaMilitiaFeat.Initialize("{=!}battanian_militia_production", "{=HLI5zAMV}Towns owned by Battanian rulers will have +20% chance of militias to spawn as veteran militias.", 0.2f, isPositiveEffect: true, FeatObject.AdditionType.Add);
		_battaniaConstructionFeat.Initialize("{=!}battanian_slower_construction", "{=ruP9jbSq}10% slower build rate for town projects in settlements.", -0.1f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
		_empireGarrisonWageFeat.Initialize("{=!}empire_decreased_garrison_wage", "{=a2eM0QUb}20% less garrison troop wage.", -0.2f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_empireArmyInfluenceFeat.Initialize("{=!}empire_army_influence", "{=xgPNGOa8}Being in army brings 25% more influence.", 0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_empireVillageHearthFeat.Initialize("{=!}empire_slower_hearth_production", "{=UWiqIFUb}Village hearths increase 20% less.", -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
		_khuzaitCheaperRecruitsFeat.Initialize("{=!}khuzait_cheaper_recruits_mounted", "{=JUpZuals}Recruiting and upgrading mounted troops are 10% cheaper.", -0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_khuzaitAnimalProductionFeat.Initialize("{=!}khuzait_increased_animal_production", "{=Xaw2CoCG}25% production bonus to horse, mule, cow and sheep in villages owned by Khuzait rulers.", 0.25f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_khuzaitDecreasedTaxFeat.Initialize("{=!}khuzait_decreased_town_tax", "{=8PsaGhI8}20% less tax income from towns.", -0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
		_sturgianGrainProductionFeat.Initialize("{=!}sturgian_increased_grain_production", "{=5BabRyaa}Villages grain production is increased by 10%.", 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_sturgianArmyInfluenceCostFeat.Initialize("{=!}sturgian_decreased_army_influence_cost", "{=Lmjm5Q9D}Armies are gathered with 50% less influence.", -0.5f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_sturgianDecisionPenaltyFeat.Initialize("{=!}sturgian_increased_decision_penalty", "{=fB7kS9Cx}20% more relationship penalty from kingdom decisions.", 0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
		_vlandianRenownIncomeFeat.Initialize("{=!}vlandian_renown_mercenary_income", "{=ppdrgOL8}5% more renown from the battles, 15% more income while serving as a mercenary.", 0.05f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_vlandianVillageProductionFeat.Initialize("{=!}vlandian_villages_production_bonus", "{=3GsZXXOi}10% production bonus to villages that are bound to castles.", 0.1f, isPositiveEffect: true, FeatObject.AdditionType.AddFactor);
		_vlandianArmyInfluenceCostFeat.Initialize("{=!}vlandian_increased_army_influence_cost", "{=O1XCNeZr}Recruiting lords to armies costs 20% more influence.", 0.2f, isPositiveEffect: false, FeatObject.AdditionType.AddFactor);
	}
}
```

## Cultural-Feat Audit

| faction | shipped feat | mentioned in CC page? | notes |
|---|---|---|---|
| Stewardship of Gondor | Tower Guard: -20% garrison wage | yes | bonus 2 |
| Stewardship of Gondor | Gondorian Discipline: +30% army influence award | yes | perk 1 / bonus 3 |
| Stewardship of Gondor | War-Depleted Lands: -15% village hearth growth | yes | bonus 6 / weakness 0 |
| Stewardship of Gondor | Standing Armies: +2.5% party size | yes | perk 2 / bonus 1 |
| Stewardship of Gondor | Tower Guard Discipline: +1 loyalty/day | yes | perk 0 / bonus 5 |
| Stewardship of Gondor | Gondorian Resolve: +5 morale | yes | perk 1 / bonus 4 |
| Stewardship of Gondor | Men of the Fields: +10% plains speed | yes | perk 2 / bonus 0 |
| Dominion of Mordor | The Dark Lord's Will: -60% army influence cost | yes | perk 0 / bonus 0 |
| Dominion of Mordor | Nurn Farmlands: +20% grain production | yes | bonus 2 |
| Dominion of Mordor | Dark Tribute: +20% party wages | yes | bonus 8 / weakness 0 |
| Dominion of Mordor | Sauron's Hordes: +10% party size | yes | bonus 1 |
| Dominion of Mordor | Sauron's Wrath: +25% raid damage | yes | perk 1 / bonus 3 |
| Dominion of Mordor | Shadow March: +5% plains speed | yes | bonus 5 |
| Dominion of Mordor | Dead Marshes: +5% swamp speed | yes | bonus 5 |
| Dominion of Mordor | Creatures of the Dark: +10% night speed | yes | perk 2 / bonus 4 |
| Dominion of Mordor | Sauron's Levy: +20% volunteer respawn | yes | bonus 6 |
| Dominion of Mordor | Black Speech Heralds: +2 Gang Leader town notables | yes | bonus 7 |
| Dominion of Mordor | Slave Drivers: +5% village notable count | no | see MEDIUM finding |
| Dominion of Isengard | War Machine: -15% mounted recruit/upgrade cost | yes | bonus 3 |
| Dominion of Isengard | Orthanc Garrison: -20% garrison wage | yes | bonus 4 |
| Dominion of Isengard | Saruman's Grip: +25% decision relationship penalty | yes | bonus 8 / weakness 0 |
| Dominion of Isengard | Uruk-hai Legions: +20% party size | yes | perk 0 / bonus 0 |
| Dominion of Isengard | Industrial Might: +15% construction | yes | perk 1 / bonus 1 |
| Dominion of Isengard | Industrial Forges: -20% smithing energy | yes | perk 1 / bonus 2 |
| Dominion of Isengard | War Machine Raids: +20% raid damage | yes | bonus 5 |
| Dominion of Isengard | Forced March: +10% plains speed | yes | perk 0 / bonus 6 |
| Dominion of Isengard | Fenland Drillmasters: +10% swamp speed | yes | perk 0 / bonus 6 |
| Dominion of Isengard | Orthanc Quartermasters: +2 Merchant town notables | yes | perk 2 / bonus 7 |
| Dominion of Isengard | Industrial Forges town notable: +1 Artisan | yes | bonus 7 |
| Dominion of Isengard | Uruk-hai Captains: +12 Gang Leader town notables | yes | perk 2 / bonus 7 |
| Dominion of Isengard | Iron Press: +10% village notable count | no | see MEDIUM finding |
| Overlordship of Dol Guldur | Shadow Command: -50% army influence cost | yes | perk 0 / bonus 0 |
| Overlordship of Dol Guldur | Dark Conscription: +20% veteran militia chance | yes | bonus 3 |
| Overlordship of Dol Guldur | Ruinous Works: -20% construction | yes | bonus 6 / weakness 1 |
| Overlordship of Dol Guldur | Dark Legions: +20% party size | yes | perk 1 / bonus 1 |
| Overlordship of Dol Guldur | Voracious Hordes: +10% food consumption | yes | bonus 5 / weakness 0 |
| Overlordship of Dol Guldur | Dark Conscripts: +20% volunteer respawn | yes | perk 1 / bonus 2 |
| Overlordship of Dol Guldur | Shadow Brokers: +1 Merchant town notable | yes | bonus 4 |
| Overlordship of Dol Guldur | Dark Smithies: +1 Artisan town notable | yes | bonus 4 |
| Overlordship of Dol Guldur | Shadow Captains: +13 Gang Leader town notables | yes | perk 2 / bonus 4 |
| Overlordship of Dol Guldur | Hidden Hovels: +10% village notable count | no | see MEDIUM finding |
| Overlordship of Gundabad | Orc Horde: -40% army influence cost | yes | perk 0 / bonus 0 |
| Overlordship of Gundabad | Plundered Stores: +15% grain production | yes | bonus 5 |
| Overlordship of Gundabad | Plunder Demands: +10% party wages | yes | bonus 7 / weakness 0 |
| Overlordship of Gundabad | Mountain Swarm: +20% party size | yes | perk 1 / bonus 1 |
| Overlordship of Gundabad | Orc Pillagers: +25% raid damage | yes | bonus 2 |
| Overlordship of Gundabad | Mountain Marauders: +10% snow speed | yes | perk 1 / bonus 3 |
| Overlordship of Gundabad | Mountain Levies: +20% volunteer respawn | yes | bonus 4 |
| Overlordship of Gundabad | Bone-Smiths: +1 Artisan town notable | yes | perk 2 / bonus 6 |
| Overlordship of Gundabad | Pale Warband Chieftains: +3 Gang Leader town notables | yes | perk 2 / bonus 6 |
| Overlordship of Gundabad | Bone Camps: +10% village notable count | no | see MEDIUM finding |
| Havens of Umbar | Corsair Trade: -25% caravan cost | yes | perk 0 / bonus 0 |
| Havens of Umbar | Corsair Glory: +8% battle renown | yes | perk 1 / bonus 2 |
| Havens of Umbar | Corsair Greed: +8% party wages | yes | bonus 4 / weakness 0 |
| Havens of Umbar | Corsair Trade Networks: +15% tariffs | yes | perk 0 / bonus 1 |
| Havens of Umbar | Desert Corsairs: +10% desert speed | yes | perk 2 / bonus 3 |
| Kingdom of Erebor | Dwarven Garrison: -25% garrison wage | yes | perk 1 / bonus 0 |
| Kingdom of Erebor | Dwarven Industry: +10% village production | yes | perk 2 / bonus 3 |
| Kingdom of Erebor | Dwarven Perfectionism: -15% construction | yes | bonus 6 / weakness 0 |
| Kingdom of Erebor | Dwarven Honor: +1 loyalty/day | yes | perk 1 / bonus 5 |
| Kingdom of Erebor | Dwarven Stubbornness: +5 morale | yes | perk 2 / bonus 4 |
| Kingdom of Erebor | Master Smiths: -30% smithing energy | partial | bonus/perk correct; description falsely says "halve forge costs" |
| Kingdom of Erebor | Mountain Folk: +10% snow speed | yes | bonus 2 |
| Kingdom of Imladris | Elven Wisdom: +35% army influence award | yes | perk 0 / bonus 0 |
| Kingdom of Imladris | Last Homely House: +20% hearth growth | yes | perk 1 / bonus 1 |
| Kingdom of Imladris | Elven Pride: +25% army influence cost | yes | bonus 5 / weakness 0 |
| Kingdom of Imladris | Elven Frugality: -15% food consumption | yes | perk 1 / bonus 3 |
| Kingdom of Imladris | Elven Wisdom loyalty: +0.5 loyalty/day | yes | bonus 4 |
| Kingdom of Imladris | Woodland Grace: +10% forest speed | yes | perk 2 / bonus 2 |
| Kingdom of Lasgalen | Woodland Realm: +10% forest speed | yes | perk 0 / bonus 0 |
| Kingdom of Lasgalen | Silvan Wardens: +25% veteran militia chance | yes | perk 1 / bonus 1 |
| Kingdom of Lasgalen | Forest Isolation: -20% hearth growth | yes | bonus 4 / weakness 0 |
| Kingdom of Lasgalen | Woodland Sustenance: -15% food consumption | yes | perk 0 / bonus 3 |
| Kingdom of Lasgalen | Woodland Bonds: +3 morale | yes | perk 2 / bonus 2 |
| Kingdom of Lothlorien | Golden Wood: +10% forest speed | yes | perk 0 / bonus 0 |
| Kingdom of Lothlorien | Wardens of Lorien: -20% garrison wage | yes | perk 0 / bonus 1 |
| Kingdom of Lothlorien | Timeless Craft: -10% construction | yes | bonus 5 / weakness 0 |
| Kingdom of Lothlorien | Lembas Bread: -15% food consumption | yes | perk 1 / bonus 2 |
| Kingdom of Lothlorien | Elven Grace: +0.5 loyalty/day | yes | perk 2 / bonus 4 |
| Kingdom of Lothlorien | Elven Harmony: +3 morale | yes | perk 2 / bonus 3 |
| Kingdom of Rohan | Horse-lord Heritage: -15% mounted recruit/upgrade | yes | perk 0 / bonuses 0-1 |
| Kingdom of Rohan | Riders of the Mark: -15% mounted wages | yes | perk 1 / bonus 2 |
| Kingdom of Rohan | Cavalry Dependent: -10% speed over 50% infantry | yes | bonus 6 / weakness 0 |
| Kingdom of Rohan | Horse-lord Fellowship: +0.5 loyalty/day | yes | perk 2 / bonus 5 |
| Kingdom of Rohan | Riders' Spirit: +5 morale | yes | perk 2 / bonus 4 |
| Kingdom of Rohan | Riders of the Plains: +10% plains speed | yes | perk 1 / bonus 3 |
| Clans of Dunland | Battanian Forest Speed: 50% less forest speed penalty +15% sight | partial/wrong | page says "+15% party speed in forest" |
| Clans of Dunland | Battanian Militia: +20% veteran militia chance | yes | inherited line present |
| Clans of Dunland | Battanian Construction: -10% construction | partial/wrong | page says "-15%" |
| Clans of Dunland | Hill Marchers: +10% plains speed | yes | perk 0 / bonus 1 |
| Clans of Dunland | Hill-Tribe Levy: +5% party size | yes | perk 0 / bonus 2 |
| Clans of Dunland | Hill-Tribe Recruitment: +10% volunteer respawn | yes | perk 1 / bonus 3 |
| Kingdom of Dale | Sturgian Grain Production: +10% village grain | no | page instead claims forest/winter inheritance |
| Kingdom of Dale | Sturgian Army Influence Cost: -50% army influence cost | no | omitted |
| Kingdom of Dale | Sturgian Decision Penalty: +20% relationship penalty | no | omitted |
| Kingdom of Dale | Vale Traders: +10% plains speed | yes | perk 0 / bonus 0 |
| Khudorom of Khand | Battanian Forest Speed: 50% less forest speed penalty +15% sight | partial/wrong | page says "+15% party speed in forest" |
| Khudorom of Khand | Battanian Militia: +20% veteran militia chance | yes | inherited line present |
| Khudorom of Khand | Battanian Construction: -10% construction | partial/wrong | page says "-15%" |
| Khudorom of Khand | Steppe Charioteers: +10% steppe speed | yes | perk 0 / bonus 0 |
| Taskralan of Harwan | Aserai Trader: 30% cheaper caravans +10% less trade penalty | partial | broad inherited line only; no concrete numbers |
| Taskralan of Harwan | Aserai Desert: no speed penalty on desert | partial | broad "hardiness" line only |
| Taskralan of Harwan | Aserai Increased Wages: +5% party wages | no | inherited negative omitted |
| Taskralan of Harwan | Sons of the Sun: +10% desert speed | yes | perk 0 / bonus 0 |
| Taskralan of Harwan | Haradrim Warbands: +5% party size | yes | perk 1 / bonus 1 |
| Golden-Realm of Rhun | Khuzait mounted recruit/upgrade: -10% mounted costs | partial | "cavalry economy" vague |
| Golden-Realm of Rhun | Khuzait animal production: +25% horse/mule/cow/sheep production | no | omitted |
| Golden-Realm of Rhun | Khuzait decreased town tax: -20% town tax | no | inherited negative omitted |
| Golden-Realm of Rhun | Easterling Outriders: +10% steppe speed | yes | perk 0 / bonus 0 |
| Golden-Realm of Rhun | Easterling Host: +5% party size | yes | perk 1 / bonus 1 |

## Key-Coverage Audit

Automated check: 599 unique `{=...}` tokens in `factions.json`; all 599 have matching `<string id="...">` entries in `taom_module_strings.xml`.

| key | result |
|---|---|
| `taom_faction_stewardship_of_gondor_name` | MATCHED |
| `taom_faction_stewardship_of_gondor_perk_2_desc` | MATCHED |
| `taom_faction_dominion_of_mordor_bonus_4` | MATCHED |
| `taom_faction_dominion_of_isengard_bonus_7` | MATCHED |
| `taom_faction_overlordship_of_dol_guldur_perk_2_name` | MATCHED |
| `taom_faction_kingdom_of_rohan_bonus_6` | MATCHED |
| `taom_faction_difficulty_5` | MATCHED in XML; not a JSON token because it is produced by `FormatDifficultyText` |
| `taom_faction_havens_of_umbar_special_unit_0_name` | MISSING / not a live JSON key; the live key is `taom_faction_havens_of_umbar_su_0_name` |
| `taom_faction_kingdom_of_imladris_weakness_2` | MATCHED |
| `taom_faction_clans_of_dunland_strength_3` | MATCHED |

## Known Suspect Verdicts

1. **Content accuracy vs. shipped cultural feats: CONFIRMED.** Most TAOM custom feats are represented, including the explicit Isengard, Dol Guldur, Mordor, and Rohan checks in the prompt. However, four village notable-count feats are not mentioned, Erebor's description overstates the smithing feat, and inherited vanilla-feat content for Dale/Dunland/Khand/Harad/Rhun contains false or partial claims.

2. **XSLT-wrapped culture feat inheritance: CONFIRMED.** Rohan is correct as TAOM-only because `spcultures.xslt:858-865` replaces vlandia feats. Dunland and Khand inherit Battanian feats, but the page misstates Battanian forest/construction effects. Dale inherits Sturgian grain/army/decision-penalty feats, but the page claims Sturgian forest/winter effects that do not exist. Harad and Rhun omit inherited negatives.

3. **Special-units accuracy: CONFIRMED.** Exact-name grep found only 7/48 UI special-unit names in the expected troop files. The other 41 are fabricated or renamed hints rather than actual troop display names. Per the prompt, this is cosmetic but player-facing and should be fixed to actual troop names or clearly relabeled as archetypes.

4. **JSON ↔ XML key alignment: DISPUTED for live keys.** Every live JSON token has an XML entry. One requested spot-check key, `taom_faction_havens_of_umbar_special_unit_0_name`, is not a live key because the data uses `_su_` abbreviations; that is a naming-convention issue, not a missing live XML entry.

5. **Key naming convention consistency: CONFIRMED.** No plural/singular slippage was found for bonuses/perks/strength/weakness. The special-unit section is the exception: all special-unit strings use `_su_...` instead of a section-derived `_special_unit_...` or `_special_units_...` form.

6. **String token escaping safety: DISPUTED.** `factions.json` and `taom_module_strings.xml` decode as UTF-8, contain 37 U+2212 minus signs each, and `taom_module_strings.xml` parses as XML. `Select-String '[&<>]' factions.json` returned no matches. The harvester escapes `&`, `<`, `>`, and `"`.

7. **Strength/weakness double-prefix safety: DISPUTED.** No `strengths[]` or `weaknesses[]` default text starts with `+ ` or `- `, so `FactionDisplayHelper.ApplyResult` will not render `+ +` or `- -`.

8. **Old hard-coded content fully removed: DISPUTED.** Exact searches for `Dunedain Blood`, `Lords gain experience 10% faster`, `Defense Boost`, `Elite Units in Specific Regions`, and standalone `Varies` found no hits. Generic `Dunedain` remains in valid Gondor/Arnor lore text.

9. **Pre-existing helper coverage of new content: CONFIRMED.** The selected-faction panel path goes through `FactionDisplayHelper.ApplyResult` and localizes faction content. The hover tooltip path bypasses it: `PolygonWidget` stores `faction.Name` raw, `FactionHoverService` returns it raw, and `FactionDisplayHelper.ShowHoverTooltip` passes it to `TooltipProperty` without `Localize`.

## Findings

### HIGH

[HIGH] Main/_Module/ModuleData/factionmap/factions.json:500 — Content accuracy — Dale's page claims inherited Sturgian forest mobility and winter resilience, but decompiled v1.4.5 `DefaultCulturalFeats` gives Sturgia +10% grain production, -50% army influence cost, and +20% relationship penalty from decisions. The CC page omits all three real inherited feats and promises two nonexistent ones. Fix by replacing Dale's inherited perk/bonus text with the actual Sturgian grain/army/decision-penalty effects.

[HIGH] Main/_Module/ModuleData/factionmap/factions.json:326 — Content accuracy — The XSLT-wrapped inherited-culture pages misstate or omit vanilla inherited effects: Dunland/Khand say Battanian "+15% party speed in forest" and "-15% construction" instead of "50% less forest speed penalty +15% sight" and "-10% construction"; Rhun says "Khuzait cavalry economy and militia traditions" but vanilla Khuzait has mounted-cost, animal-production, and -20% town-tax feats; Harad omits Aserai's +5% wage penalty and uses broad "hardiness" wording. Fix these inherited sections from the decompiled vanilla feat text, with negative inherited feats shown as negative bonuses/weaknesses.

[HIGH] Main/Features/FactionMap/FactionDisplayHelper.cs:92 — Localization bypass — Hover tooltips can display raw `{=taom_faction_...}Default` names because `PolygonWidget.cs:185` stores `faction.Name` directly, `FactionHoverService.cs:32` returns it as `FactionName`, and `ShowHoverTooltip` passes `change.FactionName` to `TooltipProperty` without `Localize`. Fix by localizing the hover title at the same boundary as selected faction content, while preserving raw-key comparison if needed for color lookup.

### MEDIUM

[MEDIUM] Main/_Module/ModuleData/factionmap/factions.json:161 — Special-units accuracy — 41 of 48 `special_units[].name` values do not exactly exist in the matching troop XML. Only Isengard's 3, Gondor's Citadel Guard/Swan Knight, and Mordor's Black Uruk/Cave Troll matched. Examples: `Pale Orc Champion`, `Gundabad War Troll`, `Noldor Blade-master`, `Galadhrim Guard`, `Iron Guard of Erebor`, `Rohirrim Royal Guard`, `Morgul Bowman`, `Easterling Bladelord`, `Mumakil War Tower`, and Umbar's three names were not found. Fix by using actual troop display names from `troops_<culture>.xml`, or rename the section so it no longer implies exact in-game unit names.

[MEDIUM] Main/_Module/ModuleData/factionmap/factions.json:575 — Content completeness — The four village notable-count feats are shipped but absent from the faction pages: Mordor `Slave Drivers` (+5% village notable count), Isengard `Iron Press` (+10%), Gundabad `Bone Camps` (+10%), and Dol Guldur `Hidden Hovels` (+10%). Fix by adding concrete positive bonus lines, or consciously document why village-notable density is hidden while town-notable density is shown.

[MEDIUM] Main/_Module/ModuleData/factionmap/factions.json:454 — Content accuracy — Erebor's description says "Master Smiths halve forge costs", but `TaomCulturalFeats.cs:432-435` initializes Master Smiths as "Smithing energy cost reduced by 30%" with `EffectBonus = -0.3f`. The perk and bonus rows are correct; the description is not. Fix the description to "reduces smithing energy by 30%" or equivalent.

### LOW

[LOW] Main/_Module/ModuleData/factionmap/factions.json:161 — Key naming convention — Special-unit keys use `_su_0_name` / `_su_0_desc`, while the stated convention and the spot-check key use `special_unit`. Runtime key coverage is clean, but this abbreviation is inconsistent with section-derived naming and makes manual review/translation spot-checking error-prone. Fix by renaming `_su_` keys to the documented convention or updating the convention/tests to bless `_su_`.

## Observations

- No invalid `game_faction` values `rohan` or `dol_guldur` were found. `kingdom_of_rohan` correctly maps to `vlandia`; `overlordship_of_dol_guldur` correctly maps to `dolguldur`.
- `FactionDisplayHelper.ApplyResult` covers selected faction `name`, `description`, `traits`, `bonuses`, `perks`, `strengths`, `weaknesses`, `special_units`, and `DifficultyText`.
- The right-panel prefab still contains static English labels such as `Perks`, `Special Units`, `Bonuses`, `Strengths`, `Weaknesses`, `Start`, and `This realm is not playable`; I did not count these as Phase 2 faction-content defects because they predate the JSON rewrite, but they remain untranslated for Phase 3 unless handled elsewhere.

## Verification

- Decompilation performed against installed v1.4.5 DLLs under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client`.
- Automated JSON/XML checks run with Python: 599 JSON localization tokens, 599 matched XML ids, 0 missing live keys.
- `taom_module_strings.xml` parsed as XML; both faction JSON and strings XML decode as UTF-8.
- `dotnet test TAOM.Tests --filter FactionMapDataTests` could not run in this sandbox. First attempt failed writing first-time-use files under `C:\Users\CodexSandboxOffline`; retry with `DOTNET_CLI_HOME` inside the repo progressed further but failed reading `C:\Users\mikew\AppData\Local\Microsoft SDKs` due sandbox denial.

CRITICAL: 0 | HIGH: 3 | MEDIUM: 3 | LOW: 1
VERDICT: ISSUES FOUND
