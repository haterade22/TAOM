# Troubleshooting: symptom to cause

## What this file is

A lookup table for the moment when the game does something wrong and you do not yet know which file
did it. Every row names one symptom, the data defect behind it, and the chapter that explains the
fix. Each one is a failure that was actually root-caused in TAOM at least once, so a row that matches
your symptom is worth more than a fresh theory.

## Read the evidence first

TAOM writes its own log and its own crash bundle. The log is `Logs/taom_debug_*.log` and a crash
also drops `Logs/taom_crash_<timestamp>_<sig>.zip` beside it (`docs/features/crash-report.md:17`);
the folder is built from the game's working directory, so it sits next to the executable rather than
in the repo (`Main/Features/CrashReport/CrashReportService.cs:276`). The engine keeps a separate log
at `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`, and that is the one
that names a missing collision body (`docs/features/crash-report.md:133`).

Three habits save the most time:

- **A hang is not a crash.** A crash throws, so it lands in the bundle. A hang blocks the main
  thread, so nothing is thrown and the crash pipeline never fires; the evidence is the last
  `[BattleLoad]` line in the debug log instead (`docs/features/battle-load-diagnostics.md:9`).
- **Check the cheap XML causes before the expensive ones.** For anything race-specific or
  render-specific the order is `skins.xml`, then `monsters.xml`, then `action_sets.xml`, and only
  then meshes or engine internals. An elf CC break cost a long mesh investigation and was one
  missing `as_elf_facegen` reference (`docs/reviews/lessons/misc.md:23`).
- **A reporter's theory names the symptom they noticed, not the defect.** One tournament bundle
  from a dwarf campaign carried two unrelated bugs, and the crash in the file was not the crash
  being complained about (`docs/reviews/lessons/misc.md:47`, `docs/features/arena.md:17-36`).

Some rows below point at files in the game install (`TAOM_Map`, `LOTRLOME_Armory`). This file lives
in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side
validator gate with any fix.

## Symptom to cause

Codes in `CAPITALS` are `python tools/validate_moduledata.py` finding codes: if the row names one,
the validator catches that defect before you launch.

| Symptom | What is actually wrong | Where to fix it |
|---|---|---|
| New campaign crashes with an NRE, log says `Null object reference found with ID: lord_X` | An `<NPCCharacter is_hero="true">` in `lords.xml` has no matching `<Hero id="lord_X">` in `characters/heroes.xml`, so the hero's `CharacterObject` is null and `LordNeedsHorsesIssueBehavior.ConditionsHold` reads it | [Lords and heroes](lords-and-heroes.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/kingdom-creation.md:516` |
| New campaign crashes in `Hero.Deserialize` | A `<Hero>` entry carries no `faction`. TAOM's fix was `faction="Faction.neutral"` on every entry that had none | [Lords and heroes](lords-and-heroes.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/named-companions.md:149` |
| A child of your culture turns 8 and the game closes | An `is_main_culture="true"` culture is missing some of the six `child_education_templates_stage_2_page_0_branch_{0-5}_{culture}` entries; the engine dereferences the missing template with no null guard. `MISSING_EDUCATION_TEMPLATES` | [Cultures](cultures.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:5` |
| The new-game loading screen never ends and one NRE repeats thousands of times | A castle culture's `<notable_templates>` has no entry for one of the four castle occupations, or holds a literal null, so `HeroCreator.CreateNotable` NREs inside `OnNewGameCreated` and campaign creation restarts every tick | [NPCs, notables and townsfolk](npcs-notables-and-townsfolk.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/castle-recruitment.md:117` |
| New game throws `KeyNotFoundException` in `GetInfestedHideoutCount` | A clan in `Clan.BanditFactions` has a culture that owns no hideout; the engine indexes `_hideouts[clan.Culture]` with no guard. Author the hideouts first, then the clan, and strip vanilla bandit clans that lost theirs | [Clans](clans.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/bandit-management.md:103` |
| The game closes on a daily tick, with no TAOM frame anywhere on the stack | A hero with `Occupation.Lord` belongs to a culture that owns no settlement; vanilla `SpawnLordParty` ends in an unguarded `Settlement.All.First(culture)`. `LANDLESS_CULTURE` | [Settlements](settlements.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/lord-spawn-guard.md:16-49` |
| Hovering a troop stack on the party screen closes the game | An upgrade edge whose target does not reach a higher tier makes `GetXpCostForUpgrade` return 0, and `GetTroopXPTooltip` then evaluates `Xp % cost`. `UPGRADE_TIER_COLLAPSE` | [Troops](troops.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/gamemodels-services.md:623` |
| A character-creation stage is blank, or advancing throws `KeyNotFoundException` | The playable culture has no entries in the four culture-keyed narrative menus. An empty menu still reports that advancing is fine, then `SelectedOptions[CurrentMenu]` throws | [Configs: factions and world](configs-factions-and-world.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:148`, `docs/reviews/lessons/adapters-taleworlds-api.md:408` |
| The campaign map fails to load, for every player | A settlement in `settlements.xml` has no matching world-map scene entity | [Settlements](settlements.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:160` |
| Battle load never finishes: no crash, no error, one CPU core pinned | A weapon or shield names a `bo_` collision body that resolves to nothing, and `PreloadHelper.WaitForMeshesToBeLoaded` polls it forever. `rgl_log_errors_*.txt` carries `get_object failed for body: bo_X` | [Module: Armory](module-armory.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/mesh-ref-validation.md:7` |
| A troop or hero spawns in its underwear | An `Item.X` in its equipment resolves to no item. `BROKEN_ITEM_REF` | [Equipment rosters](equipment-rosters.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/moduledata-validation.md:15` |
| Still naked, and the validator says the item is fine | The item's XML file is newer than the running process. Item XML registers once at launch and globs once at campaign start, so nothing re-reads it mid-session | [Editing safely](editing-safely.md#when-an-edit-reaches-the-game); `docs/reviews/lessons/data-content-cultures.md:124` |
| Arena spectators are naked, but the same townsfolk are dressed on the town walk | The townsfolk or notable has only a `civilian="true"` roster. Spectators spawn with battle equipment, so `FirstBattleEquipment` is empty | [NPCs, notables and townsfolk](npcs-notables-and-townsfolk.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:118` |
| A named lord has a generic face instead of the authored one | `face_key_template` points at a `BodyProperty` that is not defined. The engine registers a placeholder, then `MBObjectManager.UnregisterNonReadyObjects` drops it. `BROKEN_BODY_PROPERTY_REF` | [Body properties](body-properties.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/moduledata-validation.md:24` |
| A troop carries a weapon through the pre-battle phase and never draws it | Its crafted primary usage resolved to a set flagged `requires_no_shield` while the roster also gives it a shield. The primary is the first `WeaponDescription` that lists every piece, so a polearm absent from `OneHandedPolearm` loses | [Items: weapons and crafting](items-weapons-and-crafting.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:532` |
| A harness refuses to equip, with no message and no log line | The `HorseHarness` has no `<Armor family_type>`, so it defaults to 0, the human family, and the inventory screen compares that against the mount's `Monster.FamilyType`. `MISSING_HARNESS_FAMILY_TYPE` | [Items: mounts and harness](items-mounts-and-harness.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/xslt-moduledata.md:110` |
| The barding is on the mount until the first inventory transfer, then gone | The `Horse` and the `HorseHarness` in the same set disagree on family type. An XML roster places the pair without the inventory screen ever checking, and `SPInventoryVM.cs:3923` force-unequips it later. `HARNESS_FAMILY_MISMATCH` | [Items: mounts and harness](items-mounts-and-harness.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/xslt-moduledata.md:111` |
| A dwarf lord spawns inside his horse | A `race="dwarf"` character is tagged `Cavalry` or `HorseArcher`, or is handed a mount other than the Dwarven war ram. The dwarf skeleton's rider bone is misaligned. `MOUNTED_DWARF` | [Items: mounts and harness](items-mounts-and-harness.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/moduledata-validation.md:31` and `:165` |
| A culture's garrisons, militia, villagers or town patrols are Calradian | A party-template attribute is unbound. A culture retagged in `spcultures.xslt` copies vanilla's value for every attribute the block does not name, so nothing in the repo shows the defect | [Party templates](party-templates.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/culture-playability-wiring.md:215-260` |
| Caravans are Calradian about half the time | Caravans come only from the `<caravan_party_templates>` and `<elite_caravan_party_templates>` child elements, and the deserializer appends rather than replaces. Emitting the TAOM block without excluding vanilla's leaves both | [Party templates](party-templates.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/culture-playability-wiring.md:239-245` |
| The tavern of a LOTR town offers "Hired Pike" | The culture's `<basic_mercenary_troops>` list is still vanilla's. The engine also walks the drawn troop's upgrade targets, so a vanilla tier-2 entry surfaces as its tier-4 name | [Troops](troops.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/tavern-mercenaries.md:22-42` |
| The tavern still offers the old troop after the fix | `TownMercenaryData.TroopType` is a saved property, so an existing campaign keeps its stored offer until that town's next reroll, up to two in-game days | [Troops](troops.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/tavern-mercenaries.md:188-191` |
| A hideout boss fight starts with everyone friendly and the boss says a guard line | The boss troop is not `occupation="Bandit"` in the bandit culture, so `GuardsCampaignBehavior` hijacks the conversation and the taunt that restores enmity never runs | [Troops](troops.md#gotchas-what-fails-silently-and-what-crashes); `docs/reviews/lessons/data-content-cultures.md:142` |
| A new troop is authored but never appears in recruit slots | Volunteers do not come from the troop XML. The per-settlement and per-clan pools are C# under `Main/Features/TroopProgression/RecruitmentPools/`, and a culture with no pool has empty recruit slots | [Troops](troops.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/culture-playability-wiring.md:113` |
| The player finishes character creation with zero denars | The culture has no `<Culture id playerGold>` row in `startup_resources/startup_resources_config.xml`. The documented default is 0 with no warning, so an omission looks exactly like an intentional zero | [Configs: balance](configs-balance.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/culture-playability-wiring.md:82`, `docs/features/character-creation.md:375` |
| Wanderers never show up in the new culture's towns | The wanderer `<NPCCharacter>` entries were copied from a donor culture and still carry the donor's `culture=` attribute, so they spawn in the donor's settlements | [Wanderers and named companions](wanderers-and-named-companions.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/kingdom-creation.md:536` |
| Every clan in a new kingdom shows the same plain banner | The clan was created with a placeholder `banner_key`. A real key is hundreds of characters long; a short one is a placeholder | [Banners and heraldry](banners-and-heraldry.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/kingdom-creation.md:526` |
| A settlement shows the wrong culture's banners and guards | `owner` or `culture` on the `<Settlement>` element was not updated, in the live `TAOM_Map/ModuleData/settlements.xml` | [Settlements](settlements.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/kingdom-creation.md:544` |
| A child renders lying down or T-posed past the character-creation parent menu | The race's facegen action set declares only the 14 parent action types. The engine does not fall through `base_set` for the childhood, toddler, inventory, stand, sit and story-background action types; they must be declared directly | [Recipe: add a race or creature](recipe-add-a-race-or-creature.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/character-creation.md:388` |
| The character-creation parent menu itself shows a contorted mesh | The race has no `as_<race>_facegen` entry at all, in `LOTRLOME_Armory/ModuleData/action_sets.xml`, so the engine falls back to a set that does not bind to its skeleton | [Recipe: add a race or creature](recipe-add-a-race-or-creature.md#gotchas-what-fails-silently-and-what-crashes); `docs/features/character-creation.md:386`, `docs/features/culture-playability-wiring.md:115` |
| A troop renders as a pure black silhouette in the encyclopedia | An Isengard helmet mesh bundles a skin material alongside the armour one. Closed by setting `UseTeamColor="true"` on the affected items; the bundling itself is still open | [Items: armor](items-armor.md#gotchas-what-fails-silently-and-what-crashes); `docs/reference/lotrlome-armory-snapshot/README.md:328-337` |

## Five readings that mislead people

- **A green validator does not mean a dressed troop.** `validate_moduledata.py` reads files from
  disk. It never starts a campaign, so it cannot see load timing: a brand new item file is invisible
  to the running process until a full restart (`docs/reviews/lessons/data-content-cultures.md:124`).
  [Validation and testing](validation-and-testing.md#green-validator-naked-troop) walks the same
  trap.
- **A clean mesh run means clean within the scanned scope.** `validate_mesh_refs.py` reported PASS
  for a year while its default `--items` scope excluded the file that held the defect
  (`docs/features/mesh-ref-validation.md:15`).
- **A reference that looks like a typo may be the asset's real name.** `wm_isengard_shield_a04`
  names `bo_capwm_isengard_shield_a02_clean`, which is misspelled compared with 224 siblings and is
  also the name the asset is packaged under. Correcting it would manufacture the load hang. Only
  names the tool flags `MISSING_BODY` are safe to rewrite (`docs/features/mesh-ref-validation.md:17`).
- **The polearm gate is per equipment set.** It resolves each set on its own, so a shield in one set
  and a `requires_no_shield` weapon in another passes with neither set malformed
  (`docs/reviews/lessons/data-content-cultures.md:1025`). Read the whole roster when the symptom is
  a weapon that never gets drawn.
- **Placeholder counts in older docs are wrong about wanderers.** The shipped file holds 210
  wanderer entries over 20 cultures and 17 of those cultures ship exactly 10; mordor has 15, gondor
  13, erebor 12. Any doc that says every culture has 12 does not match a single culture.
  <!-- measured: python count of spc_wanderer_* ids in Main/_Module/ModuleData/taom_wanderers.xml 2026-09-05 -->

## Worked example: reading a validator finding

This is the tail of a real run against the tree as it stands today, with the two lines that name the
game install removed because they are absolute paths.

<!-- measured: python tools/validate_moduledata.py 2026-09-05 -->

```text
Registry: 5,900 items, 5,291 NPCCharacters, 40 cultures, 476 party templates, 121 body properties
  WARNING INCONSISTENT_ARMOUR_SLOT   troops/troops_dunland.xml:3174 [dunland_militia_spearman]
            slot "Head" is filled in 1 of 3 battle sets. The engine draws each slot from an independently chosen set, so this troop can spawn with that slot empty. Fill it in every battle set or in none

=== SUMMARY ===
  0 error(s), 94 warning(s)
    INCONSISTENT_ARMOUR_SLOT     94
```

Three things to take from it:

1. **The `Registry:` line is the first check.** If the item or character count is far below what you
   expect, the run did not see a module and every reference check below it is weaker than it looks.
2. **A finding gives you file, line and entry id.** `troops/troops_dunland.xml:3174
   [dunland_militia_spearman]` is where to open, not where to guess.
3. **Warnings are not automatically noise, and errors are not automatically new.** These 94
   `INCONSISTENT_ARMOUR_SLOT` warnings are known and accepted. Compare against the rehearsal table in
   [Validation and testing](validation-and-testing.md#rehearsal-runs-2026-09-05) before deciding a
   tool broke.

## The check sequence before you launch

One block, copied from
[Validation and testing](validation-and-testing.md#the-ordered-check-sequence). Run it in this order;
everything before the last step is cheap.

```bash
python tools/validate_moduledata.py          # always, whatever you edited
python tools/validate_mesh_refs.py           # any mesh, body_name or art change
python tools/audit_polearm_shield_parity.py  # any weapon or roster change
python tools/check_external_xslt.py          # only if an .xslt changed
python tools/audit_mbproj_registration.py    # only if you added a file
# then: ask a developer for `dotnet test TAOM.Tests` if you touched
# cultures, party templates or lords
# then: full game restart, new campaign, in-game smoke
```

The last two steps are not optional politeness. The shipped-data tests read ModuleData off disk and
are the only gate on several culture contracts, and a full restart is the only way a new XML file
reaches the running engine.

## What TAOM has not written down

Say so rather than guessing, and point at where an answer would come from.

- **Why the Isengard helmets bundle a skin material** is not established. The applied fix is a
  workaround and the diagnostic Harmony category that exists to name the cause still describes the
  root cause as undetermined (`docs/reference/harmony-patch-registry.md:546`). If a new troop shows
  the same symptom, the census in that category is the instrument, and the earlier
  `_agentVisualLoadingCounter` explanation was refuted and must not be reinstated
  (`docs/reference/harmony-patch-registry.md:550`).
- **The minimum viable kingdom** has no measured floor. TAOM records crash classes and a
  fatal-or-silent checklist, not the smallest clan, lord and settlement counts that avoid them: read
  `docs/features/kingdom-creation.md:514-563` together with the 14-row checklist in
  `docs/features/culture-playability-wiring.md:100-116`, and see
  [Recipe: add a kingdom](recipe-add-a-kingdom.md#the-floor-what-a-realm-needs-before-it-stops-crashing).
- **The banner-key number grammar is undecoded.** Placeholder keys are a known in-game defect and
  the fix on record is copying a working key, not authoring one from scratch
  (`docs/features/kingdom-creation.md:526`). [Banners and heraldry](banners-and-heraldry.md) has
  what is known.
- **Which of a culture's many attributes are mandatory** has never been split into required and
  optional, and two TAOM docs disagree on how many there even are: `docs/features/kingdom-creation.md:128`
  says about 50, `docs/cultures.md:53` says over 80. Trust the deserializer and the shipped file over
  either: [Cultures](cultures.md#attributes).

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 0 errors, 94 warnings, all `INCONSISTENT_ARMOUR_SLOT` | `python tools/validate_moduledata.py` | 2026-09-05 |
| Registry seen by that run: 5,900 items, 5,291 NPCCharacters, 40 cultures, 476 party templates, 121 body properties | `python tools/validate_moduledata.py` (its `Registry:` line) | 2026-09-05 |
| 21 distinct finding codes emitted from the schema engine (the `DUPLICATE_*` codes come from the schema JSON files on top) | `python -c "import re;t=open('tools/taom_schema.py',encoding='utf-8').read();c=set(re.findall(r'code=\"([A-Z0-9_]+)\"',t))\|set(re.findall(r'Severity\.[A-Z]+, \"([A-Z0-9_]+)\"',t));print(len(c))"` | 2026-09-05 |
| 210 wanderer entries over 20 cultures; 17 cultures at exactly 10, mordor 15, gondor 13, erebor 12 | python `re.findall(r'<NPCCharacter\s+id="(spc_wanderer_[a-z_]+?)_(\d+)"')` over `Main/_Module/ModuleData/taom_wanderers.xml`, counted per culture | 2026-09-05 |

## Read next

- [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md) for the bug
  classes the validator encodes and what each code means.
- [`docs/features/mesh-ref-validation.md`](../features/mesh-ref-validation.md) for the missing-body
  hang and the scope trap.
- [`docs/features/battle-load-diagnostics.md`](../features/battle-load-diagnostics.md) for reading a
  hang log phase by phase.
- [`docs/features/crash-report.md`](../features/crash-report.md) for what a crash bundle contains.
- [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md) for the
  fatal-or-silent checklist and the party-template binding contract.
- [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md) for the known new-kingdom
  crashes.
- [`docs/features/lord-spawn-guard.md`](../features/lord-spawn-guard.md) for the landless-culture
  daily-tick crash.
- [`docs/features/castle-recruitment.md`](../features/castle-recruitment.md) and
  [`docs/features/bandit-management.md`](../features/bandit-management.md) for the two new-game
  loops.
- [`docs/features/character-creation.md`](../features/character-creation.md) and
  [`docs/features/arena.md`](../features/arena.md) for the facegen and spectator failures.
- [`docs/features/tavern-mercenaries.md`](../features/tavern-mercenaries.md) and
  [`docs/features/named-companions.md`](../features/named-companions.md) for the two data fixes
  quoted above.
- [`docs/reviews/lessons/data-content-cultures.md`](../reviews/lessons/data-content-cultures.md),
  [`docs/reviews/lessons/xslt-moduledata.md`](../reviews/lessons/xslt-moduledata.md),
  [`docs/reviews/lessons/gamemodels-services.md`](../reviews/lessons/gamemodels-services.md) and
  [`docs/reviews/lessons/misc.md`](../reviews/lessons/misc.md) for the full lesson entries behind
  most rows; the index is
  [`docs/reviews/LESSONS-LEARNED.md`](../reviews/LESSONS-LEARNED.md).
- [`docs/community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md`](../community/bannerlordmodding-lt/guides/custom_creature_troubleshooting.md)
  for the creature and mount table this one is shaped after.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/validation-and-testing.md](./validation-and-testing.md)

<!-- backlinks-end -->
