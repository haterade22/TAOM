# Wanderers and named companions

## What this file is

`Main/_Module/ModuleData/taom_wanderers.xml` holds 210 `<NPCCharacter>` blueprints for the hireable strangers who stand around in taverns, spread over 20 cultures. <!-- measured: grep -oE '<NPCCharacter[[:space:]]|<NPCCharacter$' Main/_Module/ModuleData/taom_wanderers.xml | wc -l 2026-09-05 --> It is a template file, not a cast list: nobody in a running campaign *is* one of those entries, each wanderer you meet is a fresh hero the engine cloned from one of them and then gave a generated first name. `Main/_Module/ModuleData/named_companions/named_companions.xml` works the opposite way round, 17 fixed lore characters (Gimli, Legolas, Aragorn and 14 more) who exist as real heroes from day one and stand in a settlement you picked by hand.

Everything a wanderer needs is spread over four files, and a named companion over four different ones. Get one of the eight wrong and the failure is usually silent.

## Where it lives and how it is registered

<!-- excerpt file="Main/_Module/SubModule.xml" -->

| File | Registered at | Root element | Per entry | Engine class |
|---|---|---|---|---|
| `Main/_Module/ModuleData/taom_wanderers.xml` | `SubModule.xml:740` `<XmlName id="NPCCharacters" path="taom_wanderers"/>` | `<NPCCharacters>` | `<NPCCharacter>` | `TaleWorlds.CampaignSystem.CharacterObject` |
| `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` | `SubModule.xml:762` `<XmlName id="SkillSets" .../>` | `<SkillSets>` | `<SkillSet>` | `TaleWorlds.Core.MBCharacterSkills` |
| `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml` | `SubModule.xml:771` `<XmlName id="EquipmentRosters" path="equipmentsets/taom_wanderer_equipment"/>` | `<EquipmentRosters>` | `<EquipmentRoster>` | `TaleWorlds.Core.MBEquipmentRoster` |
| `Main/_Module/ModuleData/taom_wanderer_strings.xml` | `SubModule.xml:870` `<XmlName id="GameText" .../>` | `<strings>` | `<string>` | game text, no object type |
| `Main/_Module/ModuleData/named_companions/named_companions.xml` | `SubModule.xml:781` `<XmlName id="NPCCharacters" .../>` | `<NPCCharacters>` | `<NPCCharacter>` | `TaleWorlds.CampaignSystem.CharacterObject` |
| `Main/_Module/ModuleData/named_companions/named_companion_strings.xml` | `SubModule.xml:879` `<XmlName id="GameText" .../>` | `<strings>` | `<string>` | game text, no object type |
| `Main/_Module/ModuleData/characters/heroes.xml` | `SubModule.xml:148` `<XmlName id="Heroes" path="characters/heroes"/>` | `<Heroes>` | `<Hero>` | `TaleWorlds.CampaignSystem.Hero` |
| `Main/_Module/ModuleData/named_companions/named_companion_config.json` | not registered with the engine at all; read by TAOM C# at `Main/Features/NamedCompanions/NamedCompanionConfigProvider.cs:28` | JSON array | JSON object | `NamedCompanionDefinition` |

**Position in `SubModule.xml` does not matter for these files.** The engine loads by type, not by node order: `SkillSets` and `BodyProperties` in `Game.LoadBasicFiles` (`Game.cs:444-445`), then `EquipmentRosters` (`Campaign.cs:1472`), then `NPCCharacters` (`SandBoxManager.cs:362`), then `Heroes` (`SandBoxManager.cs:365`). A wanderer registered near the top of the file still finds its skill set and its equipment roster. See [Submodule and registration](submodule-and-registration.md) and [Load order and dependencies](load-order-and-dependencies.md).

The race names you may put in `race=` come from `LOTRLOME_Armory/ModuleData/skins.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. It defines 14 races and vanilla `Native/ModuleData/skins.xml` adds `human`, so 15 ids are legal: `human`, `dwarf`, `elf`, `orc`, `goblin`, `uruk`, `uruk_hai`, `pale_uruk`, `dg_uruk`, `berserker`, `nazghul`, `cave_troll`, `hill_troll`, `saruman`, `sauron`. <!-- measured: python script counting '<race id=' across every Modules/*/ModuleData/skins.xml with a multiline regex 2026-09-05 -->

## Attributes

This chapter documents only what makes an `<NPCCharacter>` a wanderer. Every other attribute on the element (`level`, `default_group`, `upgrade_targets`, `is_basic_troop` and the rest) behaves exactly as it does for a soldier and is documented once in [Troops](troops.md).

<!-- engine-ref type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" lines="536-601" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `is_template` | bool | yes for a wanderer | `false` | Marks the entry as a blueprint the engine copies rather than a character in the world. The wanderer pool is built from `IsTemplate && Occupation == Wanderer` | `CharacterObject.cs:544` |
| `occupation` | enum, `Wanderer` here | yes | `NotAssigned` | Puts the entry in the wanderer pool and opens the vanilla hire dialogue. A misspelling throws inside `Enum.Parse`, which truncates the load at that entry: the wanderers already read stay, the ones after it in the merged document are skipped in silence (`MBObjectManager.cs:1387-1395`, `:786-796`) | `CharacterObject.cs:539-542` |
| `is_hero` | bool | yes, `false` on a template and `true` on a named companion | `false` | `false` means the engine may clone this entry; `true` means it is one specific person and vanilla's wanderer churn never touches it | `BasicCharacterObject.cs:334` |
| `race` | string, one of the 15 skin ids | no | `0`, which is human | Which body, skeleton and animation set the wanderer wears. Native since 1.4.x, no C# needed. An id that is not registered throws `KeyNotFoundException`, it is not defaulted | `BasicCharacterObject.cs:324-328`, `FaceGen.cs:115-118` |
| `culture` | ref, `Culture.<id>` | yes | null | Decides the wanderer's **birthplace** and its generated first name, not where it stands. See the gotcha below | `BasicCharacterObject.cs:484` |
| `skill_template` | ref, `SkillSet.<id>` | no, but every TAOM wanderer uses one | a fresh empty skill set named after the character | Points at a block in `taom_wanderer_skill_sets.xml`. If it resolves, an inline `<skills>` child is discarded in silence | `BasicCharacterObject.cs:337-358` |
| `voice` | bare trait id, no `Trait.` prefix | no | `PersonaSoftspoken` | One of `curt`, `ironic`, `earnest`, `softspoken`. Picks the dialogue persona | `CharacterObject.cs:572` |
| `age` | int | no | at least 20 | Apparent age for face generation. For a wanderer the engine overrides it at spawn with `HeroComesOfAge + 5 + rand(12)` | `BasicCharacterObject.cs:485-486`, `CompanionsCampaignBehavior.cs:389` |
| `name` | localized string | no | no display name | For a wanderer this is the **surname pattern**, and the engine substitutes the culture's generated first name into `{FIRSTNAME}` | `BasicCharacterObject.cs:318-322`, `NameGenerator.cs:76-79, 130-137` |

The `<Hero>` row that a named companion also needs has only two attributes worth writing.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Hero" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs" lines="1803-1852" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | crash | Must match an `<NPCCharacter>` id exactly. `Hero.Deserialize` looks the character up by this id and dereferences it one line later | `Hero.cs:1805-1807` |
| `faction` | ref, `Faction.<clan_id>` | yes | `NullReferenceException` | Which clan the hero joins. The literal value `Faction.neutral` is the engine's escape hatch: the id is read, compared, and the clan assignment is skipped, leaving the companion clanless | `Hero.cs:1834-1835` |

The JSON config that places a named companion has four fields, all read by TAOM code rather than the engine.

<!-- engine-ref type="TAOM.Features.NamedCompanions.Domain.NamedCompanionDefinition" file="Main/Features/NamedCompanions/Domain/NamedCompanionDefinition.cs" lines="5-18" -->

| Field | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `character_id` | string | yes | the row does nothing | Must equal the `<NPCCharacter>` id and the `<Hero>` id | `Main/Features/NamedCompanions/Domain/NamedCompanionDefinition.cs:8` |
| `spawn_settlement` | string | yes | placement throws and is logged | A settlement id, for example `town_E1` | `NamedCompanionDefinition.cs:11` |
| `race` | string | yes | race stays whatever the XML gave | Re-applied at spawn as insurance over the XML `race=` | `NamedCompanionDefinition.cs:14` |
| `enabled` | bool | no | `true` | Set `false` to park a companion without deleting anything | `NamedCompanionDefinition.cs:17` |

## Child elements

<!-- engine-ref type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" lines="315-527" -->

| Child | Used by | Shape | What to know | Read at (file:line) |
|---|---|---|---|---|
| `<face>` | both | `<face><face_key_template value="BodyProperty.fighter_erebor"/></face>` | Every one of the 210 wanderers uses a shared `face_key_template`; the 17 named companions use an inline `<BodyProperties key="...">` instead so their faces are fixed. A template beats an inline block when both are present | `BasicCharacterObject.cs:415-476` |
| `<Traits>` | both | `<Traits><Trait id="Mercy" value="-1"/></Traits>` | Case sensitive, `<traits>` is ignored. An unknown trait id is skipped in silence, and `value="0"` deletes the entry rather than storing a zero | `CharacterObject.cs:551` |
| `<skills>` | named companions only | `<skills><skill id="OneHanded" value="200"/></skills>` | Discarded outright if `skill_template` resolved. Wanderers use the template, companions use the inline block, never both | `BasicCharacterObject.cs:353-358` |
| `<Equipments>` | both | wanderers: two `<EquipmentSet id="..."/>` references; companions: two inline `<EquipmentRoster>` blocks | A referenced set uses `equipmentType="Civilian"`; an inline roster uses `civilian="true"`. Both parse, they are not interchangeable spellings | `BasicCharacterObject.cs:360-413`, `MBEquipmentRoster.cs:88-101` |

## Worked example

A wanderer template, copied whole from the shipped file.

<!-- example file="Main/_Module/ModuleData/taom_wanderers.xml" id="spc_wanderer_erebor_0" -->

```xml
<NPCCharacter
		id="spc_wanderer_erebor_0"
		race="dwarf"
		name="{=aom_spc_wanderer_erebor_0_name}{FIRSTNAME} Bit Barukkhaz"
		voice="softspoken"
		age="28"
		is_template="true"
		default_group="Infantry"
		is_hero="false"
		culture="Culture.erebor"
		occupation="Wanderer"
		skill_template="SkillSet.spc_wanderer_erebor_0_skills">
		<face>
			<face_key_template value="BodyProperty.fighter_erebor" />
		</face>
		<Traits>
			<Trait id="Calculating" value="1" />
			<Trait id="Mercy" value="-1" />
		</Traits>
		<Equipments>
			<EquipmentSet id="npc_companion_equipment_template_erebor" equipmentType="Civilian" />
			<EquipmentSet id="npc_companion_equipment_template_erebor" />
		</Equipments>
	</NPCCharacter>
```

What a reader changes first, in order of how much it moves:

1. **`race="dwarf"`.** The single most visible attribute. It swaps the body and skeleton, and it is also the attribute that forces the equipment choice: a dwarf in human-rigged cloth clips and floats, which is exactly the bug fixed in [hero-race.md](../features/hero-race.md) for these twelve Erebor entries.
2. **`skill_template="SkillSet.spc_wanderer_erebor_0_skills"`.** The whole personality of the hire. Edit the numbers in `taom_wanderer_skill_sets.xml`, not here, and do not add an inline `<skills>` block beside it.
3. **`name="{=aom_...}{FIRSTNAME} Bit Barukkhaz"`.** Only the surname is yours. The engine replaces `{FIRSTNAME}` with a name drawn from the culture's list, so write a family name or an epithet, never a full name.

A named companion, copied whole from its own file.

<!-- example file="Main/_Module/ModuleData/named_companions/named_companions.xml" id="named_companion_gimli" -->

```xml
<NPCCharacter id="named_companion_gimli" name="{=nc_gimli_name}Gimli" age="30" voice="earnest" culture="Culture.erebor" race="dwarf" default_group="Infantry" is_hero="true" occupation="Wanderer" face_mesh_cache="true">
			<face>
				<BodyProperties version="4" age="32.01" weight="0.7454" build="0.8379"  key="0000D404C000314508807EB03F1F0F70108788878FF88067888878888D3017F0008DB60308FFF0AE000000000000000000000000000000000000000043040142"  />
	</face>
			<skills>
				<skill id="OneHanded" value="200"/>
				<skill id="TwoHanded" value="280"/>
				<skill id="Polearm" value="200"/>
				<skill id="Bow" value="50"/>
				<skill id="Crossbow" value="50"/>
				<skill id="Throwing" value="50"/>
				<skill id="Riding" value="0"/>
				<skill id="Athletics" value="220"/>
				<skill id="Crafting" value="150"/>
				<skill id="Scouting" value="100"/>
				<skill id="Tactics" value="50"/>
				<skill id="Charm" value="50"/>
				<skill id="Roguery" value="260"/>
				<skill id="Leadership" value="40"/>
				<skill id="Trade" value="150"/>
				<skill id="Steward" value="80"/>
				<skill id="Medicine" value="100"/>
				<skill id="Engineering" value="160"/>
			</skills>
			<Traits>
				<Trait id="Honor" value="2"/>
				<Trait id="Generosity" value="1"/>
				<Trait id="Calculating" value="0"/>
				<Trait id="Mercy" value="1"/>
				<Trait id="Valor" value="2"/>
			</Traits>
			<Equipments>
				<EquipmentRoster>
				<equipment slot="Item0" id="Item.sm_dwarf_dain_axe_a" />
				<equipment slot="Head" id="Item.sk_dwarf_erebor_helmet_leather_heavy_a"/>
				<equipment slot="Cape" id="Item.sk_dwarf_iron_pauldron_medium_a"/>
				<equipment slot="Body" id="Item.sk_dwarf_erebor_chest_leather_med_b"/>
				<equipment slot="Gloves" id="Item.sk_dwarf_erebor_bracers_med_a"/>
				<equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_med_d"/>
				</EquipmentRoster>
				<EquipmentRoster civilian="true">
				<equipment slot="Cape" id="Item.sk_dwarf_iron_pauldron_medium_a"/>
				<equipment slot="Body" id="Item.sk_dwarf_erebor_chest_leather_med_b"/>
				<equipment slot="Gloves" id="Item.sk_dwarf_erebor_bracers_med_a"/>
				<equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_med_d"/>
				</EquipmentRoster>
			</Equipments>
		</NPCCharacter>
```

1. **`is_hero="true"` with `occupation="Wanderer"`.** The pairing is the whole trick. `is_hero="true"` keeps the entry out of the clone pool (`CompanionsCampaignBehavior.cs:348` only collects templates), while `occupation="Wanderer"` still opens the hire dialogue (`LordConversationsCampaignBehavior.cs:1276`).
2. **The inline `<EquipmentRoster>` pair.** A named companion carries its own gear, so every item id must exist in the Armory or the slot ends up empty with no error at all. The second roster is tagged `civilian="true"`, which is the correct spelling for an inline roster.
3. **`name="{=nc_gimli_name}Gimli"`.** No `{FIRSTNAME}` here, because a named companion is not run through the name generator.

That entry alone does nothing until three more rows exist. The hero row:

<!-- example file="Main/_Module/ModuleData/characters/heroes.xml" id="named_companion_gimli" -->

```xml
	<Hero id="named_companion_gimli" faction="Faction.neutral" />
```

The config row that says where he stands, one JSON object in `named_companion_config.json` with `"character_id": "named_companion_gimli"`, `"spawn_settlement": "town_E1"`, `"race": "dwarf"`, `"enabled": true`. And seven dialogue strings, keyed `<kind>.<character_id>`:

<!-- example file="Main/_Module/ModuleData/named_companions/named_companion_strings.xml" id="prebackstory.named_companion_gimli" -->

```xml
	<string id="prebackstory.named_companion_gimli"
		text="{=nc_prebackstory.named_companion_gimli_text}A dwarf warrior far from the mountains? What brings you here?" />
```

The other six ids are `backstory_a`, `backstory_b`, `backstory_c`, `backstory_d`, `response_1` and `response_2`, each with the same `.named_companion_gimli` suffix. All 17 companions ship all seven (119 strings). <!-- measured: grep -o '<string id=' Main/_Module/ModuleData/named_companions/named_companion_strings.xml | wc -l 2026-09-05 -->

## Recipes: Add / Modify / Delete

### Add a wanderer template

1. Open `Main/_Module/ModuleData/taom_wanderers.xml`, copy the nearest entry of the same culture, and give it the next free id in the `spc_wanderer_<culture>_<N>` sequence. Ids are unique across every `NPCCharacters` file in the mod, not just this one.
2. Set `culture="Culture.<id>"` to the culture you actually want. Copying a block and forgetting this line is the mistake [kingdom-creation.md](../features/kingdom-creation.md) records.
3. Set `race=` if the culture is not human, and leave it off if it is. Check the value against the 15 ids listed above.
4. Point `face_key_template` at a `BodyProperty` that exists. TAOM's own live under `Main/_Module/ModuleData/TAOM_bodyproperties.xml`; `fighter_battania`, still used by the ten Dunland wanderers, is vanilla's and lives in `SandBoxCore/ModuleData/sandboxcore_bodyproperties.xml`.
5. Add a matching `<SkillSet id="spc_wanderer_<culture>_<N>_skills">` to `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` and reference it from `skill_template`. Do not also write an inline `<skills>` block.
6. Reference an existing roster from `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml` twice, once with `equipmentType="Civilian"` and once without, or add a new `<EquipmentRoster>` there first.
7. Add the seven backstory strings to `Main/_Module/ModuleData/taom_wanderer_strings.xml`, keyed `<kind>.spc_wanderer_<culture>_<N>`, then run the localization pipeline for the twelve languages ([TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md)).

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Add a named companion

1. Add the `<NPCCharacter>` to `Main/_Module/ModuleData/named_companions/named_companions.xml` with `is_hero="true"`, `occupation="Wanderer"`, a `culture=`, a `race=` and inline `<skills>`, `<Traits>` and `<Equipments>`. Verify every `Item.` id exists in the Armory before you save; a typo leaves the slot empty and nothing warns you.
2. Add `<Hero id="<same id>" faction="Faction.neutral" />` to `Main/_Module/ModuleData/characters/heroes.xml`. Miss it and the character is a troop nobody can talk to; write it without `faction=` and `Hero.Deserialize` throws.
3. Add one JSON object to `Main/_Module/ModuleData/named_companions/named_companion_config.json` with `character_id`, `spawn_settlement`, `race` and `enabled`. The three id spellings must match exactly, and nothing in the test suite checks that they do.
4. Add the seven `<string>` rows to `Main/_Module/ModuleData/named_companions/named_companion_strings.xml`, then run the localization pipeline.
5. Confirm `spawn_settlement` is a real settlement id in the live `TAOM_Map/ModuleData/settlements.xml`.

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only
Code: No code changes needed

### Modify

1. Skill numbers live in `taom_wanderer_skill_sets.xml` for a template and inline for a named companion. Change one place, never both.
2. Gear for a template lives in `equipmentsets/taom_wanderer_equipment.xml` and is shared by every wanderer of that culture, so a change there moves ten to fifteen characters at once. Gear for a named companion lives inline in his own entry.
3. Changing the English text inside a `{=KEY}` leaves all twelve translations stale, because the translator only fills untranslated rows. Re-run the pipeline after any wording change.
4. Editing a template does not touch a wanderer already walking around in a save. Their skills, race and name were copied at creation and are stored in the save; they update only when that wanderer dies and the engine spawns a replacement.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Delete

1. Deleting a wanderer template is safe. It only shrinks the pool the engine draws from, and heroes already cloned from it keep working. Delete the `<NPCCharacter>`, its `<SkillSet>` and its seven strings together.
2. Deleting a **named companion** is not safe for an existing save: the `<Hero>` is a saved object, so removing the XML leaves the save pointing at a hero the engine can no longer build. Prefer `"enabled": false` in `named_companion_config.json`, which stops TAOM placing him while leaving every object intact.
3. If you do remove one for a fresh campaign, take out all four pieces at once: the `<NPCCharacter>`, the `<Hero>` row, the JSON object and the seven strings.
4. Do not delete an `<EquipmentRoster>` from `taom_wanderer_equipment.xml` while any wanderer still references it. An unresolved `<EquipmentSet id="...">` is a hard load failure, not a naked character.

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **`culture=` does not decide where a wanderer appears.** The engine picks the settlement first, from any town at random, then uses the culture only to choose a matching town as the hero's birthplace, falling back to a random town when no town of that culture exists. So a wanderer of a landless culture still turns up, and fixing the `culture` attribute will not by itself move a wanderer into your kingdom's towns. `CompanionsCampaignBehavior.cs:378-393`.
- **Only about 47 of the 210 templates are in the world at once.** The engine spawns until the number of distinct live templates reaches `Town.AllTowns.Count * 0.6`, which is 46.8 against TAOM's 78 towns, kills one wanderer on a 10 percent daily roll, and forgets a dead one after 40 days. Adding templates widens the variety, it does not add population. `CompanionsCampaignBehavior.cs:37, 113, 215-233, 239, 287`.
- **A typo in `race=` throws, it is not defaulted.** `FaceGen.GetRaceOrDefault` reads a plain `Dictionary<string,int>` indexer, so an unregistered name raises `KeyNotFoundException` during deserialization. Only a null FaceGen instance gives you the 0 fallback the method name implies. `FaceGen.cs:40-42, 115-118`.
- **A typo in an `<EquipmentSet id="...">` reference is a hard crash.** The lookup returns null and `AddEquipmentRoster` iterates it with no null check. Contrast a typo in an `Item.` id, which is silent and leaves the slot empty. `MBEquipmentRoster.cs:110-119`, `Equipment.cs:445-450`.
- **A typo in `skill_template` is silent and leaves the wanderer at zero skills.** The engine creates an empty skill set named after the character and moves on, and TAOM's validator has no code for an unresolved skill-set reference. It does have `SKILL_TEMPLATE_SHADOWS_SKILLS`, which fires when an entry carries both a template and inline `<skills>`. `BasicCharacterObject.cs:337-358`, `tools/taom_schema.py:987-1003`.
- **Four cultures ship wanderers with no backstory at all.** 40 of the 210 (`goblin`, `mistymountainorcs`, `lindon`, `bluecraig`, ten each) have no `prebackstory` or `backstory_*` string, and the same 40 borrow another culture's skill sets (`goblin`, `mistymountainorcs` and `bluecraig` from `gundabad`, `lindon` from `rivendell`). Talking to one gets vanilla's "I do not care to talk about my past" fallback. <!-- measured: python script diffing spc_wanderer ids in taom_wanderers.xml against the '.'-suffixed ids in taom_wanderer_strings.xml, and comparing each entry's skill_template prefix to its own id 2026-09-05 -->
- **The 119 named-companion backstory strings are not reachable on the current code path.** The dialogue that reads them hangs off `start_wanderer_unmet`, which is gated by `ConversationUseMeetingDialogs`, which returns false as soon as the hero's `HasMet` flag is set (`LordConversationsCampaignBehavior.cs:221, 609-618, 1222-1226`); TAOM sets that flag on every companion at placement (`Main/Features/NamedCompanions/NamedCompanionService.cs:54`, `Main/Adapters/NamedCompanionAdapter.cs:82-86`). The same branch would also break on a companion anyway, because it keys the lookup off `Hero.Template.StringId` and `Hero.Template` is null for a hero built from `heroes.xml` rather than cloned from a template (`Hero.cs:298`, `CharacterObject.cs:419`). **TAOM has never recorded an in-game check of this.** [named-companions.md](../features/named-companions.md) claims the vanilla flow triggers; if you want to settle it, talk to a companion in game and see which lines appear.
- **Vanilla's wanderer introduction dereferences one hardcoded settlement id.** `Settlement.FindFirst(x => x.StringId == "town_ES4")` is called with no null guard while setting up the backstory text, so deleting or renaming `town_ES4` would crash the first conversation with any generated wanderer. It still exists in the live `TAOM_Map/ModuleData/settlements.xml`. `LordConversationsCampaignBehavior.cs:1751`. <!-- measured: grep -c 'id="town_ES4"' on the live TAOM_Map settlements.xml 2026-09-05 -->
- **The two wanderer generators overwrite four files with no dry run and no backup.** `tools/extract_wanderers.py` and `tools/generate_batch2_wanderers.py` take no arguments and write in place. Worse, both write `Main/_Module/ModuleData/taom_wanderer_equipment.xml`, at the ModuleData root, while the file the game actually loads is `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml` (`SubModule.xml:771`). Running either leaves a stray root-level file the engine never reads and the real roster untouched. `tools/generate_batch2_wanderers.py:11-12, 605, 651-658`, `tools/extract_wanderers.py:18, 458-460`.
- **Nothing in the test suite reads the shipped companion data.** Both `TAOM.Tests/Features/NamedCompanions/` test files use mocks and a temp directory, so the 1:1:1:7 agreement between `named_companions.xml`, `heroes.xml`, `named_companion_config.json` and the strings file is held by hand. It agrees today. `TAOM.Tests/Features/NamedCompanions/NamedCompanionConfigProviderTests.cs:21-25`.
- **The player-switcher feature offers wanderers as takeover targets only for cultures that have them,** which is 20 of TAOM's cultures. That is the same 20 counted below, and it is why an empty wanderer list in a new culture makes the switcher look broken. [player-switcher.md](../features/player-switcher.md).

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 210 wanderer templates | `grep -oE '<NPCCharacter[[:space:]]|<NPCCharacter$' Main/_Module/ModuleData/taom_wanderers.xml \| wc -l` | 2026-09-05 |
| 20 cultures, 17 of them with exactly 10; mordor 15, gondor 13, erebor 12 | `grep -o 'id="spc_wanderer_[a-z]*_' Main/_Module/ModuleData/taom_wanderers.xml \| sort \| uniq -c \| sort -rn` | 2026-09-05 |
| 127 entries carry `race=`, 83 omit it and default to human; elf 40, goblin 20, orc 16, dwarf 12, dg_uruk 10, pale_uruk 10, uruk_hai 7, uruk 6, berserker 3, human 3 | `grep -o 'race="[a-z_]*"' Main/_Module/ModuleData/taom_wanderers.xml \| sort \| uniq -c` | 2026-09-05 |
| 170 wanderer skill sets for 210 entries, so 40 entries borrow another culture's | `grep -o '<SkillSet id=' Main/_Module/ModuleData/taom_wanderer_skill_sets.xml \| wc -l` plus a python pass comparing each entry's `skill_template` to its own id | 2026-09-05 |
| 18 wanderer equipment rosters for 20 cultures (abanissa and shaghana both use harad's) | `grep -o '<EquipmentRoster id=' Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml \| wc -l` plus a python pass mapping culture to roster | 2026-09-05 |
| 1337 wanderer strings: 7 each for 170 characters, plus 147 `generic_backstory` rows | `grep -o '<string id=' Main/_Module/ModuleData/taom_wanderer_strings.xml \| wc -l` plus a python pass grouping ids by kind | 2026-09-05 |
| 40 wanderers with no backstory strings, all in goblin, mistymountainorcs, lindon, bluecraig | python pass diffing the wanderer ids against the string-file suffixes | 2026-09-05 |
| 17 named companions in the XML, 17 config rows, 17 `<Hero>` rows, 119 strings | `grep -oE '<NPCCharacter[[:space:]]' .../named_companions.xml \| wc -l`; `grep -c '"character_id"' .../named_companion_config.json`; `grep -c 'id="named_companion_' Main/_Module/ModuleData/characters/heroes.xml`; `grep -o '<string id=' .../named_companion_strings.xml \| wc -l` | 2026-09-05 |
| 1001 `<Hero>` rows in `characters/heroes.xml` | `grep -o '<Hero\b' Main/_Module/ModuleData/characters/heroes.xml \| wc -l` | 2026-09-05 |
| 78 towns (and 143 castles, 607 villages, 159 hideouts) in the live map, giving a live wanderer ceiling of 46.8 | python pass over the live `TAOM_Map/ModuleData/settlements.xml` counting `Components/Town` with and without `is_castle` | 2026-09-05 |
| 15 legal race ids | python pass counting `<race id=` across every `Modules/*/ModuleData/skins.xml` | 2026-09-05 |
| 14 face templates in use, 13 of them TAOM's and `fighter_battania` vanilla's | python pass collecting `face_key_template` values and resolving them against every `<BodyProperty id=` in the repo and the game install | 2026-09-05 |

## Read next

- [named-companions.md](../features/named-companions.md), the feature doc for the 17 lore companions.
- [hero-race.md](../features/hero-race.md), the wanderer race fix and the rule that gear must match the skeleton.
- [kingdom-creation.md](../features/kingdom-creation.md), where wanderers sit in the 13-file order for a new kingdom.
- [player-switcher.md](../features/player-switcher.md), for how wanderers appear as takeover targets.
- [moduledata-validation.md](../features/moduledata-validation.md), for what the validator does and does not cover.
- [tools/README.md](../../tools/README.md), for the wanderer generators and every other data tool.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/body-properties.md](./body-properties.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/skill-sets.md](./skill-sets.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
