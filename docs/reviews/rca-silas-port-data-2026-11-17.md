# RCA: #1439 Silas He's data fixes (2026-11-17)

**Scope:** Batch D of the port of Silas He's fixes. Celebrían's parents (`characters/heroes.xml`, with Galadriel and
Celeborn older in `characters/lords.xml`), the Erebor and Eredluin child-education kits
(`equipmentsets/taom_education_equipment_templates.xml`), the Calradian political philosophy strings
(`module_strings.xslt`), and at review time the civilian sets of 47 dwarf troops. Branch `fix/silas-port`, on top of
9ee24bb5d, reviewed uncommitted. Tier 2: Lens 5 Data flow, Lens 7 XML and the Tooling lens at effort high. Codex
skipped (out of usage since 2026-11-15); its pass is owed before merge.

**Top line:** one HIGH. Trunk had already fixed the dwarf civilian sets in #1431 (fb5a0c563, the same day, 12:47) with
inline dwarf rosters, a `Realm.civilian_kits` field and a generator self-check. The worktree's base, 55806e3df, is
older, so the port fixed the same 47 references a second way, and the two fixes would have collided at the merge. The
item left the batch. The other three items passed all three lenses with 0 gaps.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Item 2 (47 dwarf civilian references, `Realm.civilian_set`, a test of the same rule) duplicated #1431 on trunk. At the merge, either trunk's generator self-check refuses the Ered Luin realm, or trunk's inline kits revert and its `--check` reports the file stale. Fixed: the three troop files, `realm_troops.py` and `generate_eredluin_troops.py` are back to HEAD, and the test method for them is gone. (Lens 7.) | Stale base | The batch compared Silas's items with the worktree, not with trunk. The worktree was 7 commits behind trunk, and fb5a0c563 was in the git log shown at session start. The port plan's comparison ran against trunk in the morning, before #1431 landed. | Before a batch in a long-lived worktree, run `git log HEAD..bannerlord-1.5.x -- <the files the batch will touch>` and read each hit. Lesson in `lessons/build-tooling-workflow.md`. |
| 2 | MED | The removed test method overlapped trunk's `DwarfCivilianKitTests`. Fixed with finding 1. (Lens 7.) | Stale base | Same as finding 1. | Same as finding 1. |
| 3 | LOW | The engine citations in `PoliticalPhilosophyStringsTests` pointed at the v1.5.6 dump and at the engine's merge loop. Fixed: v1.5.7 `LordConversationsCampaignBehavior` :77-87 (read this session) and TAOM's `XmlMergeService.MergeFast` :79-88, which replaces the loop. (Lens 7.) | Citation | The lookup was found by a grep of the dump, and the installed DLL was read only after. | None new: AGENTS.md "Research first" already says signatures come from the installed DLLs. |
| 4 | LOW | `tools/README.md` and the `realm_troops.py` docstring did not name `civilian_set`. Moot: the field left with finding 1. (Tooling lens.) | Docs | | None. |
| 5 | LOW | An education kit is chosen by the child's culture, while its fit depends on race (`EducationCampaignBehavior`, v1.5.7). A human child with Erebor culture now previews dwarf clothes; a dwarf child of another culture still previews human cloth. The kit is a preview only, and dwarf parents with a dwarf child are the common case. Existed before this change. (Lens 5.) | Data model | | `no issue:` display-only and rare, left as it is. |

## Why each agent missed these

- Finding 1 is not a code fault that a lens reads in the diff; Lens 7 found it because its method compares the
  branch with trunk. Lens 5 and the Tooling lens read only the worktree, as briefed. The brief said the worktree, not
  trunk, so the orchestrator's brief carried the stale premise.

## Other results

- Lens 5: 9 flows traced, 0 gaps. Celebrían's parents give her ancestral claims on Lothlórien titles when they die
  (`RealmsService.cs:160-168`) and lower Galadriel's daily pregnancy chance (one child now); both fit canon. The two
  older faces are the only visible cost of the age change. No age model kills an elf of 35 (`TaomAgeModel`).
- Lens 7: all 6 kit items exist in the live Armory; the engine reads the kits only for the education preview, so no
  Civilian item flag is needed. No other file reads `str_political_philosophy_*`.
- Silas's wanderer backstory rewrite was not ported: `lords.xslt` (#758) already takes vanilla wanderers out of the
  spawn pool.
