# Equipment rosters

## What this file is

An equipment roster is one named outfit-and-loadout that a troop, lord, child, wanderer or the
player can be dressed in, stored apart from whoever wears it so that many characters share it. TAOM
keeps them in `Main/_Module/ModuleData/equipmentsets/`: one file per culture, plus seven
special-purpose files for lord templates, children, education stages, character creation, careers,
enlistment and wanderers. This chapter covers that standalone form, and the inline form inside a
character's own `<Equipments>` block where it differs.

## Where it lives and how it is registered

| Thing | Value |
|---|---|
| Folder | `Main/_Module/ModuleData/equipmentsets/` |
| Registration | `<XmlName id="EquipmentRosters" path="equipmentsets/<file-without-extension>"/>` in `Main/_Module/SubModule.xml` |
| Root element | `<EquipmentRosters>` |
| Per-entry element | `<EquipmentRoster id="..." culture="Culture....">` |
| Engine class | `TaleWorlds.Core.MBEquipmentRoster` (`MBEquipmentRoster.cs:56-105`) |
| Loaded by | `Campaign.InitializeDefaultCampaignObjects` (`Campaign.cs:1466-1473`) |

<!-- measured: ls Main/_Module/ModuleData/equipmentsets/*.xml | wc -l ; grep -c 'XmlName id="EquipmentRosters"' Main/_Module/SubModule.xml 2026-09-05 -->
There are **27 roster files** and **27 registrations**, so every file on disk is wired up. The
folder also holds 5 backup sidecars whose names end `.xml.bak-enlist-<timestamp>`; because they do
not end in `.xml` they are invisible to both the engine and the validator.

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
    <XmlNode>
      <XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_lindon"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
        <GameType value = "EditorGame"/>
      </IncludedGameTypes>
    </XmlNode>
```

Item ids inside a roster point at the Armory, for example
`LOTRLOME_Armory/ModuleData/LOTRLOME_items/rivendell/body_armors.xml`. This file lives in the game
install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate
with any fix.

### The seven special-purpose files, and what keys each one

<!-- measured: python - (ElementTree count of EquipmentRoster entries and distinct culture= values per file under Main/_Module/ModuleData/equipmentsets/) 2026-09-05 -->

| File | Rosters | Cultures | Id shape |
|---|---|---|---|
| `taom_equipment_sets_<culture>.xml` (20 files, 19 with content) | 10 to 74 each | 1 each | `<culture>_bat_template_medium_a`, `<culture>_civ_template_default_a` |
| `taom_lord_template_equipment.xml` | 226 | 22 | `taom_<culture>_lord_battle_male`, `taom_<culture>_ruler_civilian_female` |
| `taom_child_equipment_templates.xml` | 84 | 14 | `child_template_<culture>_noble_male` |
| `taom_education_equipment_templates.xml` | 1,568 | 16 | `child_education_equipments_stage_1_page_0_branch_default_<culture>` |
| `taom_char_creation_equipment.xml` | 880 | 16 | `player_char_creation_<culture>_<titletype>_<m or f>` |
| `taom_career_starting_equipment.xml` | 78 | 13 | `player_career_<culture>_<archetype>_<m or f>` |
| `taom_enlistment_equipment.xml` | 268 | 21 | `enlist_<culture>_<assignment>_<rank>` |
| `taom_wanderer_equipment.xml` | 18 | 18 | `npc_companion_equipment_template_<culture>` |

`taom_equipment_sets_named_companions.xml` is an empty placeholder holding 0 rosters: named companion
kit is inline, in `Main/_Module/ModuleData/named_companions/named_companions.xml`.

**Three different things find a roster, and only one of them is greppable.** The per-culture and
wanderer families are pointed at by an `<EquipmentSet id="..."/>` line in an XML file. The lord and
child template families are found by the engine searching for a matching `culture` plus `<Flags>`,
so their ids are never written anywhere else. The education, character-creation, career and
enlistment families are found by TAOM C# composing the id from parts at runtime
(`PlayerEquipmentRosterIds.cs:7`, `CareerEquipmentRosterIds.cs:16`).

<!-- measured: grep -rho '<EquipmentSet[^>]*id="<prefix>[^"]*"' --include=*.xml Main/_Module/ModuleData/ excluding equipmentsets/, per id prefix 2026-09-05 -->
That is why a missing career, education, character-creation, enlistment or child-template roster is
invisible to any reference check: those five id prefixes have **0** XML references between them,
against 420 for the wanderer family. It is also why `CareerCultureCoverageTests` exists.

### Load order does not depend on the order in SubModule.xml

`EquipmentRosters` is loaded whole, from every module at once, at `Campaign.cs:1472`, and
`NPCCharacters` only later at `SandBoxManager.cs:360-362`. Every roster is therefore registered
before any character file is parsed.

<!-- measured: python scratch script comparing each <EquipmentSet id=> reference in Main/_Module/ModuleData against the SubModule.xml line that registers the file defining that roster 2026-09-05 -->
**2,867** shipped references point at a roster file registered *later* in `SubModule.xml` than the
character file that uses them, and the game ships. Put your roster files wherever you like in the
registration list. Item ids are the ordering that does matter: `Items` loads at `Campaign.cs:1471`,
one line before rosters.

## Attributes

### `<EquipmentRoster>` and `<EquipmentSet>`

<!-- engine-table type="TaleWorlds.Core.MBEquipmentRoster" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBEquipmentRoster.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `culture` | object ref, `Culture.<id>` | No | none, and the engine prints `EquipmentRoster with id: <id> don't have culture definition` | On `<EquipmentRoster>`. Binds the roster to a culture so the culture-matching template searches can find it. | `MBEquipmentRoster.cs:59-65` |
| `equipmentType` | enum `Battle` / `Civilian` / `Stealth` | No | `Battle` | On `<EquipmentSet>`. Decides when the outfit is worn. Parsed case-sensitively; a value that does not parse fires an assert and stays `Battle`. | `MBEquipmentRoster.cs:92-98` |
| `civilian` | bool | No | `Battle`, and the attribute is read only when `equipmentType` is absent | On `<EquipmentSet>`. The older spelling of `equipmentType="Civilian"`. On this code path it is accepted with no assert. | `MBEquipmentRoster.cs:99-103` |

`id` is read by the base class (`MBObjectBase`), not by this deserializer, and is required on every
`<EquipmentRoster>`.

### `<Equipment>`, one slot

<!-- engine-ref type="TaleWorlds.Core.Equipment" file="Core/TaleWorlds.Core/TaleWorlds.Core/Equipment.cs" lines="204-223" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `slot` | slot name, see below | Yes | crash: dereferenced with no null check, then `Enum.Parse` | Which of the twelve slots this item goes in. | `Equipment.cs:210-212` |
| `id` | `Item.<item_id>` | Yes | crash: dereferenced with no null check | The item. The deserializer splits on the first `.` and keeps what follows, so a bare id with no dot also resolves. An id that resolves to nothing leaves the slot **empty and silent**. | `Equipment.cs:209-219` |

`Equipment.DeserializeNode` copies `node.Attributes` into a local before indexing it, so the repo's
automated attribute gate (`tools/check_handbook_attributes.py`) cannot see these two reads and this
table is marked as a plain reference rather than a machine-checked one. The reads are at
`Equipment.cs:209-212`.

### The slot vocabulary

<!-- engine-ref type="TaleWorlds.Core.EquipmentIndex" file="Core/TaleWorlds.Core/TaleWorlds.Core/EquipmentIndex.cs" lines="3-26" -->

`slot=` is translated by `GetEquipmentIndexFromOldEquipmentIndexName` (`Equipment.cs:225-236`): the
five `Item*` names are rewritten, everything else is handed to `Enum.Parse` verbatim, which is
**case-sensitive**. `slot="body"` throws; `slot="Body"` works.

| Write this | Engine slot | Index | What fits (`Equipment.cs:445-506`) |
|---|---|---|---|
| `Item0` `Item1` `Item2` `Item3` | `Weapon0`..`Weapon3` | 0-3 | any weapon, shield, ammunition |
| `Item4` | `ExtraWeaponSlot` | 4 | only items flagged `DropOnWeaponChange` or `DropOnAnyAction`, which in practice means banners |
| `Head` | `Head` | 5 | `HeadArmor` |
| `Body` | `Body` | 6 | `BodyArmor` |
| `Leg` | `Leg` | 7 | `LegArmor` |
| `Gloves` | `Gloves` | 8 | `HandArmor` |
| `Cape` | `Cape` | 9 | `Cape` |
| `Horse` | `Horse` | 10 | `Horse` or `Animal` |
| `HorseHarness` | `HorseHarness` | 11 | `HorseHarness` |

<!-- measured: grep -ho 'slot="[A-Za-z0-9]*"' Main/_Module/ModuleData/equipmentsets/*.xml Main/_Module/ModuleData/troops/*.xml | sort | uniq -c 2026-09-05 -->
The raw enum spellings `Weapon0` and `ExtraWeaponSlot` also parse, but TAOM never uses them: across
`equipmentsets/` and `troops/` the only eleven names in use are the ones above minus `Item4`, which
appears **0** times.

### `<Flags>` attributes

<!-- engine-ref type="TaleWorlds.Core.EquipmentCategories" file="Core/TaleWorlds.Core/TaleWorlds.Core/EquipmentCategories.cs" lines="5-14" -->

Each attribute name on `<Flags>` is parsed as an `EquipmentCategories` value and OR-ed in when the
value is `true` (`MBEquipmentRoster.cs:73-84`). A misspelled flag name throws from `Enum.Parse`.
Five flags exist on v1.4.8; the thirteen listed in
[the v1.4.3 overhaul note](../migration/v1.4.x-equipment-overhaul.md) were removed and must not be
authored.

<!-- measured: grep -ho '<Flags[^/]*' Main/_Module/ModuleData/equipmentsets/*.xml | grep -o '[A-Za-z]*="true"' | sort | uniq -c 2026-09-05 -->

| Flag | Uses in `equipmentsets/` | Means |
|---|---|---|
| `IsLordTemplate` | 222 | the kit a generated lord or noble wears |
| `IsFemaleTemplate` | 155 | the female variant of whatever else is set |
| `IsChildEquipmentTemplate` | 90 | young child |
| `IsKingdomRulerTemplate` | 88 | the ruler of a kingdom |
| `IsTeenagerEquipmentTemplate` | 44 | teenager |

## Child elements

<!-- engine-table type="TaleWorlds.Core.MBEquipmentRoster" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBEquipmentRoster.cs" method="Deserialize" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<EquipmentSet>` | container | No, but a roster with none is an empty outfit | roster holds no equipment | One complete outfit. Repeatable: each one is another variant, and the engine picks among them per character. | `MBEquipmentRoster.cs:69-72` |
| `<Flags>` | attribute carrier | No | `EquipmentCategories.None` | Marks the roster as a lord / female / child / teen / ruler template so the engine's template searches can find it. | `MBEquipmentRoster.cs:73-84` |

`<Equipment>` rows sit inside `<EquipmentSet>`. `Equipment.Deserialize` walks every child of the set
without checking the element name (`Equipment.cs:196-202`), so the name `Equipment` is convention,
not a requirement, and only the `slot` and `id` attributes are read.

<!-- measured: python - (ElementTree, EquipmentSet children per EquipmentRoster across Main/_Module/ModuleData/equipmentsets/*.xml) 2026-09-05 -->
Of the 3,448 shipped rosters, 3,080 hold one set and **368** hold more than one (350 hold two, and
the largest hold fifteen).

### The same XML inline on a character, and the one attribute that changes

Inside a `<NPCCharacter>`'s `<Equipments>` block the identical `<EquipmentRoster>` shape is legal,
and so is `<EquipmentSet id="..."/>` as a *reference* to a standalone roster. Those take different
code paths, which is the whole of the "two civilian spellings" question.

<!-- engine-ref type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" lines="360-413" -->

| Where | Handled by | `equipmentType="Civilian"` | `civilian="true"` |
|---|---|---|---|
| `<EquipmentSet>` inside a standalone `<EquipmentRoster>` | `MBEquipmentRoster.InitEquipment` (`MBEquipmentRoster.cs:88-105`) | accepted | accepted, no assert |
| `<EquipmentRoster>` inline under `<Equipments>` | `MBEquipmentRoster.Init` then `InitEquipment` (`MBEquipmentRoster.cs:44-54`) | accepted | accepted, no assert |
| `<EquipmentSet id="..."/>` reference under `<Equipments>` | `BasicCharacterObject.Deserialize` (`BasicCharacterObject.cs:382-408`) | accepted | fires the engine assert "This civilian tag should not be used anymore" (`BasicCharacterObject.cs:395-398`) |

<!-- measured: python - (ElementTree tally of EquipmentRoster/EquipmentSet attributes across Main/_Module/ModuleData/{equipmentsets,troops,characters}/*.xml) 2026-09-05 -->
The shipped convention matches those rows exactly. In `equipmentsets/` there are **786**
`equipmentType="Civilian"` sets and **0** uses of `civilian=`. In `troops/` and `characters/` the
inline rosters use `civilian="true"` (129 and 1,440) while every `<EquipmentSet>` reference uses
`equipmentType="Civilian"` (560 and 1,216). Follow that: use `equipmentType` on a standalone file,
and leave the inline `civilian="true"` alone rather than "fixing" it.

## Worked example

A per-culture battle roster. Nine of the twelve slots are filled, which is normal: an unfilled slot
is simply empty.

<!-- example file="Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_lindon.xml" id="lindon_bat_template_medium_a" -->
```xml
	<EquipmentRoster id="lindon_bat_template_medium_a" culture="Culture.lindon">
		<EquipmentSet>
			<Equipment slot="Item0" id="Item.wm_rivendell_sword_a02" />
			<Equipment slot="Item1" id="Item.wm_rivendell_shield_a02" />
			<Equipment slot="Head" id="Item.rivendell_helmet_gold" />
			<Equipment slot="Body" id="Item.rivendell_body_gold_a" />
			<Equipment slot="Cape" id="Item.rivendell_pauldron_tier2_cape" />
			<Equipment slot="Gloves" id="Item.rivendell_gloves_gold" />
			<Equipment slot="Leg" id="Item.rivendell_boots_greaves3" />
			<Equipment slot="HorseHarness" id="Item.chain_horse_harness" />
			<Equipment slot="Horse" id="Item.charger" />
		</EquipmentSet>
	</EquipmentRoster>
```

What you change first:

1. **`id`** is the name other files call this roster by. Changing it silently orphans every
   `<EquipmentSet id="...">` that pointed at it, so grep before you rename.
2. **`culture`** must name a real culture id. `Culture.lindon` here, while every item in the roster
   is a `rivendell_*` one and the file's own section comment above this block reads
   `RIVENDELL LORD BATTLE EQUIPMENT`. Lindon shares Rivendell's kit; see
   [new factions](../features/new-factions-misty-mountains-lindon.md).
3. **Each `<Equipment>` row's `id`** is an Armory item. Swap the item, keep the slot.

A lord template roster is the same shape with a `<Flags>` row appended. The odd indentation is what
the generator emits.

<!-- example file="Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml" id="taom_gondor_lord_battle_male" -->
```xml
  <EquipmentRoster id="taom_gondor_lord_battle_male" culture="Culture.gondor">
    <EquipmentSet>
    			<Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
    			<Equipment slot="Item1" id="Item.sm_gd_shield_a1" />
    			<Equipment slot="Head" id="Item.sk_gd_mns_cita_helmet_heavy_a" />
    			<Equipment slot="Body" id="Item.sk_gd_mns_citadel_chest_med_a" />
    			<Equipment slot="Cape" id="Item.sk_gd_ano_pauld_inf_heavy_a" />
    			<Equipment slot="Gloves" id="Item.sk_gd_ano_gloves_a" />
    			<Equipment slot="Leg" id="Item.sk_gd_ano_grvs_inf_med_a" />
    			<Equipment slot="HorseHarness" id="Item.chain_horse_harness" />
    			<Equipment slot="Horse" id="Item.charger" />
    		</EquipmentSet>
    <Flags IsLordTemplate="true" />
  </EquipmentRoster>
```

A character-creation roster is deliberately thin, because the player's starting kit is meant to be
poor. Body and legs only.

<!-- example file="Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml" id="player_char_creation_childhood_age_gondor_retainer_m" -->
```xml
	<EquipmentRoster
		id="player_char_creation_childhood_age_gondor_retainer_m"
		culture="Culture.gondor">
		<EquipmentSet>
			<Equipment
				slot="Body"
				id="Item.gondor_noble_coat_b" />
			<Equipment
				slot="Leg"
				id="Item.sk_gd_ano_boots_a" />
		</EquipmentSet>
	</EquipmentRoster>
```

## Recipes: Add / Modify / Delete

### Add a named roster

1. Pick the culture file, `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_<culture>.xml`.
   If you are adding to a family file instead, add to the one whose id shape matches.
2. Copy an existing `<EquipmentRoster>` block in that file and give it a new `id`. Keep
   `culture="Culture.<id>"`.
3. Replace the item ids. Every id must exist in the Armory or in vanilla. If you are unsure of an
   id, grep the Armory folder for the culture before typing it.
4. If the roster is civilian, name it with `_civ` in the id **and** put `equipmentType="Civilian"`
   on the `<EquipmentSet>`. The id alone does nothing; the attribute is what the engine reads.
5. Point something at it: an `<EquipmentSet id="your_new_id"/>` line inside the
   `<Equipments>` block of an `<NPCCharacter>` in `troops/` or `characters/`. A roster nothing
   references is dead weight, not an error.

**Check:** `python tools/validate_moduledata.py --code MISSING_CIVILIAN_TYPE --code DUPLICATE_ROSTER_ID`
**Takes effect:** full game restart
**Code:** No code changes needed

### Add the eight mandatory template rosters for a culture

A culture whose clans get lords needs child, teen and lord templates or new-game child generation
returns null and the game throws. The matrix is enumerated at the top of
`tools/audit_equipment_roster_coverage.py`: `IsLordTemplate` battle and civilian, each with and
without `IsFemaleTemplate`, plus `IsChildEquipmentTemplate` and `IsTeenagerEquipmentTemplate`
civilian variants, each with and without `IsFemaleTemplate`. Four `IsKingdomRulerTemplate` rosters
are optional and every shipped culture has them anyway.

1. Add the lord, ruler and teen rosters to
   `Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml`, or regenerate with
   `python tools/generate_lord_template_equipment.py`. Read
   [the generator drift warning](../features/culture-playability-wiring.md) first: several files in
   this folder have been hand-corrected since their last generated run.
2. Add the six child rosters to
   `Main/_Module/ModuleData/equipmentsets/taom_child_equipment_templates.xml`, following the
   `child_template_<culture>_<noble|townsman|villager>_<male|female>` shape.
3. Give every one of them `culture="Culture.<id>"` and the right `<Flags>` row.
4. The civilian variants need `equipmentType="Civilian"` on their `<EquipmentSet>`.

**Check:** `python tools/audit_equipment_roster_coverage.py`
**Takes effect:** new campaign only
**Code:** No code changes needed

### Modify a roster, including fixing a civilian set

1. Edit the `<Equipment>` rows in place. Do not reformat the file: 26 of the 27 files use CRLF
   endings and a whole-file rewrite makes an unreviewable diff.
2. To fix a civilian set in a standalone file, add `equipmentType="Civilian"` to the
   `<EquipmentSet>` opening tag. Do not add `civilian="true"` there.
3. To fix one inline on a character, the file convention is the opposite: inline
   `<EquipmentRoster civilian="true">` is what the shipped data uses, and only an
   `<EquipmentSet id="..."/>` *reference* needs `equipmentType`.
4. If you removed a slot expecting it to fall back to the culture default, read the `FillFrom`
   gotcha below first.
5. For an enlistment or career roster, re-run that family's own gate as well.

**Check:** `python tools/validate_moduledata.py --code MISSING_CIVILIAN_TYPE --code DUPLICATE_ROSTER_ID` then `python tools/audit_enlistment_roster_coverage.py`
**Takes effect:** new campaign only
**Code:** No code changes needed

### Delete a roster

1. Grep for consumers first, across the whole repo and both live modules:
   `rg -n 'EquipmentSet[^>]*id="<roster_id>"'`. A reference to a roster that no longer exists is a
   null passed to `AddEquipmentRoster`, which is a hard NullReferenceException at load
   (`MBEquipmentRoster.cs:110-116`), not a silent gap.
2. Also grep the C# for the id: the character-creation, career, enlistment and education families
   are resolved by composed strings, so no XML reference exists to find.
3. Delete the `<EquipmentRoster>` block.
4. Delete or repoint every consumer you found.

**Check:** `python tools/validate_moduledata.py` then `dotnet test TAOM.Tests --filter CareerCultureCoverage -p:DisableModuleCopy=true -p:ModuleId=`
**Takes effect:** full game restart
**Code:** No code changes needed

## Gotchas: what fails silently and what crashes

- **A misspelled item id makes a naked troop, not an error.** `GetObject<ItemObject>` returns null,
  `IsItemFitsToSlot(slot, null)` returns `true` by design, and the slot is filled with an empty
  element. Nothing is logged. `Equipment.cs:209-219` and `Equipment.cs:445-450`.
- **An item in the wrong slot also leaves the slot empty**, after a failed assert a shipping build
  does not show you. `Equipment.cs:216-221`, legality at `Equipment.cs:445-506`.
- **A missing roster reference is a crash, not a gap.** `<EquipmentSet id="typo"/>` on a character
  resolves to null and `AddEquipmentRoster` dereferences it immediately.
  `MBEquipmentRoster.cs:110-116`, called from `BasicCharacterObject.cs:407`.
- **`equipmentType` is case-sensitive and fails soft.** `Enum.TryParse` is called with no
  ignore-case flag, so `equipmentType="civilian"` fires an assert and silently stays `Battle`.
  `MBEquipmentRoster.cs:92-98`.
- **`slot` is case-sensitive and fails hard.** Anything the `Item0`..`Item4` rewrite does not match
  goes to `Enum.Parse` verbatim, which throws on `body` or `HEAD`. `Equipment.cs:225-236`.
- **Spell the inline element `EquipmentRoster` exactly.** `MBEquipmentRoster.Init` asserts on any
  other name, and `BasicCharacterObject` accepts the lowercase `equipmentRoster` spelling only to
  hand it straight to that assert. `MBEquipmentRoster.cs:44-54`, `BasicCharacterObject.cs:372-378`.
- **`FillFrom` copies all twelve slots, including the empty ones.** A slot your roster omits
  overwrites the target's slot with nothing; it does not inherit. `Equipment.cs:184-194`.
  [career-system.md](../features/career-system.md) states the opposite at line 354 and is wrong;
  the header of `tools/wire_career_starter_armor.py` states it correctly.
- **A character's first battle set is not the first one in the file.** Every character's sets are
  re-sorted at the end of deserialization so battle sets come before civilian and stealth ones.
  `MBEquipmentRoster.cs:138-141`, called from `BasicCharacterObject.cs:526`.
- **A character with several rosters is mixed per slot at spawn, not picked whole.** The randomiser
  draws three independent roster numbers: slots 0-1, slots 2-3, and slots 4-11. The sword can come
  from one variant and the whole suit of armour from another, so design each variant to look right
  against every other variant's armour. `Equipment.cs:549-595`.
- **`MISSING_CIVILIAN_TYPE` is a warning and only looks at some ids.** The rule fires only when a
  roster id contains `_civ` or `child_template_`, and it deliberately skips every
  `child_education` id. A civilian roster named some other way is never checked, and the run still
  exits 0. `tools/taom_schema.py:385-393` and `tools/schemas/taom_equipmentsets.json`.
- **A backup copy that keeps a `.xml` extension breaks the validator.** The schema globs
  `equipmentsets/*.xml`, so a copy trips `DUPLICATE_ROSTER_ID` on every roster in it. The five
  shipped sidecars avoid this by ending `.xml.bak-enlist-<timestamp>`.
- **Do not use the C# enum spellings.** `Weapon0` and friends parse, but nothing in TAOM writes
  them, and the mixed vocabulary is how `slot="body"` mistakes get made. Write `Item0`..`Item3` and
  the capitalised armour names.
- **The [xml-data rule](../../.claude/rules/xml-data.md) cites a memory file that does not exist.**
  Its `feedback_equipmenttype_civilian_required.md` reference has nothing behind it in the repo or
  the project memory store. The rule's own text is correct; only the citation is dangling.

### Not answered anywhere in TAOM

- **Does `Item4` work from a roster?** The engine maps it to `ExtraWeaponSlot` and it is the only
  slot a banner fits (`Equipment.cs:225-236`, `Equipment.cs:445-506`), but TAOM ships **0** uses of
  it, no doc describes one, and `tools/audit_enlistment_roster_coverage.py` bans it from its slot
  allowlist. Nobody has tested it here.
- **`equipmentType="Stealth"` is legal and unused.** The enum has it (`Equipment.cs:14-20`) and TAOM
  ships **0** occurrences, so no TAOM evidence exists for when a stealth set is worn.
- **Which slot an empty `id=""` override actually clears** is documented in
  [career-system.md](../features/career-system.md) at line 361 but ships nowhere: there are **0**
  `id=""` rows in `equipmentsets/`. The engine side is `Equipment.cs:445-450`; the behaviour has
  not been exercised in shipped data.

## Numbers in this chapter

Every number below was measured on 2026-09-05. Run the commands from the repository root; the
ElementTree scans were short throwaway Python scripts over the paths named in each row.

| Number | Command |
|---|---|
| 27 roster files, 27 registrations | `ls Main/_Module/ModuleData/equipmentsets/*.xml \| wc -l` and `grep -c 'XmlName id="EquipmentRosters"' Main/_Module/SubModule.xml` |
| 5 backup sidecars | `ls Main/_Module/ModuleData/equipmentsets/ \| grep -c bak-enlist` |
| 3,448 `<EquipmentRoster>` entries | `grep -ho '<EquipmentRoster\b' Main/_Module/ModuleData/equipmentsets/*.xml \| wc -l` |
| 3,448 of 3,448 carry `culture=` (0 missing) | ElementTree scan of `equipmentsets/*.xml` counting `EquipmentRoster` elements without a `culture` attribute |
| 3,998 `<EquipmentSet>` children; 3,080 rosters with one, 368 with more | ElementTree scan counting `EquipmentSet` children per `EquipmentRoster` |
| 786 `equipmentType="Civilian"`, 0 `civilian=` in `equipmentsets/` | ElementTree attribute tally over `equipmentsets/*.xml` |
| 129 and 1,440 inline `civilian="true"`; 560 and 1,216 `equipmentType="Civilian"` references | same tally over `troops/*.xml` and `characters/*.xml` |
| 11 slot names in use, `Item4` 0 times | `grep -ho 'slot="[A-Za-z0-9]*"' Main/_Module/ModuleData/equipmentsets/*.xml Main/_Module/ModuleData/troops/*.xml \| sort \| uniq -c` |
| Flags: 222 / 155 / 90 / 88 / 44 | `grep -ho '<Flags[^/]*' Main/_Module/ModuleData/equipmentsets/*.xml \| grep -o '[A-Za-z]*="true"' \| sort \| uniq -c` |
| Per-family roster and culture counts (the table above) | ElementTree scan per file counting `EquipmentRoster` entries and distinct `culture=` values |
| 3,412 `<EquipmentSet id=>` references outside `equipmentsets/`, 0 unresolved | regex scan of `Main/_Module/ModuleData/**/*.xml` against the roster ids defined in TAOM plus the loaded vanilla and dependency modules |
| 2,867 references whose roster file is registered later in `SubModule.xml` | scratch script pairing each reference with the `XmlName` line that registers the defining file |
| 26 of 27 files CRLF, 0 with a BOM | byte scan of `equipmentsets/*.xml` for `\r\n` and the UTF-8 BOM |
| 0 `equipmentType="Stealth"`, 0 `id=""` | `grep -rho 'equipmentType="Stealth"' --include=*.xml Main/_Module/ModuleData/` and `grep -ho 'id=""' Main/_Module/ModuleData/equipmentsets/*.xml` |
| 0 XML references for five id prefixes, 420 for `npc_companion_equipment_template` | `grep -rho '<EquipmentSet[^>]*id="<prefix>[^"]*"' --include=*.xml Main/_Module/ModuleData/` per prefix, excluding `equipmentsets/` |
| Validator PASS; coverage audit 16 cultures, 0 misses; enlistment audit 252/320 cells, 16/16 defaults, PASS | `python tools/validate_moduledata.py --code MISSING_CIVILIAN_TYPE --code DUPLICATE_ROSTER_ID`, `python tools/audit_equipment_roster_coverage.py`, `python tools/audit_enlistment_roster_coverage.py` |

## Read next

- Who wears these: [Troops](troops.md), [Lords and heroes](lords-and-heroes.md),
  [Wanderers and named companions](wanderers-and-named-companions.md), [Cultures](cultures.md)
- What goes in the slots: [Armour items](items-armor.md),
  [Weapons and crafting](items-weapons-and-crafting.md),
  [Mounts and harness](items-mounts-and-harness.md)
- Registration and gates: [SubModule and registration](submodule-and-registration.md),
  [Validation and testing](validation-and-testing.md), [tools README](../../tools/README.md)
- The dev docs this chapter distilled: [xml-data rule](../../.claude/rules/xml-data.md),
  [v1.4.3 equipment overhaul](../migration/v1.4.x-equipment-overhaul.md),
  [culture playability wiring](../features/culture-playability-wiring.md),
  [enlistment](../features/enlistment.md), [career system](../features/career-system.md),
  [no-mount cultures](../features/no-mount-cultures.md),
  [new factions](../features/new-factions-misty-mountains-lindon.md),
  [ModuleData validation](../features/moduledata-validation.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/items-mounts-and-harness.md](./items-mounts-and-harness.md)
- [docs/modding/items-shields.md](./items-shields.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troops.md](./troops.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
