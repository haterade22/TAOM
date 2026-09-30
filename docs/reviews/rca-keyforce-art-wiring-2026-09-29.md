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
| F2 | LOW | `generate_enlistment_rosters.py` would have built goblin "cavalry" kits from the spider riders' gear, on foot | Same | Same | `drop_creature_riders`, pinned by `tools/tests/test_generate_enlistment_rosters.py` |
| F3 | MED | `rebalance_troops.py` would have rebaselined the riders' copied Dol Guldur skills (+94, +244 points) | Same | Same | Both ids in `SKIP_TROOP_IDS` |
| F4 | LOW | The AI branch weighting (a lord commits a Lurker line to one branch at 9999:1) and notable slot growth were not stated to Mike before he chose the branch | Same | Same | Put to Mike during the review; he kept the branch |
| F5 | LOW | A comment claimed `c9fae4dc` set the elephant rider's weight to 12.0; it set 10.0 (12.0 came in `43dada47`) | A number stated without reading its proof | `git blame` gave the commit; the value came from memory of the file, not from `git show` | Existing rule `evidence-over-claims.md` C: read the diff before naming what a commit did |
| F6 | LOW | The ledger said the 12 language files were "not yet changed" and the snapshot README said the translator fills them; both went stale within the session when the rows were hand-translated | Doc written before the work finished | The docs were written mid-work, then the plan changed (hand translation instead of the paid run) | Existing rule C1: write the record after the final state; re-read ledger rows at commit time |
| F7 | LOW | Two generated files were hand-edited out of their generator's order: `taom_troop_name_strings.xml` (its header says "Do not hand-edit") and the 12 translation caches (sorted by their writer) | Generated file edited by hand | The one-off script cloned an anchor line without checking who owns the file | Regenerated with the owning tools; lesson in `build-tooling-workflow.md` |
| F8 | LOW | The `/armory-audit` skill's Step 4 still named the mirror after Step 3 was rewritten to forbid it | Partial policy edit | The edit fixed the sentence that misled this session, not the file's other mention | Same lesson: grep the file for the rule's other mentions |
| F9 | LOW | Three per-howdah constants had no reader but the list they filled, and a test restated that list; the singular differed from the plural by one letter | Plumbing with no reader | The builder agent added a constant per id by habit | Deleted (Design lens); the test compares literals |
| F10 | LOW | `HowdahHarnessItemTests`' class-level `LiveInstall` tag kept its repo-only rider test off CI | Test tagging | A class tag is the default and the rider test was added to an Armory class | Tag moved onto the three Armory methods |
| F11 | LOW-MED | No in-repo gate for the five live spider items or for the Custom Battle picker's scenes (Mike's new Edoras row names a scene created that day) | Unversioned module without a gate | The elephant got a gate by precedent; the spider and the scenes had none | `SpiderMountItemTests`, `CustomBattleSceneLiveDataTests` |
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
