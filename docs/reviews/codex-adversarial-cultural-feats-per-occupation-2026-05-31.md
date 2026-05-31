# Codex Adversarial Review - Cultural Feats Per-Occupation Notable Counts

Date: 2026-05-31
Scope: cultural-feats per-occupation town notable-count refactor, C#/XML/tests/docs listed in prompt.

## 1. Vanilla Code

Installed DLL verification was done with `ilspycmd` against `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`. Browsing snippets below match the local v1.4.5 decompiled tree under `E:\Decompiled_Bannerlord\`.

### `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement`

```csharp
public override int GetTargetNotableCountForSettlement(Settlement settlement, Occupation occupation)
{
	int result = 0;
	if (settlement.IsTown)
	{
		result = occupation switch
		{
			Occupation.Merchant => 2, 
			Occupation.GangLeader => 2, 
			Occupation.Artisan => 1, 
			_ => 0, 
		};
	}
	else if (settlement.IsVillage)
	{
		switch (occupation)
		{
		case Occupation.Headman:
			result = 1;
			break;
		case Occupation.RuralNotable:
			result = 2;
			break;
		}
	}
	return result;
}
```

### `NotableSpawnModel` base class

```csharp
public abstract class NotableSpawnModel : MBGameModel<NotableSpawnModel>
{
	public abstract int GetTargetNotableCountForSettlement(Settlement settlement, Occupation occupation);
}
```

### `FeatObject` and Add/AddFactor math

```csharp
public sealed class FeatObject : PropertyObject
{
	public enum AdditionType
	{
		Add,
		AddFactor
	}

	public static MBReadOnlyList<FeatObject> All => Campaign.Current.AllFeats;

	public float EffectBonus { get; private set; }

	public AdditionType IncrementType { get; private set; }

	public bool IsPositive { get; private set; }

	public FeatObject(string stringId)
		: base(stringId)
	{
	}

	public void Initialize(string name, string description, float effectBonus, bool isPositiveEffect, AdditionType incrementType)
	{
		Initialize(new TextObject(name), new TextObject(description));
		EffectBonus = effectBonus;
		IncrementType = incrementType;
		IsPositive = isPositiveEffect;
		AfterInitialized();
	}
}
```

`ExplainedNumber` is the vanilla math surface used by the feat consumers: `Add` changes the base value directly; `AddFactor` accumulates a factor and `ResultNumber` becomes `BaseNumber + BaseNumber * SumOfFactors`.

```csharp
public float ResultNumber => MathF.Clamp(_unclampedResultNumber, LimitMinValue, LimitMaxValue);

public int RoundedResultNumber => MathF.Round(ResultNumber);

public float BaseNumber { get; private set; }

public float SumOfFactors { get; private set; }

private float _unclampedResultNumber => BaseNumber + BaseNumber * SumOfFactors;

public void Add(float value, TextObject description = null, TextObject variable = null)
{
	if (value.ApproximatelyEqualsTo(0f))
	{
		return;
	}
	BaseNumber += value;
	if (_explainer != null && description != null && !value.ApproximatelyEqualsTo(0f))
	{
		if (variable != null)
		{
			description.SetTextVariable("A0", variable);
		}
		_explainer.AddLine(description.ToString(), value, StatExplainer.OperationType.Add);
	}
}

public void AddFactor(float value, TextObject description = null)
{
	if (!value.ApproximatelyEqualsTo(0f))
	{
		SumOfFactors += value;
		if (description != null && _explainer != null && !value.ApproximatelyEqualsTo(0f))
		{
			_explainer.AddLine(description.ToString(), MathF.Round(value, 3) * 100f, StatExplainer.OperationType.Multiply);
		}
	}
}
```

One vanilla feat consumer with both shapes:

```csharp
if (settlement.OwnerClan.Culture.HasFeat(DefaultCulturalFeats.BattanianMilitiaFeat))
{
	result.Add(DefaultCulturalFeats.BattanianMilitiaFeat.EffectBonus, CultureText);
}

if (village.Settlement.OwnerClan.Culture.HasFeat(DefaultCulturalFeats.EmpireVillageHearthFeat) && result.ResultNumber >= 0f)
{
	result.AddFactor(DefaultCulturalFeats.EmpireVillageHearthFeat.EffectBonus, GameTexts.FindText("str_culture"));
}
```

### `PartyBaseHelper.HasFeat`

```csharp
public static bool HasFeat(PartyBase party, FeatObject feat)
{
	if (party == null)
	{
		return false;
	}
	if (party.LeaderHero != null)
	{
		return party.LeaderHero.Culture.HasFeat(feat);
	}
	if (party.Culture != null)
	{
		return party.Culture.HasFeat(feat);
	}
	if (party.Owner != null)
	{
		return party.Owner.Culture.HasFeat(feat);
	}
	if (party.Settlement != null)
	{
		return party.Settlement.Culture.HasFeat(feat);
	}
	return false;
}
```

### Vanilla notable call sites and template selection

Startup calls only the five spawn-pool occupations:

```csharp
private void SpawnNotablesAtGameStart()
{
	foreach (Settlement item in Settlement.All)
	{
		if (item.IsTown)
		{
			int targetNotableCountForSettlement = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(item, Occupation.Artisan);
			for (int i = 0; i < targetNotableCountForSettlement; i++)
			{
				HeroCreator.CreateNotable(Occupation.Artisan, item);
			}
			int targetNotableCountForSettlement2 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(item, Occupation.Merchant);
			for (int j = 0; j < targetNotableCountForSettlement2; j++)
			{
				HeroCreator.CreateNotable(Occupation.Merchant, item);
			}
			int targetNotableCountForSettlement3 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(item, Occupation.GangLeader);
			for (int k = 0; k < targetNotableCountForSettlement3; k++)
			{
				HeroCreator.CreateNotable(Occupation.GangLeader, item);
			}
		}
		else if (item.IsVillage)
		{
			int targetNotableCountForSettlement4 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(item, Occupation.RuralNotable);
			for (int l = 0; l < targetNotableCountForSettlement4; l++)
			{
				HeroCreator.CreateNotable(Occupation.RuralNotable, item);
			}
			int targetNotableCountForSettlement5 = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(item, Occupation.Headman);
			for (int m = 0; m < targetNotableCountForSettlement5; m++)
			{
				HeroCreator.CreateNotable(Occupation.Headman, item);
			}
		}
	}
}
```

Weekly maintenance also uses only those same occupation lists:

```csharp
public static void SpawnNotablesIfNeeded(Settlement settlement)
{
	if (!settlement.IsTown && !settlement.IsVillage)
	{
		return;
	}
	List<Occupation> list = new List<Occupation>();
	if (settlement.IsTown)
	{
		list = new List<Occupation>
		{
			Occupation.GangLeader,
			Occupation.Artisan,
			Occupation.Merchant
		};
	}
	else if (settlement.IsVillage)
	{
		list = new List<Occupation>
		{
			Occupation.RuralNotable,
			Occupation.Headman
		};
	}
	float randomFloat = MBRandom.RandomFloat;
	float num = 0f;
	int num2 = 0;
	foreach (Occupation item in list)
	{
		num2 += Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(settlement, item);
	}
	num = ((settlement.Notables.Count > 0) ? ((float)(num2 - settlement.Notables.Count) / (float)num2) : 1f);
	num *= TaleWorlds.Library.MathF.Pow(num, 0.36f);
	if (!(randomFloat <= num))
	{
		return;
	}
	MBList<Occupation> mBList = new MBList<Occupation>();
	foreach (Occupation item2 in list)
	{
		int num3 = 0;
		foreach (Hero notable in settlement.Notables)
		{
			if (notable.CharacterObject.Occupation == item2)
			{
				num3++;
			}
		}
		int targetNotableCountForSettlement = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(settlement, item2);
		if (num3 < targetNotableCountForSettlement)
		{
			mBList.Add(item2);
		}
	}
	if (mBList.Count > 0)
	{
		EnterSettlementAction.ApplyForCharacterOnly(HeroCreator.CreateNotable(mBList.GetRandomElement(), settlement), settlement);
	}
}
```

Creation delegates template selection to `HeroCreationModel.GetRandomTemplateByOccupation`:

```csharp
public static Hero CreateNotable(Occupation occupation, Settlement settlement = null)
{
	CharacterObject randomTemplateByOccupation = Campaign.Current.Models.HeroCreationModel.GetRandomTemplateByOccupation(occupation, settlement);
	(CampaignTime birthDay, CampaignTime deathDay) birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(randomTemplateByOccupation, createAlive: true, -1);
	CampaignTime item = birthAndDeathDay.birthDay;
	CampaignTime item2 = birthAndDeathDay.deathDay;
	Hero hero = CreateHero(randomTemplateByOccupation, useCharacterAsTemplate: true, item, item2);
	HeroInitializationArgs heroInitializationArgs = new HeroInitializationArgs(hero, isOffspring: false).SetGenerateFirstAndFullName(value: true);
	if (settlement != null)
	{
		heroInitializationArgs.SetBornSettlement(settlement);
	}
	heroInitializationArgs.SetAppearance(Campaign.Current.Models.HeroCreationModel.GetStaticBodyProperties(hero, isOffspring: false, 0f));
	InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
	return hero;
}
```

Template selection samples from the culture's `NotableTemplates` every call; it does not remove templates already used for the settlement.

```csharp
public override CharacterObject GetRandomTemplateByOccupation(Occupation occupation, Settlement settlement = null)
{
	Settlement settlement2 = settlement ?? SettlementHelper.GetRandomTown();
	List<CharacterObject> list = settlement2.Culture.NotableTemplates.Where((CharacterObject x) => x.Occupation == occupation).ToList();
	int num = 0;
	foreach (CharacterObject item in list)
	{
		int num2 = item.GetTraitLevel(DefaultTraits.Frequency) * 10;
		num += ((num2 > 0) ? num2 : 100);
	}
	if (!list.Any())
	{
		return null;
	}
	int num3 = settlement2.RandomIntWithSeed((uint)settlement2.Notables.Count, 1, num);
	foreach (CharacterObject item2 in list)
	{
		int num4 = item2.GetTraitLevel(DefaultTraits.Frequency) * 10;
		num3 -= ((num4 > 0) ? num4 : 100);
		if (num3 < 0)
		{
			return item2;
		}
	}
	Debug.FailedAssert("Couldn't find template for given occupation!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultHeroCreationModel.cs", "GetRandomTemplateByOccupation", 311);
	return null;
}
```

## 2. Per-Occupation Add Math Walk

| Feat | Line | Bonus | Positive | Type | Target math | Verdict |
|---|---:|---:|---|---|---|---|
| `taom_isengard_notable_count_town_merchant` | `TaomCulturalFeats.cs:554` | `2f` | `true` | `Add` | 2 -> 4 | Clean |
| `taom_isengard_notable_count_town_artisan` | `TaomCulturalFeats.cs:558` | `1f` | `true` | `Add` | 1 -> 2 | Clean |
| `taom_isengard_notable_count_town_gang_leader` | `TaomCulturalFeats.cs:562` | `12f` | `true` | `Add` | 2 -> 14 | Clean |
| `taom_dolguldur_notable_count_town_merchant` | `TaomCulturalFeats.cs:662` | `1f` | `true` | `Add` | 2 -> 3 | Clean |
| `taom_dolguldur_notable_count_town_artisan` | `TaomCulturalFeats.cs:666` | `1f` | `true` | `Add` | 1 -> 2 | Clean |
| `taom_dolguldur_notable_count_town_gang_leader` | `TaomCulturalFeats.cs:670` | `13f` | `true` | `Add` | 2 -> 15 | Clean |
| `taom_mordor_notable_count_town_gang_leader` | `TaomCulturalFeats.cs:747` | `2f` | `true` | `Add` | 2 -> 4 | Clean |
| `taom_gundabad_notable_count_town_artisan` | `TaomCulturalFeats.cs:601` | `1f` | `true` | `Add` | 1 -> 2 | Clean |
| `taom_gundabad_notable_count_town_gang_leader` | `TaomCulturalFeats.cs:605` | `3f` | `true` | `Add` | 2 -> 5 | Clean |

All nine use `AdditionType.Add`, not `AddFactor`.

## 3. Two-Layer NPC Registration Audit

All 17 new NPC definitions parse as XML. Each new NPC has `is_template="true"`, `occupation="GangLeader"`, the expected `culture`, a `<face>` block, a `<face_key_template>`, and `<Equipments>`.

```text
spc_notable_isengard_gl5    | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl6    | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl7    | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl8    | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl9    | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl10   | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl11   | NPCCharacter? YES | Template line? YES
spc_notable_isengard_gl12   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl5   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl6   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl7   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl8   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl9   | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl10  | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl11  | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl12  | NPCCharacter? YES | Template line? YES
spc_notable_dolguldur_gl13  | NPCCharacter? YES | Template line? YES
```

Pool counts from `taom_spcultures.xml` templates joined to the NPC files:

```text
isengard: Merchant=10 Artisan=2 GangLeader=14 RuralNotable=3 Headman=3
dolguldur: Merchant=10 Artisan=2 GangLeader=15 RuralNotable=3 Headman=3
mordor: Merchant=10 Artisan=2 GangLeader=6 RuralNotable=3 Headman=3
gundabad: Merchant=10 Artisan=2 GangLeader=6 RuralNotable=3 Headman=3
```

## 4. Config Cross-Reference

New town feat IDs:

```text
taom_isengard_notable_count_town_merchant      | C# Register? YES | XML feat? YES
taom_isengard_notable_count_town_artisan       | C# Register? YES | XML feat? YES
taom_isengard_notable_count_town_gang_leader   | C# Register? YES | XML feat? YES
taom_dolguldur_notable_count_town_merchant     | C# Register? YES | XML feat? YES
taom_dolguldur_notable_count_town_artisan      | C# Register? YES | XML feat? YES
taom_dolguldur_notable_count_town_gang_leader  | C# Register? YES | XML feat? YES
taom_mordor_notable_count_town_gang_leader     | C# Register? YES | XML feat? YES
taom_gundabad_notable_count_town_artisan       | C# Register? YES | XML feat? YES
taom_gundabad_notable_count_town_gang_leader   | C# Register? YES | XML feat? YES
```

Both-direction XML check found exactly those nine `notable_count_town` feat IDs under the four affected culture blocks, with no unexpected XML-only town feat IDs.

Deleted uniform town IDs: no matches in production/config/test/feature-doc surfaces (`Main/`, `TAOM.Tests/`, `docs/features/`, `CHANGELOG.md`) for the deleted exact IDs with a trailing XML/C# quote. Historical review logs and this prompt naturally mention the old IDs and were not treated as dead config.

Village feat isolation:

```text
taom_isengard_notable_count_village   | C# Register? YES | XML feat? YES | AddFactor 0.10
taom_dolguldur_notable_count_village  | C# Register? YES | XML feat? YES | AddFactor 0.10
taom_mordor_notable_count_village     | C# Register? YES | XML feat? YES | AddFactor 0.05
taom_gundabad_notable_count_village   | C# Register? YES | XML feat? YES | AddFactor 0.10
```

Service dispatch keeps village feats only in the `RuralNotable` / `Headman` branch (`CulturalFeatsService.cs:314-332`), so they do not fire on Merchant/Artisan/GangLeader town occupations.

## 5. Findings Or Observations

### Known Suspects

1. CONFIRMED - MEDIUM - Template pool equality is not a uniqueness guarantee. Vanilla filters `settlement.Culture.NotableTemplates` by occupation on every `CreateNotable` call and samples from the full filtered list each time; it never removes already-used templates. Therefore Isengard's 14 GL templates for a target of 14 and Dol Guldur's 15 GL templates for a target of 15 prevent empty-pool/null failure, but they do not guarantee 14/15 distinct archetype selections. Headroom reduces duplicate probability; only a no-reuse selector would guarantee it.

2. DISPUTED - The `baseCount <= 0` guard is safe for the intended town occupations. Vanilla town targets are `Merchant => 2`, `GangLeader => 2`, `Artisan => 1`; the only town occupations returning zero are the occupations TAOM maps to `Other` and should not inflate.

3. DISPUTED - `MapOccupation` maps exactly the five occupations vanilla asks the notable spawn model about: Merchant, Artisan, GangLeader, RuralNotable, Headman. Vanilla returns zero for every other occupation in `DefaultNotableSpawnModel`, and the maintenance/startup callers never request Preacher/Mercenary/Lord/etc. for normal town/village notables.

4. DISPUTED - The nine `Initialize(...)` calls match the documented target table. All nine pass the documented bonus, `isPositiveEffect: true`, and `FeatObject.AdditionType.Add`.

5. DISPUTED - The nine new C# `Register(...)` string IDs exactly match the nine XML `<feat id=...>` entries under Isengard, Dol Guldur, Mordor, and Gundabad. No XML-only or C#-only town notable feat IDs were found.

6. DISPUTED - The four old uniform town feat IDs are removed from production/config/test/feature-doc surfaces. Matches remain only in historical review artifacts and the prompt, which are not live config or code.

7. DISPUTED - The 17 new NPCs pass the two-layer audit: all 17 have `NPCCharacter` definitions and matching `<notable_templates>` entries. Attributes and child blocks are present and XML-valid.

8. DISPUTED - Village isolation is intact. The four village feats still exist, are registered in XML, use `AddFactor`, and are only consulted for `RuralNotable` / `Headman`.

### Findings

[HIGH] TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs:724 — ADR-008 service coverage — The Dol Guldur Artisan town notable branch is not directly tested. The service has a `culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat)` branch at `CulturalFeatsService.cs:295-296`, but the dispatch tests cover Dol Guldur Merchant and GangLeader only; the artisan feat appears only in the reflection initialization table. — Add `ApplyNotableCountFeat_DolGuldurArtisan_AddsOne` asserting base `1` becomes `2`.

[MEDIUM] Main/_Module/ModuleData/taom_spcultures.xml:1763 — Template pool uniqueness — Isengard and Dol Guldur GL pools are exactly equal to the new targets, but vanilla samples templates with replacement from the whole occupation pool. If the acceptance criterion is "no duplicate GangLeader archetype in a 20-notable hub," exact equality is insufficient. — Either document that vanilla duplicate archetype selection is accepted, add substantial pool headroom to reduce the chance, or implement a TAOM no-reuse selection path.

### Additional Notes

No CRITICAL two-layer registration issue found.

`python tools\validate_moduledata.py` passed: no validation issues found.

Targeted `dotnet test TAOM.Tests --filter "FullyQualifiedName~CulturalFeats"` could not run in this sandbox. The first attempt failed during .NET first-use setup under `C:\Users\CodexSandboxOffline`; retrying with `DOTNET_CLI_HOME` inside the workspace got past first-use setup but MSBuild failed before test execution with `Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied`.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0 | INFO: 0
VERDICT: ISSUES FOUND
