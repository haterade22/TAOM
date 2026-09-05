# Skill sets

## What this file is

A skill set is a named block of skill numbers that a character points at instead of writing its own. `Main/_Module/ModuleData/taom_lord_skill_sets.xml` holds 145 of them for lords and `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` holds 170 for wanderers and town notables. <!-- measured: python regex count of '<SkillSet id="' in both files 2026-09-05 --> The schema is the smallest in the game: an `id`, then a flat list of `<skill id="..." value="..."/>` lines, and nothing else.

## Where it lives and how it is registered

<!-- excerpt file="Main/_Module/SubModule.xml" -->

| File | Registered at | Root element | Per entry | Engine class |
|---|---|---|---|---|
| `Main/_Module/ModuleData/taom_lord_skill_sets.xml` | `SubModule.xml:752` `<XmlName id="SkillSets" path="taom_lord_skill_sets"/>` | `<SkillSets>` | `<SkillSet>` | `TaleWorlds.Core.MBCharacterSkills` |
| `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` | `SubModule.xml:762` `<XmlName id="SkillSets" path="taom_wanderer_skill_sets"/>` | `<SkillSets>` | `<SkillSet>` | `TaleWorlds.Core.MBCharacterSkills` |
| `SandBoxCore/ModuleData/sandboxcore_skill_sets.xml` | `SandBoxCore/SubModule.xml:51` | `<SkillSets>` | `<SkillSet>` | same |
| `SandBox/ModuleData/sandbox_skill_sets.xml` | `SandBox/SubModule.xml:178` | `<SkillSets>` | `<SkillSet>` | same |
| `Native/ModuleData/native_skill_sets.xml` | `Native/SubModule.xml:92` | `<SkillSets>` | none, the file is an empty `<SkillSets></SkillSets>` | same |

Those last three files live in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. You will not normally touch them: TAOM adds its own sets rather than editing vanilla's.

**Position in `SubModule.xml` does not matter.** The engine registers the type at `Game.cs:321` with the singular element name `SkillSet` and the plural list name `SkillSets`, then loads every registered `SkillSets` file inside `Game.LoadBasicFiles` at `Game.cs:445`. Character files load much later, at `SandBoxManager.cs:362`. Every skill set therefore exists before any character can point at one, and you cannot get the order wrong.

**`taom_lord_skill_sets.xml` is generated. Do not hand-edit it.** Line 3 of the file says so, and the owning tool is `tools/apply_culture_skills_traits.py` ([tools README](../../tools/README.md) line 136). Every run rewrites the whole file from `BASE_ARCHETYPES` plus the per-culture canonical tables, so a hand edit survives only until the next regen. `taom_wanderer_skill_sets.xml` carries no such banner; two tools name it, `tools/extract_wanderers.py` and `tools/generate_batch2_wanderers.py`, and the second one appends rather than regenerating (its docstring, line 3). TAOM has no single index mapping every generated data file to the tool that owns it. The closest thing is [tools README](../../tools/README.md), which lists the tool per row but is organised by tool, not by output file.

## Attributes

### The `<SkillSet>` element

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none | The handle everything else points at. A character writes `skill_template="SkillSet.<id>"` to inherit the whole block. Missing it throws a `NullReferenceException` inside the parse, and the schema marks it `use="required"` (`XmlSchemas/SkillSets.xsd:22`) | `MBObjectBase.cs:61` |

`<SkillSet>` has no other attributes. There is no name, no description, no culture, no tier: `MBCharacterSkills` is a 27-line class whose only state is the skill list (`MBCharacterSkills.cs:8`).

### The `<skill>` line

Both attributes are read by one shared generic reader, `PropertyOwner<SkillObject>.Deserialize`, which `MBCharacterSkills.Deserialize` hands the node to (`MBCharacterSkills.cs:25`).

<!-- engine-ref type="TaleWorlds.Core.PropertyOwner&lt;SkillObject&gt;" file="Core/TaleWorlds.Core/TaleWorlds.Core/PropertyOwner.cs" lines="71-88" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | one of the 18 skill ids, case-sensitive | yes | none, missing throws | Picks the skill. An id that is not one of the 18 resolves to null and **the whole line is dropped in silence**, so a typo costs you the points with no error anywhere | `PropertyOwner.cs:78, 80-85` |
| `value` | int | yes | none, missing throws | The number. Parsed with `Convert.ToInt32`, so `30.5` or `high` throws a `FormatException`. `value="0"` is not "set to zero", it deletes the entry (`PropertyOwner.cs:30-40`), and an absent skill already reads back as 0 (`PropertyOwner.cs:42-48`). Negative values are stored as written | `PropertyOwner.cs:79, 83` |

Nothing clamps the number at load. TAOM lord values run from 20 to 417 <!-- measured: python min/max over every value= in taom_lord_skill_sets.xml 2026-09-05 --> against vanilla troop templates in the 10 to 30 band. `DefaultCharacterDevelopmentModel.cs:33` declares `MaxSkillLevels = 1024`, but that constant appears nowhere else in the v1.4.8 dump, so treat it as unrelated to loading, not as a cap. <!-- measured: rg -rn --glob '*.cs' -c "MaxSkillLevels" over the v1.4.8 dump, one file one hit 2026-09-05 -->

### The 18 skill ids

<!-- engine-ref type="TaleWorlds.Core.DefaultSkills" file="Core/TaleWorlds.Core/TaleWorlds.Core/DefaultSkills.cs" lines="115-132" -->

| Governing attribute | Skill ids |
|---|---|
| Vigor | `OneHanded`, `TwoHanded`, `Polearm` |
| Control | `Bow`, `Crossbow`, `Throwing` |
| Endurance | `Riding`, `Athletics`, `Crafting` |
| Cunning | `Tactics`, `Scouting`, `Roguery` |
| Social | `Charm`, `Trade`, `Leadership` |
| Intelligence | `Steward`, `Medicine`, `Engineering` |

**The one trap in the list: the id is `Crafting`, not `Smithing`.** The player-facing label is Smithing (`DefaultSkills.cs:96` initialises it with the text key `{=smithingskill}Smithing`), and writing `id="Smithing"` gives you a silently dropped line. Attribute grouping comes from `DefaultSkills.cs:88-105`.

### What a number buys

<!-- engine-ref type="TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultPerks.cs" lines="809-813" -->

| Number | What changes |
|---|---|
| Every 25 points up to 300 | One perk tier. The ladder is the literal array `25, 50, 75, 100, 125, 150, 175, 200, 225, 250, 275, 300` at `DefaultPerks.cs:809-813`, so 12 tiers and nothing above 300 |
| `Athletics` up to 300 | Step size, `min(0.8 + 0.2 * Athletics * 0.00333333, 1)` at `BasicCharacterObject.cs:242-244`. It saturates exactly at 300 |
| Any skill up to 300, for equipped gear | The AI's estimate of the character's power weights each item by `0.3 + skill / 300 * 0.7` (`CharacterObject.cs:640`). Above 300 the weight stops moving |
| Steward, on a party leader | Party size. TAOM records it as +0.25 per point ([lord skills authoring](../ai-includes/lord-skills-authoring.md) line 240) |
| Nothing at all | Tier. `DefaultCharacterStatsModel.cs:18-25` computes a troop's tier as `clamp(ceil((level - 5) / 5), 0, 6)` from `level` alone, and a hero is always tier 0. Raising skills never raises tier |

Three practical consequences. Values above 300 are flavour in the formulas above: TAOM ships 84 such lines in the lord file and 33 in the wanderer file. <!-- measured: python count of value>300 over both skill set files 2026-09-05 --> A hero does not get the declared numbers exactly, because `DefaultHeroCreationModel.cs:361-378` adds `MBRandom.RandomInt(5, 10)` to every non-zero skill (`:462-466`), and returns an empty list for a hero under `HeroComesOfAge`, so a child lord gets nothing from the set at all. And `SkillFactor`, despite the name, reads `level` and never a skill: `min(level, 32) / 32` at `BasicCharacterObject.cs:74` with the 32 declared at `:14`, consumed by `Agent.UpdateLocalPositionError` (`Agent.cs:5083-5086`) to decide how sloppily an agent holds its slot in formation.

### The two attributes that point at a set

<!-- engine-ref type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" lines="337-345" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `skill_template` | ref, written `SkillSet.<id>` | no | a fresh empty set named after the character, so every skill reads 0 | Attaches the shared block. **The dot is mandatory**: `skill_template="taom_lady_skills"` throws `MBInvalidReferenceException` (`MBObjectManager.cs:1525-1528`), `skill_template="SkillSet.taom_lady_skills"` is correct. The prefix is the singular `SkillSet` | `BasicCharacterObject.cs:337` |
| `voice` | bare trait id, no prefix | no | `softspoken` | Dialogue persona, one of `curt`, `ironic`, `earnest`, `softspoken`. It resolves as a trait, which is why it lives beside this system. An unknown string falls back to softspoken | `CharacterObject.cs:572-576` |

A well-formed reference to an id that does not exist does **not** crash. `RegisterType` defaults to auto-creating instances (`MBObjectManager.cs:376`), so the engine invents an empty placeholder (`MBObjectManager.cs:724-729`) and the character walks around with every skill at 0.

### Traits, the sibling system

Traits are read by the same `PropertyOwner` reader from a `<Traits>` block on the character, never from a skill set, and there is no `trait_template` equivalent.

<!-- engine-ref type="TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs" lines="129-188" -->

| Range | Visible | Trait ids |
|---|---|---|
| -2 to +2 | yes | `Mercy`, `Valor`, `Honor`, `Generosity`, `Calculating` |
| -2 to +2 | yes, but these are the personas the `voice` attribute points at, and they are the only lowercase ids | `curt`, `ironic`, `earnest`, `softspoken` |
| 0 to 20 | no | `Frequency`, `Commander`, `Surgeon`, `Tracking`, `Blacksmith`, `SergeantCommandSkills`, `EngineerSkills`, `RogueSkills`, `ScoutSkills`, `Trader`, `Thug`, `Smuggler`, `NavalSoldier` |
| 0 to 20 | yes | `Egalitarian`, `Oligarchic`, `Authoritarian` |

Two things measured while writing this chapter. **The declared range is not enforced at load**: `PropertyOwner.Deserialize` never consults `TraitObject.MinValue`/`MaxValue`, so `<Trait id="Honor" value="50"/>` is stored as 50. Whether anything downstream clamps it was not determined. **Six ids TAOM writes are not registered ids at all**: `ArcherFightingSkills`, `BalancedFightingSkills`, `CavalryFightingSkills`, `KnightFightingSkills`, `Manager` and `Politician` appear nowhere in the v1.4.8 dump, so those 111 lines of 13,788 are dropped by the same silent path a misspelt skill takes. <!-- measured: python count of <Trait id= across characters/*.xml + lords.xslt, differenced against the 25 ids in DefaultTraits.RegisterAll 2026-09-05 --> Vanilla ships the same dead ids in `SandBox/ModuleData/lords.xml`, so they are inherited, not a TAOM mistake. `Commander` is real; the other three names that TAOM docs group with it are not.

## Child elements

<!-- engine-ref type="TaleWorlds.Core.MBCharacterSkills" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBCharacterSkills.cs" lines="15-26" -->

| Element | Where | Required | What it does | Read at (file:line) |
|---|---|---|---|---|
| `<SkillSets>` | file root | yes | The container. If the root element is not spelled exactly `SkillSets` the file is skipped without a word (`MBObjectManager.cs:1383-1386`). An empty one is legal, which is what Native ships | `MBObjectManager.cs:1372` |
| `<SkillSet>` | inside `<SkillSets>` | at least one to be useful | One named block. Merged by `id` across modules, not replaced | `MBObjectManager.cs:846-874` |
| `<skill>` | inside `<SkillSet>` | no, repeatable, order does not matter | One number. The element **name is never checked**: the reader walks every non-comment child, so `<skill>`, `<Skill>` and `<anything>` all work. Only the schema prefers lowercase (`XmlSchemas/SkillSets.xsd:11`). Two lines with the same id inside one set: the last wins | `PropertyOwner.cs:73-75` |
| `<skills>` or `<Skills>` | inside `<NPCCharacter>` | no | The inline alternative to a shared set. Both spellings are accepted. **It is only read when `skill_template` did not resolve** (`BasicCharacterObject.cs:355`), so a character carrying both silently runs on the template | `BasicCharacterObject.cs:353-359` |
| `<Traits>` | inside `<NPCCharacter>` | no | The trait block. **Case-sensitive, and there is no lowercase alias**: `<traits>` is ignored without complaint | `CharacterObject.cs:551-553` |

Merging is worth seeing once, because vanilla itself relies on it. `SandBoxCore/ModuleData/sandboxcore_skill_sets.xml` declares `infantry_heavyinfantry_level1_template_skills` with five skills, and `SandBox/ModuleData/sandbox_skill_sets.xml` declares the same id again with eight. The engine folds the second into the first: the union of the lines, with the later module's value winning for any skill both name (`MBObjectManager.cs:846-874`). To wipe the inherited lines instead of merging them, put `_replaceWhileMerging="true"` on your `<SkillSet>` (`MBObjectManager.cs:804-808`, `:829-831`).

## Worked example

<!-- example file="Main/_Module/ModuleData/taom_lord_skill_sets.xml" id="taom_black_numenorean_skills" -->

```xml
  <SkillSet id="taom_black_numenorean_skills">
    <skill id="OneHanded" value="255" />
    <skill id="TwoHanded" value="210" />
    <skill id="Polearm" value="240" />
    <skill id="Bow" value="210" />
    <skill id="Crossbow" value="170" />
    <skill id="Throwing" value="180" />
    <skill id="Riding" value="240" />
    <skill id="Athletics" value="255" />
    <skill id="Crafting" value="200" />
    <skill id="Scouting" value="240" />
    <skill id="Tactics" value="260" />
    <skill id="Roguery" value="240" />
    <skill id="Charm" value="270" />
    <skill id="Leadership" value="260" />
    <skill id="Trade" value="240" />
    <skill id="Steward" value="255" />
    <skill id="Medicine" value="190" />
    <skill id="Engineering" value="240" />
  </SkillSet>
```

The three things a reader changes first:

1. **`id`**, only when adding a new set. Changing an existing id orphans every character pointing at it, and because a dangling reference auto-creates an empty placeholder they all drop to zero skills without an error.
2. **`Leadership`, `Steward`, `Tactics`**, the command axis. These are what a lord's army and autoresolve run on, and they are the numbers TAOM's balance passes actually move ([lord skills authoring](../ai-includes/lord-skills-authoring.md) lines 360-373).
3. **The combat skill matching the lord's role**, here `OneHanded` at 255. TAOM's canonical hero specialty band is 270 to 330, so this set is deliberately a notch below a named legend.

Sixteen characters point at this one set: 6 in `Main/_Module/ModuleData/characters/lords.xml` and 10 in `Main/_Module/ModuleData/lords.xslt`. <!-- measured: rg -c 'taom_black_numenorean_skills' over both files 2026-09-05 --> That is the point of a set, and it is also why editing one is never a local change.

## Recipes: Add / Modify / Delete

### Add

Add a canonical set for one named character through the generator's override table. Never by typing into the XML.

1. Confirm the character exists: `rg -n 'id="lord_R3_2"' Main/_Module/ModuleData/characters/lords.xml Main/_Module/ModuleData/lords.xslt`. If it does not, create the `<NPCCharacter>` first, per [Lords and heroes](lords-and-heroes.md).
2. Commit or stash first. The generator rewrites the whole of `taom_lord_skill_sets.xml` plus `lords.xml` and `lords.xslt`, and a dirty tree makes the diff unreadable. [tools README](../../tools/README.md) line 136 states the pre-flight: a regen on a clean tree must diff empty before any `--apply`.
3. Open `tools/apply_culture_skills_traits.py`, find `CULTURES['<culture>']['canonical']`, and add an entry keyed by the character id with a `skills=dict(...)` of all 18 skills and a `traits=dict(...)`. An entry may instead carry `archetype='<name>'` alone, or an archetype plus a partial override.
4. Dry run: `python tools/apply_culture_skills_traits.py --all-cultures`. Read what it says it would change.
5. Apply: `python tools/apply_culture_skills_traits.py --all-cultures --apply`. It writes a new set named `taom_canonical_<id>_skills` and points the character's `skill_template` at it.

Check: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/taom_lord_skill_sets.xml'); print('well-formed')"`
Takes effect: new campaign only. The XML reloads on a full game restart, but a hero created in an existing save keeps the numbers it was born with ([lord skills authoring](../ai-includes/lord-skills-authoring.md) line 63).
Code: No code changes needed.

### Modify

#### Point a character at a different set

1. Find who points where: `rg -n 'skill_template="SkillSet.taom_lord_skills"' Main/_Module/ModuleData/`.
2. Change the attribute value on the `<NPCCharacter>` in `Main/_Module/ModuleData/characters/lords.xml`, or the matching `<xsl:attribute>` in `Main/_Module/ModuleData/lords.xslt`. Keep the `SkillSet.` prefix.
3. If the character exists in both files, `characters/lords.xml` is the one the engine uses: it loads second and last-loaded wins among additive sources with the same id ([lord skills](../features/lord-skills.md) line 20).
4. For a culture-wide swap onto a variant set, use `python tools/repoint_evil_lord_skillsets.py` (dry run by default, `--apply` to write) rather than re-running the generator per culture. The live XML carries hand-tuned assignments the generator cannot reproduce ([tools README](../../tools/README.md) line 137).

Check: `rg -c 'skill_template="SkillSet\.' Main/_Module/ModuleData/characters/lords.xml` before and after, and confirm the count is unchanged.
Takes effect: new campaign only.
Code: No code changes needed.

#### Retune the numbers

1. For a lord set, edit `BASE_ARCHETYPES` (an archetype, which moves every character resolving to it) or the culture's `canonical` entry (one character) in `tools/apply_culture_skills_traits.py`, then re-run it with `--apply`. Editing the XML directly is undone by the next regen.
2. Never edit a shared base archetype in place to fix one culture. Fork a variant, alias it in that culture's `archetype_alias`, and repoint. Six cultures already carry an alias map ([lord skills authoring](../ai-includes/lord-skills-authoring.md) lines 346-351).
3. For a wanderer set, edit `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` by hand. It has no generator banner.
4. For a troop, this chapter does not apply. TAOM troops carry no `skill_template` at all: all 857 `<NPCCharacter>` blocks under `Main/_Module/ModuleData/troops/` use an inline `<skills>` block. <!-- measured: python count of '<NPCCharacter' and 'skill_template' across Main/_Module/ModuleData/troops/*.xml 2026-09-05 --> Retune those through `tools/rebalance_troops.py` and see [Troops](troops.md).

Check: `python tools/analyze_lord_balance.py --culture <key> --stdout`, which resolves each lord through its `skill_template` and reports drift between the set and the inline block.
Takes effect: new campaign only.
Code: No code changes needed.

### Delete

1. Grep for users first, always: `rg -n 'skill_template="SkillSet.<id>"' Main/_Module/ModuleData/`. A set with no users is safe to drop; one with users is not, because deleting it does not error, it silently zeroes every character that pointed at it.
2. Repoint each user to a surviving set before removing anything.
3. If the set is a lord set, delete the archetype or canonical entry in `tools/apply_culture_skills_traits.py` and regenerate. Deleting the `<SkillSet>` block from the XML alone is undone by the next run.
4. For a wanderer set, delete the `<SkillSet>` block from `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` directly.

Check: `rg -c 'skill_template="SkillSet.<id>"' Main/_Module/ModuleData/` returns nothing, then re-run the well-formedness parse from the Add recipe.
Takes effect: new campaign only.
Code: No code changes needed.

## Gotchas: what fails silently and what crashes

- **One bad `value=` kills every remaining set in the file, with no log line.** `MBObjectManager.LoadXML` wraps the whole document parse in a bare `try { ... } catch (Exception) { }`, so a `FormatException` from `Convert.ToInt32` aborts the load of everything below it and the game carries on. The symptom is lords further down the file having all-zero skills. `MBObjectManager.cs:790-796`, `PropertyOwner.cs:83`.
- **A misspelt skill id is dropped without a word.** `GetObject<SkillObject>` returns null, the line is skipped, the skill stays 0. `PropertyOwner.cs:80-85`.
- **A `skill_template` pointing at an id that does not exist is not an error either.** The engine auto-creates an empty placeholder and the character ends up at zero across the board. `MBObjectManager.cs:724-729`. TAOM has no validator for this: nothing in `tools/taom_schema.py` opens the skill set files, so `python tools/validate_moduledata.py` cannot catch a dangling reference. The repo is clean today, with 271 distinct `skill_template` values across 2,471 attributes and zero unresolvable. <!-- measured: python resolving every skill_template in Main/_Module/ModuleData against the ids in both TAOM set files plus sandbox and sandboxcore 2026-09-05 -->
- **A `skill_template` without a dot is a hard crash**, unlike everything else here. `MBObjectManager.cs:1519-1534` throws `MBInvalidReferenceException` when the value has no `.` separator.
- **Declaring `skill_template` and an inline `<skills>` block means the inline block is discarded.** No merge, no warning. Until 2026-08-31 that described 44 TAOM militia troops, authored at 850 total skill points and delivered at 215 while every TAOM tool reported the authored figure ([troop skill balance](../features/troop-skill-balance.md) lines 145-152). `validate_moduledata.py` now errors on it as `SKILL_TEMPLATE_SHADOWS_SKILLS` and `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs:257` pins it in the C# suite. An **empty** `<skills>` block beside a template is the legitimate shape and is left alone.
- **`value="0"` does not mean zero, it means delete the line.** You cannot use it to override an inherited value back down to nothing. `PropertyOwner.cs:30-40`.
- **`<traits>` in lowercase is ignored.** Only the exact spelling `Traits` is matched. `CharacterObject.cs:551`.
- **Hand edits to `taom_lord_skill_sets.xml` vanish on the next generator run**, and the run is routine during any balance pass. Edit `tools/apply_culture_skills_traits.py` instead. File header line 3.
- **What TAOM never established:** whether trait values outside a trait's declared range are clamped by any consumer after load; what the hidden `Tracking` trait does (`DefaultTraits.cs:142` registers the id but the dump has no accessor for it); and whether the schema validation pass rejects a capitalised `<Skill>` even though the reader provably does not care. `GetMergedXmlForManaged` is called with `skipValidation: false` (`MBObjectManager.cs:789`), so the pass runs, but nobody has read the routine to see whether a schema violation aborts the load or only logs. Start from `DefaultTraits.cs:164-188` and `MBObjectManager.cs:789` if you need an answer.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 145 sets in `taom_lord_skill_sets.xml`, 170 in `taom_wanderer_skill_sets.xml` | python `re.findall(r'<SkillSet\s+id="([^"]+)"')` over each file | 2026-09-05 |
| 2,610 `<skill>` lines in the lord file, values 20 to 417 | python min/max over every `value=` in `taom_lord_skill_sets.xml` | 2026-09-05 |
| 84 lord lines and 33 wanderer lines above 300 | python count of `value > 300` over both files | 2026-09-05 |
| 271 distinct `skill_template` values, 2,471 attributes, 0 unresolvable, 0 missing the dot | python resolving each `skill_template` in `Main/_Module/ModuleData` against the ids defined in both TAOM set files plus `sandbox_skill_sets.xml` and `sandboxcore_skill_sets.xml` | 2026-09-05 |
| 857 `<NPCCharacter>` blocks and 857 inline `<skills>` blocks under `troops/`, 0 `skill_template` | python count over `Main/_Module/ModuleData/troops/*.xml` | 2026-09-05 |
| 16 characters point at `taom_black_numenorean_skills`, 6 plus 10 | `rg -c 'taom_black_numenorean_skills'` over `characters/lords.xml` and `lords.xslt` | 2026-09-05 |
| 111 of 13,788 `<Trait>` lines use an id the engine never registers | python count of `<Trait id=` across `characters/*.xml` and `lords.xslt`, differenced against the 25 ids created in `DefaultTraits.RegisterAll` | 2026-09-05 |
| 74 archetypes in `BASE_ARCHETYPES` (tool lines 29-330), 74 archetype sets and 71 canonical sets in the generated file | python key count inside the `BASE_ARCHETYPES` literal, and a prefix split of the generated ids | 2026-09-05 |
| `MaxSkillLevels` appears once in the v1.4.8 dump, at its own declaration | `rg -rn --glob '*.cs' -c "MaxSkillLevels"` over the decompile root | 2026-09-05 |

Two numbers this chapter deliberately does not print. The per-level, per-formation troop baselines live in `tools/rebalance_troops.py`: `INFANTRY_BASELINES` at lines 63-75, `RANGED_BASELINES` at 77-88, `CAVALRY_BASELINES` at 90-101, `HORSEARCHER_BASELINES` at 103-113, grouped by `GROUP_BASELINES` at 115-120 with `CULTURAL_MODS` at 126. Infantry, ranged and cavalry are keyed to levels 1 through 51 in steps of 5; horse archers stop at 46. <!-- measured: python listing the integer keys of each baseline dict in tools/rebalance_troops.py 2026-09-05 --> Those tables drive troop `<skills>` blocks, not skill sets, and they belong to [Troops](troops.md) and [Balance levers](balance-levers.md).

## Read next

- [lord skills](../features/lord-skills.md)
- [lord skills authoring](../ai-includes/lord-skills-authoring.md)
- [lord perk review](../features/lord-perk-review.md)
- [troop skill balance](../features/troop-skill-balance.md)
- [tools README](../../tools/README.md)
- [Troops](troops.md)
- [Lords and heroes](lords-and-heroes.md)
- [Wanderers and named companions](wanderers-and-named-companions.md)
- [Notables and townsfolk](npcs-notables-and-townsfolk.md)
- [Editing safely](editing-safely.md)
- [Validation and testing](validation-and-testing.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)

<!-- backlinks-end -->
