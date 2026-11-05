# RCA: Ered Luin deep review, 2026-10-27

## Top line

`/deep-review` at tier 1 (live `TAOM_Map` data, C# lists, generators) ran Codex at `ultra` as the
Step 0 pre-review, then one wave of four lenses: 5 Data flow, 2 Engine compatibility, 7 XML and
ModuleData, and Tooling. Every lens finding then went to one skeptic `deep-reviewer` prompted to
refute it. Codex found no CRITICAL or HIGH: one MEDIUM the change introduced and one MEDIUM in a tool
the change touched. The lenses reported 19 findings: 17 confirmed (2 MEDIUM, 13 LOW, 2 NIT) and 2
refuted. No CRITICAL or HIGH, so no second wave ran. Every confirmed finding is fixed in this change
except four that are recorded as follow-ups with their reason.

The work is the new dwarf kingdom of Ered Luin ([ered-luin.md](../features/ered-luin.md)),
uncommitted on `feat/ered-luin-kingdom` over `aa708f620` during the review.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Ered Luin's troops wore 50 shared Erebor and Iron Hills armour pieces below the band Erebor wears them at, so the class table moved them down (26 elite to heavy, 14 out of the armoury gate) and re-linked 32 upgrade chains (lens 7, XML-1) | Borrowed data re-anchored | The tree was dressed by the id tier word, which `ARMOUR_MESH_TIER_LADDER` checks. The class table bands a piece by its lowest wearer, a rule no gate compares across kingdoms. The `ARMOUR_CLASS_TABLE_DRIFT` warning that showed it was answered by `--apply` twice, once by the orchestrator and once by the integration checker, whose brief said to re-run on drift. **Repeat** of #978 finding 2 (one Gondor bracer re-banded by Cardolan, accepted then) | Re-dressed in `generate_eredluin_troops.py`; `armour_classes.xml` now equals the base commit. Gate `BorrowedArmourAnchorTests`. Lesson: a drift warning after a roster change is a finding to read, not a table to regenerate |
| 2 | MED | The live `TAOM_Map` names `Culture.eredluin` and four clans that the deployed and released TAOM lack, and no release check records the pair (lens 5, DF-1) | Two-module coupling | The live map is unversioned and was written before the culture existed, as for Cardolan; the release checklist has one hand-written line per realm (Arthedain, Dale) | Ered Luin line in `release-process.md`; Known gaps say no `TAOM_Map` package from the live install until this branch ships; the branch build is deployed. Follow-up closed 2026-11-04 (002f8ff7e, 334c187e4: tools/check_release_pair.py) no issue: the generic per-channel validator gate the lens proposed is tooling work for Mike to schedule |
| 3 | MED | The class table's successor rule offered two paid upgrades that only lowered arm armour and added weight (Codex F1) | Derived table semantics | `pick_next` chose by class and id only | `generate_armour_classes.py` skips a successor below lord that is no better on any armour value and worse on one; elite-to-lord forge paths exempt; 8 tests. Finding 1's re-dress then removed both rows' cause |
| 4 | MED, pre-existing | `insert_new_faction_cc_menus.py` wrote each menu inside its loop, before the strings file was built (Codex P1) | Write order | The builder added a parse per file, not all outputs before any write | `prepare_cc_strings` and one write block after everything parses; test `test_apply_writes_nothing_when_a_later_output_fails` |
| 5 | LOW | The Mattock Warrior carried Dáin's hammer, a `<NamedWeapons>` hero item (XML-3) | Named item on a troop | Weapons were chosen by DPS band; the named-weapon list was not read | Erebor's two-handed mace (54.5 DPS, in band) at first; the convergence review found it under both of the Mattock Hewer's weapons (54.5 against 62.9 and 54.6 DPS), so both rosters now carry Erebor's heavier two-handed axe (72.7 DPS, in band). Follow-up closed 2026-11-04 (c30c4e618) no issue: a `realm_troops.self_check` rule against `<NamedWeapons>` ids is a small tool change for the next realm |
| 6 | LOW | The hair rationale (an unmatched tag falls back to Bald) is wrong for `fighter_erebor`, whose keys carry hair indices 1 and 10 (lens 2, F1) | Precedent copied across templates | The Cardolan rationale was copied for a different race and template without re-deriving it | Doc and generator comment corrected; the tag kept as a style choice. Lesson: re-derive a precedent's mechanism for a new template |
| 7 | LOW | The doc said the unused dwarf town scene needs a `project.mbproj` row; no module registers scenes there (lens 2, F2) | Wrong cause stated | Assumed, not read | Corrected; binding it to Thorin's Halls is an open decision for Mike |
| 8 | LOW | The in-game checklist had no navmesh island audit for 31 new settlements (lens 2, F3) | Checklist gap | The Arnor recipes lacked it, and the root recipe (`settlements.md` "Add") did too | Added at the root, `settlements.md` "Add" step 7, before the cache rebuild, and in `ered-luin.md`; `module-map.md` and the naming doc now say to fix a coordinate in the editor, not in the file alone |
| 9 | LOW | Ered Luin's troop design and the new `realm_troops` options had no unit test (Tooling A) | Untested opt-ins | The byte-equal `--check` was taken as coverage | `DwarfRealmOptionTests`, Ered Luin in the shared suite, a launcher-aware ladder test |
| 10 | LOW | Two runbooks still ran `insert_new_faction_cc_menus.py` bare after its bare run became a dry run (Tooling B) | CLI contract change | Its docs were not grepped for invocations | `--apply` in both; `validation-and-testing.md`'s no-dry-run list re-measured (7 of 10) |
| 11 | LOW | `insert_new_faction_careers.py` splices had no match count and `--check` read one substring (Tooling C) | Unchecked splice | New tool modes were tested on shipped data only | Anchors counted before any write; refusal tests on temp copies; `--check` documented as "would `--apply` write or refuse" |
| 12 | LOW | A CC clone source with no entries was a warning and exit 0 (Tooling D) | Silent pass | The branch predates the new `--check` | A hard failure in every mode, with a test |
| 13 | LOW, pre-existing | `RELEASED` in `add_map_fortifications.py` was never filled, so the save-order check judged all 139 rows, not the unreleased batch (Tooling E) | Hand-kept record | No release step fills it | Filled with the 13 ids the published testing package (v2.0.35) carries; the check now judges a 92-row batch headed by `town_CA2`; test added |
| 14 | LOW, pre-existing | `rebalance_ranged_ladders.py`'s default "read-only" run rewrites the tracked `docs/reference/ranged-troops.html` (Tooling J) | Misnamed default | A tracked output on the default path | The regenerated report is kept (it adds the Cardolan, Rhudaur and Ered Luin rows). Follow-up closed 2026-11-04 (c30c4e618) no issue: make the tracked copy opt-in |
| 15 | LOW, deferred | The cloned careers show no ability icon and the Arbalester no portrait; true for all 30 cloned careers (DF-2) | Art binding by id | Pre-existing: the screen builds the icon name from the template id | Known gap. Follow-up closed 2026-11-04 (4d9ae0e8a; the Arbalester portrait stays under #1058) no issue: the lens's data-driven sprite proposal is a C# change across 30 careers |
| 16 | LOW, deferred | Two mercenary and outlaw templates and two `_e` lord rosters nothing binds (XML-2) | Dead data | The same shape in every generated realm | Known gap. Follow-up closed 2026-11-04 (14fcc5f33, 17b738aa6) no issue: one cleanup across all realms |
| 17 | LOW, deferred | The six age-8 education tutors carry no race and render as humans (XML-4) | Clone of a gap | Erebor's six have the same gap | Known gap. Follow-up closed 2026-11-04 (Mike: not fixed, LOW, and Erebor has the same gap): the skeptic showed a data-only fix puts a dwarf skeleton under a human skin; the fix is an engine-side patch |
| 18 | NIT | The doc said the name pool avoids Erebor's; Farin and Hanar are in Erebor's culture pool (XML-5) | Claim not checked | Written from intent | Comment and doc corrected; parent default names left (only StoryMode reads them) |
| 19 | NIT | `check_generator_item_refs.py` does not list the realm troop generators (XML-6) | Gate coverage note | A "run" entry would check nothing for them | A comment saying why `--check` plus `BROKEN_ITEM_REF` covers them |

Refuted by the skeptics: DF-3 (the retired-item gate cannot read a realm generator's output, so registering it adds nothing) and Tooling F (the fortification writer's BR and IT names are tagged by `name_tags.py`, which the integration step ran).

## Root-cause patterns

**A borrowed asset carries its owner's rules (findings 1, 5, 17).** Ered Luin borrowed Erebor's
armour, weapons and tutors. Each borrow passed the gates that check the borrower (tier words, DPS
bands, references) and broke a rule that belongs to the owner: the owner's armour band, the owner's
named weapon, the owner's own gap. Finding 1 is the second time a new realm re-banded a donor's
armour; the first was accepted as data.

**A drift warning answered by regeneration (finding 1).** `ARMOUR_CLASS_TABLE_DRIFT` fired after the
roster change, and the table was regenerated twice without reading what moved. The brief for the
integration step encoded that response. A derived table's drift after a roster edit is the effect of
the edit, and its diff against the base is the review of that edit.

## Why each reviewer found what it found

- **Lens 7 (XML)** found finding 1 by diffing `armour_classes.xml` against the base commit and
  regenerating it without `troops_eredluin.xml`, which reproduced the base row for row.
- **Codex** found finding 3, two of finding 1's symptoms (the downgrade edges), but not their cause.
- **Lens 5 (Data flow)** found finding 2 by following the live map's references into the deployed
  module and the release channels.
- **Lens 2 (Engine)** found findings 6 to 8 by tracing the native hair roll and the scene loader.
- **Tooling** found findings 9 to 14 by running each changed tool against broken inputs.
- The four builders and the integration checker did not see finding 1: their gates were green, and the
  drift warning was handled as a regeneration step.

## Process notes

- At 20:38 another session's v2.0.36 release checked out `bannerlord-1.5.x` in the main checkout while
  this work was uncommitted. The work rode along with no conflict and was moved back to the branch,
  now based on `aa708f620`.
- This work first dated itself 2026-10-28; the local date was 2026-10-27. 113 stamps in 17 files were
  corrected before the review.
- `gh issue create` was refused by the session's auto mode as an external write; the issue body is
  drafted for Mike.
- A convergence pass over the fixes found 1 MEDIUM (a doc rewrite spliced at offsets computed
  before an earlier replace, which cut two sentences), 4 LOW and 5 NIT. A check of those fixes found
  3 LOW and 7 NIT, among them `BorrowedArmourAnchorTests` reading rosters with its own copy of the
  class table's reader, which left out the noble anchor and the exempt (troop, item) pairs. A
  follow-up check found the fix untested and four NITs. All are fixed in this change.

## Follow-up

Each `FOLLOW-UP` above names no issue because creating issues needs Mike's word this session; the
list is in the commit body and in [ered-luin.md](../features/ered-luin.md) "Known gaps".

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)

<!-- backlinks-end -->
