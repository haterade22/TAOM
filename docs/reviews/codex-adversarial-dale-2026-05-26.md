# Codex Adversarial Review - Dale Culture

Date: 2026-05-26
Scope: Dale armor, Dale 27-troop tree, Sturgia/Dale culture wiring, author-time generators, referenced vanilla/TAOM data.

Verdict: ISSUES FOUND

Summary: P1: 1 | P2: 1 | P3: 1

## Vanilla Code

Source note: `tools/taom-src.ps1` resolves the culture and party-template types as `TaleWorlds.CampaignSystem.CultureObject`, `TaleWorlds.CampaignSystem.Party.PartyTemplateObject`, and `TaleWorlds.CampaignSystem.Party.PartyTemplateStack`. The requested `TaleWorlds.Core.CultureObject`/`TaleWorlds.CampaignSystem.PartyTemplateObject` names are not the installed v1.4.5 type names. `ArmorComponent` was read from installed v1.4.5 assemblies via `ilspycmd` because the taom-src cache write was denied for that type.

### CultureObject.Deserialize

`CultureObject.Deserialize` confirms all nine Dale XSLT military attributes are valid vanilla attributes. It also confirms militia, rebel, vassal reward, and settlement patrol template overrides are separate attributes from `default_party_template`.

```csharp
DefaultPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("default_party_template", node);
VillagerPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("villager_party_template", node);
FishingPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("fishing_party_template", node);
MilitiaPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("militia_party_template", node);
RebelsPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("rebels_party_template", node);
BanditBossPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("bandit_boss_party_template", node);
VassalRewardTroopsPartyTemplate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("vassal_reward_party_template", node);
SettlementPatrolPartyTemplateWeak = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_1", node);
SettlementPatrolPartyTemplateModerate = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_2", node);
SettlementPatrolPartyTemplateStrong = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_level_3", node);
SettlementPatrolPartyTemplateNaval = objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("settlement_patrol_template_coastal", node);
EliteBasicTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("elite_basic_troop", node);
MeleeEliteMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("melee_elite_militia_troop", node);
RangedEliteMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_elite_militia_troop", node);
MeleeMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("melee_militia_troop", node);
RangedMilitiaTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_militia_troop", node);
BasicTroop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_troop", node);
DefaultBattleEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("default_battle_equipment_roster", node);
DefaultCivilianEquipmentRoster = objectManager.ReadObjectReferenceFromXml<MBEquipmentRoster>("default_civilian_equipment_roster", node);
```

`caravan_party_templates` and `elite_caravan_party_templates` are child elements, not attributes:

```csharp
else if (item5.Name == "caravan_party_templates")
{
    foreach (XmlNode childNode16 in item5.ChildNodes)
    {
        mBList10.Add(objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("id", childNode16));
    }
}
else if (item5.Name == "elite_caravan_party_templates")
{
    foreach (XmlNode childNode17 in item5.ChildNodes)
    {
        mBList11.Add(objectManager.ReadObjectReferenceFromXml<PartyTemplateObject>("id", childNode17));
    }
}
```

### ArmorComponent / Gender Variations

The installed v1.4.5 armor data class is `TaleWorlds.Core.ArmorComponent`. It parses `has_gender_variations` into `MultiMeshHasGenderVariations` and parses `no_slim` independently.

```csharp
public bool MultiMeshHasGenderVariations { get; private set; }

public bool IsNoSlim { get; private set; }

public override void Deserialize(MBObjectManager objectManager, XmlNode node)
{
    base.Deserialize(objectManager, node);
    ...
    MultiMeshHasGenderVariations = true;
    if (node.Attributes["has_gender_variations"] != null)
    {
        MultiMeshHasGenderVariations = Convert.ToBoolean(node.Attributes["has_gender_variations"].Value);
    }
    ...
    IsNoSlim = node.Attributes["no_slim"] != null && Convert.ToBoolean(node.Attributes["no_slim"].Value);
}
```

`ItemObject.Deserialize` derives `MultiMeshName` from the `mesh` attribute, not from the item id:

```csharp
XmlNode xmlNode7 = node.Attributes["mesh"];
if (xmlNode7 != null && !string.IsNullOrEmpty(xmlNode7.InnerText))
{
    MultiMeshName = xmlNode7.InnerText;
}
...
case "Armor":
    itemComponent = new ArmorComponent(this);
    break;
```

`Module.GetMetaMeshPackageMapping` registers the base armor mesh plus `_converted`, `_converted_slim`, and `_slim` variants from `MultiMeshName`. This confirms the slim mesh name is derived from the item mesh name and does not require a separate item entry.

```csharp
public static void GetMetaMeshPackageMapping(Dictionary<string, string> metaMeshPackageMappings)
{
    foreach (ItemObject objectType in Game.Current.ObjectManager.GetObjectTypeList<ItemObject>())
    {
        if (objectType.HasArmorComponent)
        {
            string value = ((objectType.Culture != null) ? objectType.Culture.StringId : "shared") + "_armor";
            metaMeshPackageMappings[objectType.MultiMeshName] = value;
            metaMeshPackageMappings[objectType.MultiMeshName + "_converted"] = value;
            metaMeshPackageMappings[objectType.MultiMeshName + "_converted_slim"] = value;
            metaMeshPackageMappings[objectType.MultiMeshName + "_slim"] = value;
        }
    }
}
```

### PartyTemplateObject.Deserialize and PartyTemplateStack

The `min_value`, `max_value`, and `troop` attribute names used by the new Dale party templates match v1.4.5.

```csharp
public override void Deserialize(MBObjectManager objectManager, XmlNode node)
{
    Stacks = new MBList<PartyTemplateStack>();
    ShipHulls = new MBList<ShipTemplateStack>();
    base.Deserialize(objectManager, node);
    foreach (XmlNode childNode in node.ChildNodes)
    {
        if (childNode.Name == "stacks")
        {
            foreach (XmlNode childNode2 in childNode.ChildNodes)
            {
                if (childNode2.Name == "PartyTemplateStack")
                {
                    PartyTemplateStack item = new PartyTemplateStack(
                        (CharacterObject)objectManager.ReadObjectReferenceFromXml("troop", typeof(CharacterObject), childNode2),
                        Convert.ToInt32(childNode2.Attributes["min_value"].Value),
                        Convert.ToInt32(childNode2.Attributes["max_value"].Value));
                    Stacks.Add(item);
                }
            }
        }
    }
}
```

```csharp
public struct PartyTemplateStack
{
    public CharacterObject Character;
    public int MinValue;
    public int MaxValue;

    public PartyTemplateStack(CharacterObject character, int minValue, int maxValue)
    {
        Character = character;
        MinValue = minValue;
        MaxValue = maxValue;
    }
}
```

## Config Cross-Reference

- `troops_dale.xml`: 27 `NPCCharacter` ids.
- Upgrade references: 23 total, 0 missing. Every `upgrade_target id="NPCCharacter.dale_*"` resolves inside `troops_dale.xml`.
- New Dale party-template troop references: 38 total, 0 missing. Every troop referenced by the 9 new `taom_partyTemplates.xml` entries resolves inside `troops_dale.xml`.
- Inline equipment schema: 407 lowercase `<equipment>` tags, 0 uppercase `<Equipment>` tags in `troops_dale.xml`.
- Item references: 171 inline references checked, 0 missing.
- Item reference sources: 121 Dale armory items, 49 SandBoxCore weapon/horse/shield items, 1 LOTRAOM horse armor item.
- Dale armor canonical folder check: all 163 `sk_dale_*`/`clo_sk_dale_*` item ids are under `LOTRLOME_items/dale`; no duplicate or outside-folder ids found for the Dale prefixes.
- Slim mesh manifest check: all six expected `_slim` mesh ids are present in `tools/dale_armor_meshes.txt`.
- Bow ids checked in SandBoxCore `weapons.xml`: `hunting_bow`, `mountain_hunting_bow`, `lowland_yew_bow`, `lowland_longbow`, `noble_bow` all exist.
- Civilian roster tags: `dale_civ_template_default_a` through `dale_civ_template_default_e` all have `EquipmentSet equipmentType="Civilian"`.
- `VolunteerRecruitmentService` Sturgia collision check: exactly one `CultureMap["sturgia"]` write found. No `.Add("sturgia", ...)` alternative write and no Sturgia settlement-level `SettlementMap` override found.

Bow stat order from SandBoxCore:

| Bow | Difficulty | Missile speed | Damage | Accuracy | Speed |
| --- | ---: | ---: | ---: | ---: | ---: |
| `hunting_bow` | 0 | 64 | 40 | 85 | 87 |
| `mountain_hunting_bow` | 0 | 67 | 41 | 82 | 86 |
| `lowland_longbow` | 30 | 74 | 57 | 94 | 80 |
| `lowland_yew_bow` | 50 | 79 | 69 | 94 | 90 |
| `noble_bow` | 70 | 90 | 80 | 100 | 94 |

## Known Suspects

1. `has_gender_variations="true"` auto-slim behavior: CONFIRMED as valid on the managed data contract. `ArmorComponent` consumes `has_gender_variations`; `ItemObject` gets `MultiMeshName` from the `mesh` attribute; `Module.GetMetaMeshPackageMapping` registers `MultiMeshName + "_slim"`. The six `_slim` meshes exist in the manifest. No separate `_slim` item is required by the managed item schema.

2. `dale_kings_bowman` Bow 230: DISPUTED as a cap bug. The value is below 250 and below TAOM Rohan elite mounted archer values observed in the existing tree. It does not hit the cited vanilla ceiling. See Finding 3 for a separate cavalry calibration issue.

3. Vanilla bow mapping: CONFIRMED issue. All ids exist, but the presumed order was wrong: `lowland_yew_bow` is stronger than `lowland_longbow`. Dale T5 can roll the stronger yew bow while T6 royal archers always use the weaker longbow. See Finding 2.

4. Standalone civilian `EquipmentSet equipmentType="Civilian"`: DISPUTED. All existing `dale_civ_template_default_a..e` sets are correctly tagged.

5. `CultureMap["sturgia"]` collision risk: DISPUTED. Grep found one Sturgia culture-map write and no Sturgia settlement-map override in `VolunteerRecruitmentService`.

6. Solus mesh-name typo risk: DISPUTED as authored. The manifest and XML both preserve the split spelling: `chivlary` for non-chest armor families and `chivalry` for chest. Authored counts are higher because the same mesh token appears across id/name/mesh fields; no missing or cross-folder Dale armor ids were found.

7. Inline-equipment capitalization: DISPUTED. `troops_dale.xml` uses lowercase `<equipment>` for inline troop equipment, matching vanilla `spnpccharacters.xml`.

## Deep Analysis

### Skill-Curve Fairness

The "Excellent Archers" claim is broadly realized for the archer line and does not exceed vanilla-style ceilings. The cavalry curve does not match the generator's own documented Rohan-relative calibration.

| Role | Dale troop | Comparator | Result |
| --- | --- | --- | --- |
| Archer mid | `dale_bowman` L19 Bow 90 | `rohan_eastfold_bowman` L16 Bow 80 | Dale is +10, matches the design note. |
| Archer high | `dale_royal_archer` L32 Bow 160 | `erebor_noble_veteran_archer` L31 Bow 140 | Dale is +20; strong but not cap-breaking. |
| Infantry elite | `dale_kings_champion` L46 OneHanded 220 / Polearm 205 | `erebor_noble_royal_warden` L46 OneHanded 220 / Polearm 150 | Dale keeps equivalent sword skill and stronger spear identity. |
| Cavalry high | `dale_royal_cavalier` L32 Riding 140 / Polearm 145 | `rohan_edoras_golden_hall_eorlingas_rider` L31 Riding 230 / Polearm 260 | Dale is about 39-44% under, not the documented ~10% under Rohan. |
| Cavalry elite | `dale_kinsman_of_eorl` L39 Riding 170 / Polearm 175 | `rohan_edoras_golden_hall_kings_own_rider` L36 Riding 290 / Polearm 310 | Dale is about 41-44% under. |

### Save-Compat Risk

The change adds new troop ids and does not rename or delete existing vanilla Sturgia ids. Existing player parties and lord parties holding vanilla Sturgian troops should keep those object references.

The save-compat impact is generation-forward: volunteer pools for `Culture.sturgia` start producing Dale troops through `VolunteerRecruitmentService`. However, militia, patrol, rebels, and vassal reward generation still use vanilla Sturgia party templates unless Finding 1 is fixed, so existing saves will continue creating vanilla Sturgians through those paths.

### Visual / Lore Consistency

The Tolkien "long swords and tall spears" note is satisfied across the Dale tree rather than on every individual equipment roll. T4 `dale_man_at_arms` has one roster with `northern_spear_3_t4` and another with an axe/shield variant; the footman/spearman line carries the spear identity through higher tiers. I do not consider this a bug.

## Findings

## Finding 1: Dale declares militia, patrol, rebel, and reward templates but does not bind them to Culture.sturgia

Severity: P1

File: `Main/_Module/ModuleData/spcultures.xslt:1155`

What it does now:
The Sturgia/Dale XSLT block sets the Dale basic troops and `default_party_template`, but leaves the vanilla Sturgia `militia_party_template`, `rebels_party_template`, `vassal_reward_party_template`, and settlement patrol template attributes to pass through. The new Dale templates are declared in `taom_partyTemplates.xml:996`, `:1003`, `:1012`, `:1021`, `:1030`, and `:1038`, but grep found no consumer for those ids outside their declarations.

Why it's wrong:
Vanilla `CultureObject.Deserialize` reads those exact template attributes independently from `default_party_template`. Therefore the new Dale militia/patrol/rebel/reward templates are dead data, and live Sturgia/Dale settlements continue generating vanilla Sturgian militia, patrols, rebels, and vassal rewards.

Suggested fix:
Add the missing XSLT attributes in the `Culture[@id='sturgia']` block:

```xml
<xsl:attribute name="militia_party_template">PartyTemplate.militia_dale_template</xsl:attribute>
<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_dale_template</xsl:attribute>
<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_dale</xsl:attribute>
<xsl:attribute name="settlement_patrol_template_level_1">PartyTemplate.patrol_party_dale_template_level_1</xsl:attribute>
<xsl:attribute name="settlement_patrol_template_level_2">PartyTemplate.patrol_party_dale_template_level_2</xsl:attribute>
<xsl:attribute name="settlement_patrol_template_level_3">PartyTemplate.patrol_party_dale_template_level_3</xsl:attribute>
```

Also either bind/remove/document the currently unreferenced Dale mercenary and outlaw templates, and explicitly decide whether vanilla Sturgia villager/caravan templates are an accepted limitation.

## Finding 2: T5 Dale longbowmen can roll a stronger bow than T6 royal archers

Severity: P2

File: `Main/_Module/ModuleData/troops/troops_dale.xml:498`

What it does now:
`dale_longbowman` at level 25 can spawn with `Item.lowland_yew_bow`. Its upgrade target `dale_royal_archer` at level 32 uses only `Item.lowland_longbow` on both equipment rosters (`troops_dale.xml:545` and `:555`).

Why it's wrong:
SandBoxCore `weapons.xml` shows `lowland_yew_bow` is stronger than `lowland_longbow` in difficulty, missile speed, damage, and weapon speed. A T5 archer can therefore upgrade into a T6 archer with a worse bow.

Suggested fix:
Move `lowland_yew_bow` to the T6 royal archer rosters, or keep T5 on `lowland_longbow`/lower bows and reserve `lowland_yew_bow` for T6/T7. Update `tools/generate_dale_troops.py` so regeneration preserves the monotonic progression.

## Finding 3: Dale cavalry is much weaker than the documented "10% under Rohan" curve

Severity: P3

File: `tools/generate_dale_troops.py:24`

What it does now:
The generator documents Dale cavalry as "~10% below Rohan equivalent", and the branch header repeats "~10% under Rohan parity" at `tools/generate_dale_troops.py:195`. The emitted XML gives `dale_royal_cavalier` Riding 140 / Polearm 145 at level 32 and `dale_kinsman_of_eorl` Riding 170 / Polearm 175 at level 39.

Why it's wrong:
Tier-near Rohan cavalry are much higher: `rohan_edoras_golden_hall_eorlingas_rider` is level 31 with Riding 230 / Polearm 260, and `rohan_edoras_golden_hall_kings_own_rider` is level 36 with Riding 290 / Polearm 310. Dale is about 40-45% under, not 10% under. This makes the generated tree contradict its own balance contract.

Suggested fix:
Either retune Dale cavalry primary skills to approximately 90% of the chosen Rohan reference tier, or update the generator comments and Dale feature doc to state that Dale cavalry is intentionally far below Rohan, with the archery and infantry branches carrying the faction's power budget.
