# Recipe: add a race or a creature

## What this chapter is

A race (dwarf, orc, elf) and a creature (war ram, spider, mumakil) are the same shape of problem: art plus a handful of XML rows, most of them in a module the repo does not track. This chapter names every row, walks the dwarf chain end to end, and makes you pick between the two build paths before you spend a week on the wrong one. Everything below is a router; the full procedure for each surface lives in the dev docs under [Read next](#read-next).

## A race is five data surfaces

Four of the five live in `LOTRLOME_Armory/ModuleData/`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. Mirror every Armory edit into [`docs/reference/lotrlome-armory-snapshot/`](../reference/lotrlome-armory-snapshot/README.md) in the same sitting.

| Surface | File | What it decides | Missing means |
|---|---|---|---|
| **Skin** | `LOTRLOME_Armory/ModuleData/skins.xml` | skeleton, body and face meshes, hair / beard / tattoo tag pools, `<voice_types>`, per gender and maturity | the race id does not exist, so `race="x"` on a troop throws |
| **Monster** | `LOTRLOME_Armory/ModuleData/monsters.xml` | weight, hit points, capsules, bone map, which action sets the race uses | the race has no body in a mission |
| **Action sets** | `LOTRLOME_Armory/ModuleData/action_sets.xml` | which clip plays for each action, per skeleton | T-pose, bind pose, or a crash on an unbound action |
| **Body property** | [`Main/_Module/ModuleData/TAOM_bodyproperties.xml`](../../Main/_Module/ModuleData/TAOM_bodyproperties.xml) | the build and face range a troop rolls inside | the troop falls back to whatever the `face_key_template` names |
| **Ageing** | [`Main/_Module/ModuleData/raceage/race_age_config.json`](../../Main/_Module/ModuleData/raceage/race_age_config.json) | max age, `comesOfAge`, `becomeOld`, fertility, `immortal` | the race ages like a human |

The consumer is one attribute: `race="dwarf"` on an `NPCCharacter`. Everything else is inherited from the race id. See [Troops](troops.md) and [Body properties](body-properties.md).

**The race id itself is an integer, assigned by merge order.** `BasicCharacterObject.Deserialize` sets `Race = 0` then calls `FaceGen.GetRaceOrDefault(value)` (`BasicCharacterObject.cs:323-327`), and that method is a raw dictionary index (`FaceGen.cs:115-118`), so a race name with a typo throws `KeyNotFoundException` at load rather than falling back to human. The dictionary is built from the native side, `MBAPI.IMBFaceGen.GetRaceIds()` split on `;` (`FaceGen.cs:20`), in skins.xml merge order. Native contributes `human` first, so human is always 0. The authoring comment above the last race block in the live file says it outright: `race ints are skins.xml merge-order indices (issue #321)`. **Append a new `<race>` at the END of the file.** Inserting one in the middle renumbers every race after it.

### The five Monster variants

The engine asks for a race's Monster by string concatenation, `raceName + suffix`, with four suffixes plus the bare name.

<!-- engine-ref type="TaleWorlds.Core.FaceGen" file="Core/TaleWorlds.Core/TaleWorlds.Core/FaceGen.cs" lines="7-13" -->

| Monster id | Asked for by | Suffix constant |
|---|---|---|
| `<race>` | any battle spawn | none, `FaceGen.GetBaseMonsterFromRace` |
| `<race>_child` | town and village children and teenagers | `MonsterSuffixChild`, `FaceGen.cs:13` |
| `<race>_settlement` | notables, shop workers, alley bosses, tavern hosts | `MonsterSuffixSettlement`, `FaceGen.cs:7` |
| `<race>_settlement_slow` | barbers, townsfolk, villagers | `MonsterSuffixSettlementSlow`, `FaceGen.cs:9` |
| `<race>_settlement_fast` | the hurrying townsfolk variant | `MonsterSuffixSettlementFast`, `FaceGen.cs:11` |

The concatenation happens in `GetMonsterWithSuffix` (`FaceGen.cs:44-47`, `TaleWorlds.MountAndBlade`), which returns whatever `ObjectManager` holds under that exact string. A name that does not exist returns null, silently.

## Two paths, and one is much cheaper

Decide this first, because it changes everything downstream.

| If your creature | Then | Cost |
|---|---|---|
| is roughly horse shaped and horse sized | **Reskin.** Skin the mesh to `horse_skeleton`, bone for bone, and stop | an afternoon |
| is horse shaped but a different size | **Reskin**, and read the `body_length` warning below before scaling | an afternoon |
| has a different leg count or a radically different spine | **Bespoke.** New skeleton, new clips, new action set, new usage set | weeks |
| should attack with something other than a kick | **Bespoke**, or author one clip onto the existing rig. The vanilla horse rig has no attack animation | weeks, or one clip |
| you are not sure | **Reskin first.** A working creature is a better place to iterate from than a half built rig | |

Source: [custom_creatures](../community/bannerlordmodding-lt/guides/custom_creatures.md) "There are two paths"; the bespoke path owns nearly every entry in [custom_creature_troubleshooting](../community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md). **A reskin inherits the donor's behaviour, not just its animations.** Your creature now shares an action vocabulary with the engine, so "our code never fires this action" stops meaning "nothing fires it". The war ram bound `act_horse_rear` (the engine fires it on every damaged mount, and `Agent.Mount` refuses while it is current) and then `act_horse_strike_front` (inside the `StrikeBegin..StrikeEnd` band the engine reads as *being struck*). Both shipped. Details in [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) "The price of a reskin".

## Worked example: the dwarf chain

**1. The skin.** `LOTRLOME_Armory/ModuleData/skins.xml` opens with `<race id="dwarf">`, and that element holds ten `<skin>` children, one per gender and maturity (adult, teenager, tween, child, toddler). <!-- measured: python -c "import re;s=open('skins.xml',encoding='utf-8-sig').read();print(len(re.findall(r'<skin[ \n]', s[:s.index('<race id=\"uruk\"')])))" 2026-09-05 --> The adult male skin carries `skeleton="dwarf_skeleton_a"`, `min_scale="1.05"` and the `sm_dwarf_basemesh_a1_*` body meshes. Only the two adult skins ever field a soldier ([kingdom-voices](../features/kingdom-voices.md) "Read the maturity row, not the race").

**2. The five Monsters.** The base `dwarf` Monster is a full declaration at the top of the file; the other four are diffs off it.

<!-- example file="LOTRLOME_Armory/ModuleData/monsters.xml" id="dwarf_child" -->
```xml
	<Monster id="dwarf_child"
			 base_monster="dwarf"
			 action_set="as_dwarf_child"
			 weight="30"
			 walking_speed_limit="1.6"
			 standing_eye_height="1.20"
			 crouch_eye_height="0.70"
			 arm_length="0.6"
			 arm_weight="2.4" />
```

`dwarf_settlement` is the same idea with a `<Flags>` child, and `dwarf_settlement_slow` / `_fast` are two lines each, differing only in `walking_speed_limit`. The three attributes a reader changes: `base_monster` (name one and every attribute you omit is inherited, `Monster.cs:193-208`), `action_set` (which clip set this variant plays), `walking_speed_limit`.

**3. The action sets.** `as_dwarf_warrior` is **standalone**: it declares `skeleton="dwarf_skeleton_a"` and `movement_system="bipedal"` and no `base_set`, so it inherits nothing and must carry the whole Native surface itself. The character creation pair is the opposite, thin diffs: `as_dwarf_facegen base_set="as_dwarf_warrior"` with 106 actions and `as_dwarf_female_facegen base_set="as_dwarf_facegen"` with 31. <!-- measured: python -c "import re;t=open('action_sets.xml',encoding='utf-8-sig').read();[print(s,len(re.findall(r'<action\b',re.search(r'<action_set\b(?:(?!</action_set>).)*?id=\"'+s+r'\"(?:(?!</action_set>).)*?</action_set>',t,re.S).group(0)))) for s in ('as_dwarf_facegen','as_dwarf_female_facegen')]" 2026-09-05 -->

**4. The ageing row.**

<!-- excerpt file="Main/_Module/ModuleData/raceage/race_age_config.json" -->
```json
    "dwarf":      { "maxAge": 250,   "becomeOld": 220,  "comesOfAge": 18, "middleAge": 125,  "fertilityEnd": 220,  "fertilityMod": 0.6 },
```

**5. The consumer.** Every Erebor troop names the race and a matching body property.

<!-- example file="Main/_Module/ModuleData/troops/troops_erebor.xml" id="erebor_militia_spearman" -->
```xml
    <NPCCharacter
        id="erebor_militia_spearman"
        race="dwarf"
        default_group="Infantry"
        level="11"
        name="{=aom_er_militia_spear}[Erebor] Militia Spearman"
        occupation="Soldier"
        culture="Culture.erebor">
```

The three attributes a reader changes first: `race` (the whole chain above hangs off this one string), `level` (tier and stat scaling, see [Troops](troops.md)), and the `<face_key_template>` child that follows, `value="BodyProperty.fighter_erebor"`, which picks the `TAOM_bodyproperties.xml` range the face rolls inside.

### Two defects live in that chain today

Both are Monster naming mistakes, and both are invisible until a settlement scene builds.

- **`beserker_child`** is missing an `r`. The engine asks for `berserker_child`, gets null, and a berserker child never resolves a body.
- **`hill_troll`'s four variants are named `troll_child`, `troll_settlement`, `troll_settlement_slow`, `troll_settlement_fast`.** The race id is `hill_troll`, so none of the four is ever found. The action sets on the same race are named correctly (`as_hill_troll_child` exists), which is what makes the mismatch easy to miss.

## Recipes

### Add a race

1. Author the skin meshes and, if the proportions differ from human, the skeleton. Read [bannerlord-skeleton-authoring](../reference/bannerlord-skeleton-authoring.md) first: a clip authored on a mesh rig looks perfect in Blender and twisted in game.
2. **Append** a `<race id="<race>">` block at the END of `LOTRLOME_Armory/ModuleData/skins.xml`, ten `<skin>` children, copying the closest existing race. Inserting anywhere else renumbers every race below it.
3. Add five `<Monster>` entries to `LOTRLOME_Armory/ModuleData/monsters.xml`: `<race>`, `<race>_child`, `<race>_settlement`, `<race>_settlement_slow`, `<race>_settlement_fast`. Spell every one of them exactly, then read them back.
4. Action sets in `LOTRLOME_Armory/ModuleData/action_sets.xml`. Cheapest correct form is `base_set="as_human_warrior"`, which inherits everything. A **standalone** set (its own `skeleton=`, no `base_set`) must be brought to full Native parity or the engine crashes the first time it asks for an action the set lacks, for example a unit walking into water.
5. Copy `as_dwarf_facegen` and `as_dwarf_female_facegen` **verbatim** and rename two attributes per block: the male `id` and its `base_set` (point it at the combat set the Monster names), the female `id` and its `base_set` (point it at the new male facegen). The slim "declare only the 14 parent action types" form is not enough; the engine does not fall through `base_set` for `act_childhood_*` and friends.
6. Add the build and face range to `Main/_Module/ModuleData/TAOM_bodyproperties.xml`, and a row to `Main/_Module/ModuleData/raceage/race_age_config.json`.
7. Mirror the three Armory edits into `docs/reference/lotrlome-armory-snapshot/` and update its README checklist.
8. Set `race="<race>"` on the troops, notables and lords that use it, and localize the race name through [Strings and localization](strings-and-localization.md).

**Check:** `python tools/audit_action_set_parity.py` (exits non-zero on a short humanoid set or a root-level `<action>`), then `python tools/validate_mesh_refs.py --no-rgl-log` (it covers the eight skins.xml mesh attributes), then `python tools/validate_moduledata.py`.
**Takes effect:** full game restart. Whether an existing save survives a race integer moving was never tested (see below), so smoke a save that predates the edit as well as a new campaign.
**Code:** No code changes needed. One exception: `<voice_types>` binds per `<skin>`, so two cultures sharing a race share one voice pool, and splitting them is not a data edit. TAOM has not written down how.

### Add a creature by reskinning an existing skeleton

1. Skin the mesh to a skeleton the engine already ships, bone for bone. `horse_skeleton` for anything horse shaped.
2. Write one `<Monster>` with `base_monster=` naming the donor. Everything you do not name is inherited.
3. If it is rideable, author the mount item in the harness files and leave `body_length="100"` unless you have read the scaling warning below. Check whether your body mesh carries the rider's seat; if it does not, ship a barding and fill `HorseHarness` in every roster that uses the mount. See [Mounts and harness](items-mounts-and-harness.md).
4. Do not bind an attack action without first establishing its type in `action_types.xml`, whether the inherited `monster_usage` set names it in a verb slot or table, and whether the engine branches on that type anywhere.
5. Do not assume an empty `HorseHarness` slot is inert. Open the body mesh and confirm where the saddle is before you decide a bare mount is acceptable.

TAOM's war ram is the whole reskin, seven attributes:

<!-- example file="LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml" id="taom_war_ram" -->
```xml
	<Monster
		id="taom_war_ram"
		base_monster="horse"
		action_set="as_horse"
		weight="320"
		hit_points="160"
		jump_acceleration="7.5"
		relative_speed_limit_for_charge="4.0" />
```

That inherits `Mountable`, `CanRear`, `CanCharge`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone name, the ground slope block and all twelve rein attributes. Reusing `as_horse` also means `as_horse_map` and `as_horse_town_and_village` already exist; a missing `_map` child is a native access violation on the campaign map.

**Check:** `python tools/audit_action_set_parity.py`, then `python tools/validate_mesh_refs.py --no-rgl-log`.
**Takes effect:** full game restart.
**Code:** No code changes needed for the reskin itself. TAOM's war ram carries `Main/Features/WarRam/` only because it was given a bespoke attack; a plain mount does not need it.

### Add a creature with its own skeleton

Do not start here from this chapter. Invoke `/new-creature-mount`, then follow [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) phases 1 to 5 in order: skeleton tpac, clips with `quad_movement` tagged on every movement bound clip, `action_types.xml`, `action_sets.xml` plus its `_map` and `_town_and_village` children, `monster_usage_sets.xml`, then the rider partial. `LOTRLOME_Armory/ModuleData/Monsters/LOTR/` holds the seven creature Monsters TAOM ships and is the folder your file belongs in.

**Check:** `python tools/verify_mount_assets.py spider` (substitute your creature once it is registered in that script's `CREATURES` table, which today holds `spider`, `elephant` and `mumakil`), then `python tools/audit_action_set_parity.py`.
**Takes effect:** full game restart.
**Code:** Code changes required in `Main/Features/<Creature>/`: a behaviour tree, an attack service and an IoC registration, in the shape of `Main/Features/Spider/` or `Main/Features/Warg/`. A creature that only walks and carries a rider does not need them; one that attacks does.

### Delete a race or a creature

TAOM has never written a deletion checklist for a race, and deletion is the operation the project has already got wrong: a seven commit Armory reorganisation broke item references and was caught from a screenshot, not a gate ([rca-armoury-keyforce-cleanup](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)). Until [Retire content](recipe-retire-content.md) covers it, the minimum is: find every `race="<race>"` consumer first, repoint them, and only then remove the skin, the five Monsters and the action sets. **Never remove a `<race>` block from the middle of `skins.xml`**, for the same renumbering reason that governs adding one; leave the block in place and orphan it instead.

**Check:** `python tools/validate_moduledata.py`, then `python tools/validate_mesh_refs.py --no-rgl-log`, then `python tools/audit_deleted_mesh_impact.py` if you removed art.
**Takes effect:** full game restart, and only on a new campaign for anything a save already holds.
**Code:** No code changes needed unless the race appears in a C# table; `Main/Features/CombatMechanics/CombatMechanicsConfig.cs` keys several lists by race name.

## Gotchas: what fails silently and what crashes

- **A misspelled `race=` throws, it does not fall back.** `FaceGen.GetRaceOrDefault` is a raw dictionary index, `FaceGen.cs:115-118`.
- **A misspelled Monster variant id returns null and says nothing.** `GetMonsterWithSuffix` concatenates and looks up, `FaceGen.cs:44-47`. This is exactly what `beserker_child` and the `troll_*` block do today.
- **A missing or slim `as_<race>_facegen` pair renders the character creation parent and child agents contorted or lying down.** The parent menu working does not mean the later stages work, they are separate failure modes from one cause ([character-creation](../features/character-creation.md) "LOTRLOME `as_<race>_facegen` action_set requirement").
- **A standalone action set that drifts behind Native crashes on the first action it lacks.** The dwarf set was 423 action types short between Native 1.3 and 1.4.6 ([race-age-system](../features/race-age-system.md) step 5). Fixer: `python tools/patch_dwarf_action_parity.py --target <action_sets.xml> --set-id as_<race>_warrior --apply`.
- **A reskin inherits the donor's skeleton, not its saddle, so a `HorseHarness` is REQUIRED.** The horse and warg model the rider's seat on the mount BODY and look right unbarded; the war ram's `sk_eb_goat_a`/`_b` are bare pelts with the seat on the eight `sk_eb_goat_bard_*` harness meshes, and the spider has no seat at all because no spider harness item was ever authored. Nothing in the XML says which is which, so fill the slot everywhere and argue any exception in `_HARNESSLESS_BY_DESIGN`. Gated by `MOUNT_WITHOUT_HARNESS` ([war-ram](../features/war-ram.md)).
- **`body_length` on a mount item scales the RIDER too.** `EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the same value and the scale block in `Mission.BuildAgent` has no `IsMount` guard. 100 is identity ([creature-mount-authoring](../ai-includes/creature-mount-authoring.md)).
- **A root level `<action>`, one parented by `<action_sets>` instead of an `<action_set>`, loads fine on the client and kills a dedicated server at boot with a `KeyNotFoundException`.** No single player run reproduces it; `audit_action_set_parity.py` is the only gate ([tools README](../../tools/README.md)).
- **Three races write `mesh_maturity_type ="adult"` with a space before the `=`.** A grep for the unspaced form silently skips them and returns a confident partial answer. Match on `mesh_maturity_type\s*=\s*"adult"` ([kingdom-voices](../features/kingdom-voices.md) "Correction, 2026-08-25").
- **The tracked Armory snapshot has already drifted.** `docs/reference/lotrlome-armory-snapshot/skins.xml` still matches the live file byte for byte, but `action_sets.xml` is 3,921,018 bytes against 3,921,068 live, a 50 byte gap. <!-- measured: stat -c '%s' docs/reference/lotrlome-armory-snapshot/action_sets.xml "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml" 2026-09-05 --> A restore from the snapshot today would drop whatever that is.

## What TAOM has not written down

Say so rather than guessing, and go to the file named.

- **`skins.xml` has no managed deserializer.** Searching the v1.4.8 decompile for `skins.xml` or a `"skins"` object type returns nothing; the race list arrives through `MBAPI.IMBFaceGen.GetRaceIds()`, a native call. So the meaning of a `<skin>` attribute cannot be recovered from the dump. The only authority is the shipped file and the vanilla `Native/ModuleData/skins.xml` beside it.
- **Which `hair_tag` / `beard_tag` / `tattoo_tag` names are legal per race** is unresolved. The declaration site is the per race block in `skins.xml`; the matching is native (`MBBodyProperties.GetHairIndicesByTag`). Working examples to copy live in `Main/_Module/ModuleData/TAOM_bodyproperties.xml`.
- **Whether a race integer that moves invalidates an existing save** was never tested. The in-file comment only records the mitigation (append at the end), not the failure it avoids. Issue #321 is the trail.
- **Splitting one race's voice between two cultures** has no recorded method. `<voice_types>` is per `<skin>`, so today it cannot be done from data.
- **`sauron` is the one race with no `as_<race>_facegen` pair**, and it is applied to a character at `Main/_Module/ModuleData/lords.xslt:1060`. That has not bitten because he is a lord and never a character creation parent, but a culture that made him one would hit failure mode 1 above.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 3 loaded modules ship a `skins.xml` carrying a `<race>`: Native 1 and NavalDLC 1 (both `human`), LOTRLOME_Armory 14. So 15 distinct race ids merged. TAOM_Map ships an empty Kit stub | `for d in */; do [ -f "$d/ModuleData/skins.xml" ] && echo "$d $(grep -c '<race' "$d/ModuleData/skins.xml")"; done` under `$BANNERLORD_GAME_DIR/Modules` | 2026-09-05 |
| 10 `<skin>` children in the `dwarf` race block | `python` slice of `skins.xml` up to the `uruk` block, counting `<skin` | 2026-09-05 |
| 70 `<Monster>` elements in the Armory monsters.xml, 14 races times 5 variants | `python -c "import re;print(len(re.findall(r'<Monster\b(?:(?!/?>).)*?\bid=\"([^\"]+)\"', open('monsters.xml',encoding='utf-8-sig').read(), re.S)))"` | 2026-09-05 |
| 26 `as_*_facegen` ids, so 13 complete pairs; `sauron` has none | `grep -o 'id="as_[a-z_]*facegen"' action_sets.xml \| sort -u \| wc -l` | 2026-09-05 |
| 106 actions in `as_dwarf_facegen`, 31 in `as_dwarf_female_facegen` | the `python` regex in the worked example marker | 2026-09-05 |
| 15 rows in `race_age_config.json`, 3 of them `immortal` | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/raceage/race_age_config.json'));print(len(d['races']))"` | 2026-09-05 |
| 11 distinct `race=` values across TAOM's ModuleData XML | `grep -roh 'race="[a-z_]*"' Main/_Module/ModuleData --include='*.xml' \| sort -u \| wc -l` | 2026-09-05 |
| 66 `race="dwarf"` rows in `troops_erebor.xml` | `grep -c 'race="dwarf"' Main/_Module/ModuleData/troops/troops_erebor.xml` | 2026-09-05 |
| 1330 merged action sets (1304 humanoid, 9 creature, 17 other root), Native `as_human_warrior` reference surface 4699 active action types, 0 gaps today | `python tools/audit_action_set_parity.py` | 2026-09-05 |
| 7 creature Monster files under the Armory `Monsters/LOTR/` folder | `ls "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/" \| wc -l` | 2026-09-05 |
| snapshot `action_sets.xml` 3,921,018 bytes against 3,921,068 live; `skins.xml` identical at 5,678,421 | `stat -c '%s'` on both pairs | 2026-09-05 |

## Read next

- [troll-race](../features/troll-race.md) for the ten step race build order, and [race-age-system](../features/race-age-system.md) for the ageing config plus the standalone action set parity rule.
- [character-creation](../features/character-creation.md) for the full `as_<race>_facegen` copy recipe and both failure modes, with [rca-elf-cc-facegen-2026-05-22](../reviews/rca-elf-cc-facegen-2026-05-22.md) for why that gap shipped twice.
- [hero-race](../features/hero-race.md) for the per race position offsets and the wanderer equipment fit rule; [kingdom-voices](../features/kingdom-voices.md) for the race to voice binding and the seven unbound races.
- [war-ram](../features/war-ram.md) for a reskin end to end, and [creature-mount-authoring](../ai-includes/creature-mount-authoring.md) for the bespoke phases and the reskin price.
- [custom_creatures](../community/bannerlordmodding-lt/guides/custom_creatures.md) and [custom_creature_troubleshooting](../community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md) for the engine-side model and the symptom index; [bannerlord-skeleton-authoring](../reference/bannerlord-skeleton-authoring.md) before authoring any clip.
- [culture-playability-wiring](../features/culture-playability-wiring.md) row 14 and [Add a culture](recipe-add-a-culture.md) when the race arrives with a new culture; [lotrlome-armory-snapshot README](../reference/lotrlome-armory-snapshot/README.md) for the mirror checklist; [tools README](../../tools/README.md) for every validator's flags.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/body-properties.md](./body-properties.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/module-armory.md](./module-armory.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
