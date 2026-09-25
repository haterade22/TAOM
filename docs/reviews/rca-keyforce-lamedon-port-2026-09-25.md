# RCA: porting KEYforce's Lamedon drop, and the noble-line ladders (deep review, 2026-09-25)

## Top-line

Port of KEYforce's lotraom-assets commit `429746b2` ("Fixes and Lamedon") into TAOM (#669), reviewed by
`/deep-review` in three waves on the `deep-reviewer` definition (Opus 5.5, max effort): Standards, Engine
compatibility, Data flow and XML; then Completeness, Design, Tooling and a fix-loop Data flow plus XML
pass; then Efficiency and one convergence pass. Mike's live instruction mid-review ("fix any missing or
misspelled mesh ids or bo or bo cap ids") added the Armory reference repair, and his balance decisions
(nobles wear better kit than regulars; hand decisions as pairs; restat the blades) added the noble-line
ladder rules and three live restats.

**Four HIGH findings, all confirmed by re-reading the data or code, all fixed or decided:** the port
reverted three of our balance passes; the merge written to undo that lost 16 of our fields; the first
noble rule priced noble kit below regular kit; and KEYforce's deletion of `gondor_ring_peasant` breaks
saves (Mike accepted the break). Three of the four were introduced by the orchestrator while fixing the
change, not by the change itself, and each was caught one review wave later.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | A verbatim copy of `troops_gondor.xml` and `troops_umbar.xml` reverted #609, #617 and #631 (208 Gondor fields, 32 Umbar) | data merge | The #568 lesson says "copy verbatim only where the BASE equals HEAD". The orchestrator took "base" to be the commit's parent (Mike's "Full Update of TAOM" sync, which did equal HEAD); KEYforce's working copy derived from the mirror's 2026-09-15 revision, and his commit overwrote the sync | **Repeat offender.** Lesson rewritten with the exact test (the file revision with the smallest diff to the artist's file is his base, not the parent), plus a line in the orientation trap index |
| 2 | HIGH | The slot-level merge mapped KEYforce's copied roster `i` to base roster `i mod n`; he had fanned rosters out as `[r0, r0, r1, r1]` in 34 cases, so 16 of our fields stayed at old values | tooling | The merge was self-checked with an analyzer that used the same mapping, so the check agreed with the bug by construction | Lesson: verify a transform with an independent method (here, matching rosters by content); a check that shares the transform's assumption proves nothing |
| 3 | HIGH | Exempting the 85 nobles from the ladder priced their kit by the id keyword, which put most `_med_`/`_heavy_` noble pieces below a regular of the same level (Ithil Guard armour 57 to 36), and hid Gondor tiers 3 to 7 from the gates | design | Only the one case that motivated the rule (the Ringlo militia, id tier above its band) was tested; the property Mike asked for (nobles above regulars) was not measured before the live restat | Lesson: before any live restat, measure the property the rule promises on the dry-run numbers. Replaced by `_NOBLE_LINE_TROOPS` in the tools themselves |
| 4 | HIGH | `gondor_ring_peasant` deleted; saves holding it keep a hollow `CharacterObject` that vanilla's daily ticks read | save compatibility | External data; `.claude/rules/troops.md` says never delete a troop, but nothing machine-checks that a shipped troop id survives | Mike accepted the break (CHANGELOG known limitation). Patch83's apparent blind spot filed as #670 |
| 5 | MED | The noble band-up anchor dragged a regular heavy helmet worn by a level-11 noble from 43 to 33 for every regular wearer | design | Found by the orchestrator's own median check after the second restat | Floor at the item's own tier (`noble_anchor_level(level, item_id)`), with a test |
| 6 | MED | Whole-troop hand exemptions repriced every other slot of those troops (the Osgiliath chest 63 to 47) | design | Exemption granularity chosen for convenience | `_ARMOUR_LADDER_EXEMPT_ITEMS` (troop, item) pairs, with tests |
| 7 | MED | Seven of the new rules could regress with every test green (mutation check) | testing | Tests were written for the motivating case of each rule, not for what a regression would look like | Eight killing tests; all seven mutants now fail. Lesson: mutation-check new rules |
| 8 | MED | Dol Amroth shield plus two-handed greatsword (four troops) | data | The polearm audit only warns on two-handed swords | Mike kept KEYforce's kit; recorded in the CHANGELOG |
| 9 | MED | Three Belfalas cape items and 55 other Armory refs named meshes no package ships (invisible items, no body) | Armory art drop | The drop renamed or omitted meshes; XML-only drops do not raise the session banner | Repointed; audit CLEAN; art asks in #672; drift gate in #673 |
| 10 | MED | `generate_gondor_troops.py` still wrote three retired spears | generator drift | Pre-existing since the Armory sync | Replacement ids |
| 11 | MED | The melee roster pass put a heavy poleaxe on tier-5 archers, which then anchored the shared blade and dropped it for the Ringlo nobles | tooling | Pass 1 offers any in-band weapon of the culture; shared blades are priced by their lowest wearer | Reverted by hand; documented in `melee-damage-model.md`; loop stopped with 10 suggestions unapplied |
| 12 | LOW | `lord_d` repointed to `lord_az`, which is not a mesh (a `strings` scan read a length-prefixed name plus a byte) | tooling | Mesh names taken from `strings` on a tpac instead of the catalogue or `validate_mesh_refs.scan_tpac_metameshes` | Lesson; re-apply script corrected |
| 13 | LOW | The borrowed `lord_c` mesh kept `lord_d`'s beard cover (`type1` against `all`) | data | Repointing a mesh does not carry the donor's cover attributes | Set to match |
| 14 | LOW | A wave-1 reviewer ran `rebalance_ranged_ladders.py` without `--apply` and it rewrote the tracked `ranged-troops.html` | review process | Several tools write by default; the lens brief did not say which | Restored; waves 2 and 3 were briefed with the list; lesson |
| 15 | LOW | The CHANGELOG backup path lost a backslash-a to a BEL byte | tooling | A Python edit sent through a Bash heredoc (the CLAUDE.md "heredocs mangle backslashes" trap) | **Repeat offender**; fixed from a script file |
| 16 | LOW | CHANGELOG and doc counts wrong three times (977/208/113, "1,157", "85 nobles", "15 blades", "since v2.0.25") | evidence | Summaries written from numbers a flawed tool produced, or before the proving run | `evidence-over-claims.md` C.1 applies; every number re-derived in wave 3 |
| 17 | LOW | The fixer ranked a noble's candidates at the unfloored band but placed them at the floored one | tooling | Two call sites computing the same level differently | One function; test |

## Root-cause pattern

Findings 2, 3, 5 and 7 share one shape: **a rule was verified on the example that motivated it, not on
the property it promises.** The merge was checked by its own mapping, the noble exemption by the Ringlo
militia, the band-up anchor by nobles in their own kit, the tests by the case each rule was written for.
Each time, the next measurement of the actual property (content-matched rosters, noble against regular
medians, a shared regular item, a mutant) found the defect in minutes. The same session also shows the
fix: once the orchestrator measured "nobles above regulars" directly, findings 5 and the restat's
regressions surfaced before any reviewer saw them.

## Why each agent missed what it missed

- **Wave 1 (Standards, Engine, Data flow, XML)** reviewed the verbatim copy; the XML lens found finding 1
  by comparing three ways against the mirror history, which is the check the orchestrator skipped.
- **Wave 2** reviewed the merge and the exemption; the Tooling lens found finding 2 by matching rosters
  by content, the Design and fix-loop lenses found finding 3 by pricing every noble-only item.
- **Wave 3** found finding 7 by mutation and finding 17 in the fixer; nothing it found was HIGH.
- No lens could have caught finding 4 as a defect of this change's making; it was a decision.

## Feedback memories to codify

None new. The lesson entries carry the rules; the two repeat offenders (the #568 base check and the
heredoc trap) get stronger placement: the base check becomes an orientation trap-index line, and the
heredoc trap is already in CLAUDE.md's MCP and shell section.
