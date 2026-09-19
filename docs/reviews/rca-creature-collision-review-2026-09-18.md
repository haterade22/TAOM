# RCA: deep review of the 2026-09-18 creature session (ram single target, elephant collision, hit-capsule tool)

**Scope.** Everything uncommitted from the session after the war ram binding review
([rca-war-ram-headbutt-2026-09-18.md](rca-war-ram-headbutt-2026-09-18.md)): the ram's single-target head-butt and
40-50 damage (#618), the barding team colours, the elephant body capsule and per-bone hit capsules (#624),
`tools/skeleton_hit_capsules.py`, the Yotthani handoff adoption, and the docs. Eight lenses ran on the
`deep-reviewer` agent, two at a time: data flow, XML, standards, engine compatibility, completeness, tooling,
efficiency, design. **No CRITICAL or HIGH.** Every confirmed finding was fixed in the same session unless the table
says FOLLOW-UP; the full C# suite (9,799 passed, 2 skipped) and tools suite (1,725) were green after the fixes.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The ram's drift guard caught a missing action TYPE only; a typed action whose binding or clip package is gone resolves to a valid index and plays nothing | data flow | The guard was written for the failure seen before (an unresolved name) and never asked what the other half of the contract (binding, clip) looks like when it breaks | Guard extended: `MBActionSet.GetActionSet(ActionSetId)` then `CheckActionAnimationClipExists`; `ActionSetId` pinned by a test |
| 2 | MED | A single-target pick could, per the engine reading, choose an enemy's ridden horse over its rider | data flow / engine | The single-target branch reused the area branch's filter, where hitting a horse too was harmless | Ridden mounts skipped in the single-target branch; the blow log (area version hit mounted lords twice, never their horses) says it was already rare |
| 3 | MED | A pre-existing wrong engine fact in ten places: `IsInBeingStruckAction` uses the half-open `MBMath.IsBetween(type, 48, 52)`, so `MountStrike` (52) is NOT in the being-struck band | engine | The 2026-08-28 review reasoned from the constants' names (`StrikeBegin` 48, `StrikeEnd` 52) and never read `IsBetween`'s body; the claim then propagated into a lesson, the handbook and CLAUDE.md, where later readers trusted it | Corrected everywhere; historical records annotated. Lesson: read a range check's body before quoting its bounds |
| 4 | MED | Stale mirror: the repo snapshot of `action_types.xml` lacked today's action and a 2026-08-28 block while the `action_sets.xml` snapshot was current | completeness | The snapshot refresh copied the file that was edited in the same step and not its partner | Byte copy of live; the partner-file rule is the lesson below |
| 5 | MED | Pre-existing: three dead `family_type` attributes on `<Horse>` failed the engine schema on every load | XML | Nobody ran `validate_xml_schemas.py` on the live horses file until the XML lens did; the gate is new (#621) | Removed (backup kept); the file now passes the schema gate |
| 6 | MED | `check_rdc_entries.py` exited 0 after checking nothing (misspelled `--under`, or only `_mtl` packages) | tooling | Silent-clean class, already in the tooling lens checklist; the tool predates the lens | Exit 1 on a missing folder or zero packages; two tests |
| 7 | MED | `skeleton_hit_capsules.py`'s running-game guard returned "not running" when `tasklist` could not run, so `--apply` wrote the live Armory | tooling | The guard was written as a convenience check, not as a gate, so its failure path defaulted to "go" | Fails closed, CSV output; three tests |
| 8 | MED | No GitHub issue for the elephant collision work; #618's body predated the single-target work | completeness | The work grew inside a session that already had an issue for the ram | #624 filed (retroactive), #618 comment |
| 9 | MED | No provenance row for the Yotthani handoff / MithrilForge | completeness | `/adopt-external` does not list the register among its outputs | Row + detail added; the adoption doc's cycle should name it (FOLLOW-UP to `external-repo-adoption.md`, not done here) |
| 10 | MED | No in-repo gate for the barding team-colour flags in the unversioned Armory | completeness | The CLAUDE.md trap "land an in-repo gate beside an external edit" was not applied to a data-only change Mike made himself | `tools/tests/test_live_ram_bardings.py`, proven both ways |
| 11 | LOW | Claims ahead of evidence: "the confirmed slide mechanism" (the elephant doc itself says unconfirmed), "its one base-game caller" (13, all navmesh ids), "applied" before the Kit re-cook, "130 entries" (165, 130 with a window), hit-vs-ragdoll semantics and the `UnknownUInt2` rule stated as fact | engine | Repeat of the morning RCA's F3 class (a claim ahead of its measurement); the words were copied from earlier summaries instead of re-read from their source | All hedged or corrected with the evidence named |
| 12 | LOW | Stale comments by old value: the ram tree's 6 s cooldown and auto-tick, the Monster header still justifying `as_horse`, `WarRamCombat`'s "vanilla as_horse clips", a README cell on RDC validity, a test header's frame count | standards / design | Repeat of the morning RCA's F1 class (sweep by the new symbol, not the old value) | Fixed; the morning lesson already covers it, so no new rule |
| 13 | LOW | The attack task kept three parallel lists, a second pass and a `Clear()` whose comment claimed a benefit it did not deliver | design / efficiency | The pure picker was designed as a list function for testability before the loop existed | Replaced by the `SingleVictimPick` comparator in one pass; same pick, 6 tests |
| 14 | LOW | Tool refusals missing: a fit for another skeleton, a misspelled `--mesh`, duplicate stripped bone names; read-back compared the radius only | tooling | Only the happy path and the hash refusal were designed | Four refusals, four-field read-back, tests for each |
| 15 | LOW | Skill sweep command omitted `TAOM_Map/SubModule.xml` while the next paragraph said to add it | standards | Edited one bullet without reading the committed sentence after it | Folded into the command |

FOLLOW-UP, not applied (pre-existing or out of scope): a shared `set_segment_payload` helper for the two tpac
writers (design); exception wrapping in `skeleton_hit_capsules.main` (tooling); the dry-run FBX location in
`fbx_remap_materials.py` (tooling, needs a Kit measurement); a ram section in `audit_mount_parity.py` (XML); the
stale "READY-TO-DROP" header of the repo's elephant reference copy (completeness). NOT APPLIED: moving the skill's
two incident narratives into the lessons file (the skill body loads only on invocation, and the reason is what
stops the mistake).

## Root-cause patterns

- **Claims outlive their evidence (findings 3, 11).** Both a months-old engine fact and today's summaries were
  repeated from earlier text rather than re-read from source. The morning RCA named the same class. The difference
  this time: finding 3 had passed a review, so its authority grew with every copy.
- **The partner of the thing you edited (findings 1, 4, 10).** The guard covered the half that broke before, the
  snapshot refresh covered the file edited in that step, the gate rule was applied to code and not to a data edit.
- **Gates that say yes when they could not check (findings 6, 7).** Already in the tooling lens; both tools predate
  it or were written as conveniences.

## Why each lens caught what it did

The data-flow lens found 1 and 2 by tracing the action name into the engine and the candidate set into the pick;
the engine lens found 3 by reading `MBMath.IsBetween`, and the caller count and overstatements in 11; the XML lens
found 4 and 5 by running the schema gate and diffing the snapshots; the tooling lens found 6, 7 and 14; the
completeness lens found 8 to 10; standards and design found 12, 13 and 15. The morning review had not seen any of
this code.

## Lessons appended

`docs/reviews/lessons/adapters-taleworlds-api.md`: read a range check's body before quoting its bounds.
