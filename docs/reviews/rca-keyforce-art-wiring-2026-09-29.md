# RCA: KEYforce art wiring (deep review, 2026-09-29)

## Top-line

The deep review of wiring KEYforce's new elephant and spider art (eight lenses in two waves of four) found no CRITICAL
or HIGH finding and a spread of MEDIUM and LOW ones, every one fixed or decided before the commit. The work itself had
been verified in game: all five spider mounts on whole meshes, the goblin spider riders, the new elephant body and 91
crewed howdahs. What the review caught lay beside that verification. The largest class: the goblin tree's first
cavalry reached three systems that walk troops by role, and the plan had looked only at recruitment and upgrades.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The garrison swap started mapping captured cavalry onto the goblin spider riders (the tree's first Cavalry troops); Mike chose that a creature-mount rider is never a replacement (a garrison keeps the riders it holds) | New troop reaches a role-walking consumer | The plan traced how the riders are recruited and upgraded, not who else walks `troops_*.xml` by `default_group` or upgrade graph | `CreatureMountRiders` + `CultureTroopIndex` skip, pinned by `TroopCultureMapperTests` and `GarrisonCultureCoverageTests`; lesson in `data-content-cultures.md` |
| F2 | LOW | `generate_enlistment_rosters.py` would have built goblin "cavalry" kits from the spider riders' gear, on foot | Same | Same | `drop_creature_riders`, pinned by `tools/tests/test_generate_enlistment_rosters.py` (widened by the Codex pass, C1, and the fix review, R1) |
| F3 | MED | `rebalance_troops.py` would have rebaselined the riders' copied Dol Guldur skills (+94, +244 points) | Same | Same | Both ids in `SKIP_TROOP_IDS` |
| F4 | LOW | The AI branch weighting (a lord commits a Lurker line to one branch at 9999:1) and notable slot growth were not stated to Mike before he chose the branch | Same | Same | Put to Mike during the review; he kept the branch |
| F5 | LOW | A comment claimed `c9fae4dc` set the elephant rider's weight to 12.0; it set 10.0 (12.0 came in `43dada47`) | A number stated without reading its proof | `git blame` gave the commit; the value came from memory of the file, not from `git show` | Existing rule `evidence-over-claims.md` C: read the diff before naming what a commit did |
| F6 | LOW | The ledger said the 12 language files were "not yet changed" and the snapshot README said the translator fills them; both went stale within the session when the rows were hand-translated | Doc written before the work finished | The docs were written mid-work, then the plan changed (hand translation instead of the paid run) | Existing rule C1: write the record after the final state; re-read ledger rows at commit time |
| F7 | LOW | Two generated files were hand-edited out of their generator's order: `taom_troop_name_strings.xml` (its header says "Do not hand-edit") and the 12 translation caches (sorted by their writer) | Generated file edited by hand | The one-off script cloned an anchor line without checking who owns the file | Regenerated with the owning tools; lesson in `build-tooling-workflow.md` |
| F8 | LOW | The `/armory-audit` skill's Step 4 still named the mirror after Step 3 was rewritten to forbid it | Partial policy edit | The edit fixed the sentence that misled this session, not the file's other mention | Same lesson: grep the file for the rule's other mentions |
| F9 | LOW | Three per-howdah constants had no reader but the list they filled, and a test restated that list; the singular differed from the plural by one letter | Plumbing with no reader | The builder agent added a constant per id by habit | Deleted (Design lens); the test compares literals |
| F10 | LOW | `HowdahHarnessItemTests`' class-level `LiveInstall` tag kept its repo-only rider test off CI | Test tagging | A class tag is the default and the rider test was added to an Armory class | Tag moved onto the three Armory methods |
| F11 | LOW-MED | No in-repo gate for the five live spider items or for the Custom Battle picker's scenes (Mike's new Edoras row names a scene created that day) | Unversioned module without a gate | The elephant got a gate by precedent; the spider and the scenes had none | `SpiderMountItemTests`, `CustomBattleSceneLiveDataTests` (the latter tightened by the Codex pass, C2) |
| F12 | LOW | Six comments and docs still said the goblin cultures have no cavalry | Stale claim after a data change | Same as F1 | Reworded to "no cavalry the swap may use" |
| F13 | LOW | A lesson stated as fact that an attached debugger swallowed a crash; the default profile debugs managed code only | Unverified mechanism | Written as the likeliest cause | Reworded to what is established |
| F14 | LOW | A doc sentence kept the older, undecompiled claim that a missing prefab "logs `not found`" | Unverified carried-over text | Retained while rewriting the sentence around it | Points at the verified `PrefabExists` warning |
| F15 | LOW | Roster order put the Light howdah first, changing the troop's default look | UI default | New rosters were inserted ahead of the existing one | Elite first again (Mike) |
| F16 | LOW | A whitespace-only line in the Edoras row | Cosmetic | `git diff --check` was not run on Mike's hunk | Removed; run `git diff --check` on hand-edited files going into a commit (the doubled-CR language files report every line) |

Two process findings are recorded as lessons in `build-tooling-workflow.md`: a builder subagent tagged the change with
an unrelated issue number (#694), and a Visual Studio launch deployed repo refs to Armory items deliberately held back,
which spawned 14 bare elephants in a test session.

## Root-cause pattern: a new troop in a shared tree is read by more than its recruiters

F1 to F4 and F12 share one shape. `goblin_spider_rider` and `goblin_spider_lord` are the first Cavalry troops in the
tree Goblin-town, the Misty Mountain Orcs and Blue Craig share, and at least five consumers walk that tree by role or
upgrade graph without being part of the feature: the AI upgrade weighting, notable slot growth, the garrison swap, the
enlistment roster generator and the skill rebaseliner. None of them needed a code change to pick the riders up, which
is exactly why none of them showed in the plan. The rule:

> **When a troop joins a tree, list every consumer that selects troops by `default_group`, formation or upgrade
> graph, and decide for each whether the new troop belongs.** Grep `DefaultFormationClass`, `default_group`,
> `UpgradeTargets`, `troops_*.xml` readers under `tools/`, and `GetCulturePoolTroopIds`.

## Why each lens caught or missed what it did

- **Data flow (5)** found F1 to F4 by following the new troops outward instead of the ids inward. The builder's plan had
  only the inward trace.
- **XML (7)** found F5, F6, F11 and F16 by running every gate per file kind and diffing against HEAD and the backups.
- **Tooling** found F3, F7 and the cache order by dry-running every tool that reads the changed files.
- **Standards (1)** found F8 and F10; **Design (6)** found F9 and F15; **Engine compatibility (2)** found F13 and F14;
  **Completeness (4)** found the missing issue, the stale ledger row and the missing spider gate.
- **Efficiency (3)** found nothing to change: the only runtime code runs once per elephant rider.

## Feedback memories to codify

None beyond the lessons. The shared-tree rule is a lesson (`data-content-cultures.md`), because it applies to troop
work in every culture, and the generated-file rule is one too (`build-tooling-workflow.md`). Both are cheap to check
and neither has recurred before.

## Codex pass (2026-09-30)

Codex (gpt-6-astra, `xhigh`) reviewed the five commits `7bbe6f4d..0aad0d94`: 0 CRITICAL, 0 HIGH, 0 MEDIUM, 2 LOW,
both confirmed against the code and fixed, and 0 false positives. It disputed suspects S1 to S4 with quoted v1.5.3
decompiles, confirmed S5 as C1 below, and partly confirmed S6 (reported as O2) and S7 (as C2). Its one MEDIUM
observation, marked UNVERIFIED, is O1. Raw output: `docs/reviews/raw/codex-adversarial-keyforce-art-2026-09-30.md`.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | LOW | `generate_enlistment_rosters.py` read a troop's mount from its first battle roster only, so a troop with a horse or no mount in set one and a spider in a later set stayed an enlistment donor. The engine draws each slot from any battle set, and `GarrisonCultureSwapAdapter.RidesLockedCreature` already walked every set. Latent: no shipped troop mixes, and the dry run is identical before and after the fix | Logic error: the classifier took the kit reader's scope | The mount read went inside the generator's existing first-roster loop, which exists because an enlistee copies one coherent kit. Classifying a troop needs every set; copying its kit needs one. The same session wrote the every-set rule in C# and did not carry it into the Python | Mounts read from every battle set while the kit still comes from the first, then widened by the fix review (R1); `test_a_creature_the_engine_mounts_in_any_battle_set_drops_the_troop` and `test_the_kit_comes_from_the_first_battle_set`; a recurrence under "A rule reused by a second consumer answers the second consumer's question" in `build-tooling-workflow.md` (the same root cause, #581). Codex's further suggestion, battle `EquipmentSet` references, is moot: all 561 references in `troops_*.xml` are `equipmentType="Civilian"` |
| C2 | LOW | `CustomBattleSceneLiveDataTests` text-matched `<level name="siege"`: a commented-out declaration passed, a single-quoted or reordered one failed, and the wall level a siege loads alongside `siege` went unchecked | Gate weaker than the engine contract | The gate was written from the line the Edoras scene showed, not from the caller: `BannerlordMissions.OpenSiegeMissionWithDeployment` loads `level_N siege`, and `CustomBattleData.SceneLevels` offers N = 1, 2 and 3 | The gate parses the `scene/levels` declarations and requires all four levels (all 15 siege scenes declare them); `DeclaredLevels_CommentedOutReorderedAndEntityLevels_ReadsOnlyTheSceneDeclarations` pins Codex's three cases and `DeclaredLevels_NoSceneLevelsBlock_IgnoresTheEntitiesLevelLists` the depth check; the siege flag is read with `bool.TryParse`, as `CustomGame.LoadCustomBattleScenes` reads it, and a scene whose declarations do not parse joins the failures by id; lesson in `testing-qa.md` |
| O1 | MED, UNVERIFIED | The Edoras scene declares its civilian level as `civillian` (one declaration, 1,154 entity assignments), and `town_V1`'s center uses the scene for every wall level. A town visit loads `civilian level_2` (`DefaultLocationModel.GetCivilianUpgradeLevelTag`), a name the scene does not declare. Every other installed scene that declares one (145) spells it `civilian` | Data in an unversioned module | The deep review saw the spelling and put it on #696's owed list, but did not trace the campaign's town binding to the scene | Reported to Mike: rename the level in the scene editor, check that no `civillian` is left in `scene.xscene` (1,155 today), since renaming the declaration alone would strand the entities, then walk into Edoras. What the native loader does with an unknown level name was not traced (the one native function holding the string reads like a console command) |
| O2 | INFO | The Custom Battle scene names and the three Dol Guldur rider names have no registration rows, and the brood's 12 language rows are English placeholders | Pre-existing localization debt | Not in this change's scope: the rider names predate it, the new Edoras row follows the existing scene-name pattern, and the brood placeholders were English before the rename | None here; the brood and rider names are on #695's translation backlog |

## Fix review (2026-09-30)

A seven-lens `/deep-review` of the fix diff (Standards, Engine compatibility, Data flow and Tooling, then Efficiency,
Completeness and Design) found no CRITICAL, HIGH or MEDIUM finding and two LOW ones, both fixed.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | LOW | The C1 fix still read only `EquipmentRoster` children. An `<equipment>` written directly under `<Equipments>` overwrites its slot in every set the troop has (`BasicCharacterObject.Deserialize`, then `MBEquipmentRoster.AddOverriddenEquipments` and `Equipment.DeserializeNode`; an empty or unknown id clears the slot), and 88 shipped troops take their Horse that way, 11 of the 14 mounted Harad troops among them. A creature rider written in that style would have stayed a donor. Latent: none of the 88 is a creature | Missing vanilla gate, a repeat | The rule has been on record since 2026-09-22 (`lessons/xslt-moduledata.md`, "Troop-level `<equipment>` overrides beat roster slots and reach every set", #636, where `generate_troop_roster_page.py` missed the same 88 mounts, #641). It lives in the XSLT and ModuleData category and in no rule that loads for a `tools/` script, so neither the generator's author nor Codex read it; Codex's fix list named battle rosters and `EquipmentSet` references, not the override | `parse_troops` builds each battle set as the engine does, the roster's slots with the overrides written over them, and reads the kit (first set) and the mounts (every set) from that one model (the Design lens's shape); the civilian check is `ranged_ladder.is_civilian`, which also reads `equipmentType="Civilian"`; fixture troops pin the override, an empty override, a creature in either set and a civilian-typed roster; this instance added to the #636 lesson; a short rule section in `.claude/rules/moduledata-validation.md`, which loads for `tools/**/*.py` |
| R2 | LOW | Docs still described the pre-fix behaviour: `file-catalogue.md` said the gate checks a `siege` level; `enlistment.md` and the generator's header said the donor pools mount mumakil and war elephants; `custom-battles.md` listed no `CustomBattleSceneLiveDataTests`; the code cited the raw report's F1 and F2, which in this RCA are two other findings | Stale claim after a change, F8 again | The fix edited the code and its own records, not the other docs that describe the same tool and gate | Reworded, the row added, the citations point at C1, C2 and R1; the fourth recurrence under "A docs sweep for a changed value greps the old VALUE" in `build-tooling-workflow.md` |

Three lenses (Engine compatibility, Data flow and Tooling) found R1 on their own, each by feeding the production parser
a synthetic troop; Standards, Data flow and Completeness found parts of R2. Efficiency measured the new gate at about
4 KB read per scene (2.2 ms for the 15 against 121 ms for the old text search).
