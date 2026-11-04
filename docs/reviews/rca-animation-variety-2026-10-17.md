# RCA: Animation variety and the orc gait revision (2026-10-17/18)

**Summary.** One uncommitted working tree on `bannerlord-1.5.x` (HEAD `4bd4f3e54`) held two pieces of work: the
`AnimationVariety` feature module (random step size and gait set per orc and goblin warrior, plus a logging-only swing
probe) and the orc gait revision for #791 (a restyled main orc, two per-soldier variants B and C, and the restyle,
validator, clip generator, binder and viewer tools behind them). Codex counted 46 files: 18 modified tracked and 28
untracked. Eight Claude lenses ran first: standards, engine compatibility, efficiency, completeness, data flow and
design (six general lenses), then two tooling lenses (A: restyle, validator, generator; B: binder, viewer, the stage-1
copy script). Codex gpt-6-astra at max effort (421,775 tokens) ran last on the fixed tree.

- **The one HIGH:** the restyle's `min_track_half_m` clamp measured from the current frame's pelvis, so planted ankles
  slid sideways (row 1). The validator passed all 36 clips with it because its slide gate compared mismatched frames
  (row 2). Both were found by the Claude tooling lens; Codex found neither.
- **Codex:** 0 CRITICAL, 0 HIGH, 2 MEDIUM, 1 LOW, 0 false positives (rows 16, 22, 36). It disputed the recycled-slot
  suspect correctly and found no ordering defect in the `OnAgentBuild` swap.
- **Second pass (2026-10-18):** a focused Codex pass on the fixes (307,765 tokens) found 4 MEDIUM and 2 LOW defects in
  them, one a regression the first fixes introduced (rows 42 to 48), and a convergence pass added further LOW items (rows
  50 to 56). All are fixed; see "Second pass" below.
- **Third pass (2026-10-18):** a convergence-only pass on the second-round fixes found a HIGH regression the second
  round introduced in the validator (row 57) and eight smaller items (rows 58 to 65). All are fixed; see "Third pass"
  below.
- **Outcome:** every confirmed finding is fixed in the working tree except the ones marked below (row 12 is a
  follow-up, row 14 is a scratch script, rows 32 and 33 were declined or not applied with a reason, row 37 stays
  UNVERIFIED). Fixes are not yet committed; the reviews are tied to the working tree, not to a commit.
- **After the fixes (reported by the orchestrator, not re-run for this document):** tool tests 277 passed (+49
  subtests), C# feature tests 289 of 289, 100% line and branch coverage of `AnimVarietyService`, `GaitVarietyService` and
  `GaitVariantCatalog`. All 36 orc clips were re-exported on 2026-10-18 (main 16 of 16, B 10 of 10, C 10 of 10 pass the
  validator) and copied to the live Armory `AssetSources` at 00:02, hash-verified. After the second pass the clips were
  exported and copied again (rows 42 to 56); the figures for that state are in the "Second pass" section.

The raw Codex output is `docs/reviews/raw/codex-adversarial-animation-variety-2026-10-17.md` (gitignored); the prompt is
`docs/reviews/codex-adversarial-animation-variety-2026-10-17.prompt.md`. The feature doc is
[animation-variety.md](../features/animation-variety.md); the clip half is
[orc-goblin-locomotion.md](../features/orc-goblin-locomotion.md).

## Findings

Severity is the finder's. "Lens" names who found it. Rows 1 to 14 are the restyle, validator and generator; 15 to 20 the
binder; 21 to 33 the C# feature and probe; 34 to 37 engine claims and the MCM hint; 38 to 41 completeness.

### Restyle, validator and generator

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The limp restyle's `min_track_half_m` clamp kept each ankle a minimum distance from the CURRENT frame's pelvis x. `weight_over_foot` and the sway move the pelvis toward the stance foot, so the clamp moved with it and pushed planted ankles sideways: the main orc walk's right foot moved sideways at 1.42 cm per frame against 0.31 in the source (2.3 cm per stance). Lens: tooling A. The design lens corrected the planned fix: the clamp, the achieved metric and the validator gate must share ONE reference. | A bound measured from the quantity it protects (a moving reference) | The clamp, the achieved metric and the validator gate did not share one fixed line, and the validator's slide gate was too loose to see the result (row 2). | The clamp measures from the clip's mean pelvis line (`track_centre_x_m`, written to the report); the achieved metric and the validator gate read the same value. The main walk's right foot now moves at 0.18 cm per frame laterally against 0.31 in the source. Lesson in `lessons/animation-skeleton.md`. |
| 2 | HIGH (same cause as 1) | The validator's limp slide check compared the clip's contact PLATEAU frames with the source's WHOLE contact (heel strike and toe-off included), about twice too loose, using the intervals the restyle itself reported. All 36 clips passed on 2026-10-17 with row 1 in them. Lens: tooling A, which ported the sampler to numpy to re-measure. | A gate judged on the producer's own intervals, over mismatched frames | The validator had no tests (row 3) and nothing re-measured a clip with an independent sampler. Codex's S5 noted that the validator "shares producer intervals" but marked skating UNVERIFIED and did not measure it. | Round 1: plateau against plateau, plus lateral and vertical ankle speed gates, on the frames the report listed; pure tests in `tools/tests/test_validate_locomotion_clips_pure.py`. Round 1 still trusted the report's frame selection, which the second pass showed (row 42); the intervals are now found independently. Lesson in `lessons/animation-skeleton.md`. |
| 3 | MED | `validate_locomotion_clips.py` had no tests (tooling A; completeness lens). | An untested gate | Not recorded. The validator's 36 passes were read as evidence about the clips, not about the validator. | `test_validate_locomotion_clips_pure.py` (contact statistics, plateau slide, lateral and vertical speed). |
| 4 | MED | `gen_troll_anim_clips.ps1 -CloneByName` took `-TravelScale` typed by hand and never compared it with the restyle report's `stride_scale` (tooling A and data flow I2). A hand-typed 1.377 once skated the troll's feet by 12%. | An input that must match a report is typed beside it | The Fab path already had this guard (`-RetargetReport`); the clone-by-name path did not copy it. | `-RestyleReport` sets `-TravelScale` from the report; a typed value must match within 0.001 or the run is refused. |
| 5 | MED | `-StepPointsFrom` validated nothing. A stale report (the 2026-10-14 orc report predates step points) gave 0 clips, and `-Verify` passed with "step points checked=0" (tooling B, T-B1). | A check that passes when it checked nothing | `-Verify` counted failures, not coverage, so a report that matched no clip had nothing to fail. | Renamed `-RestyleReport`. The report is validated before anything is written (clip prefix, an entry per index clip, no error entry, a good export, two step points per gait clip, one `stride_scale`); `-Verify` fails a clip as STEP or LOOP and fails the run when it checked no step point. Lesson in `lessons/build-tooling-workflow.md`. |
| 6 | LOW | The mapped step points were sorted, so a point that wrapped past the loop seam could swap X and Y (tooling A; data flow I5). | A reorder hides a mapping | Not recorded. | Mapped in place; the entry carries `step_points_wrapped`. |
| 7 | LOW | An explicit `--vanilla-step-points` path that did not exist was ignored without an error (tooling A). | A silent default on an explicit input | Not recorded. | An explicit path that does not exist exits with an error; only the unset default is optional. |
| 8 | LOW | The `run` spec block had no value checks (tooling A). | Missing input validation | Not recorded. | Spec checks cover the run block. |
| 9 | LOW | The claim that the goblin output was unchanged by the new restyle features was not backed by a run (tooling A). The neck and head pitch moved to the body's side axis, which differs slightly for a vanilla neck that leans 5 to 7 degrees sideways. | A regression claim without a regression run | The test proved less than its name: that the goblin spec asks for none of the new features. | `ShippedDataTests.test_goblin_has_no_limp_run_or_clearance_block` states in its docstring that it proves the spec, not the output. |
| 10 | LOW | The limp's other keys were applied to idle clips, and a spec that mirrors the low shoulder (orc C) would flip the shoulder at every halt, because B and C inherit the main orc's idles (data flow I3). | A shared fallback inherits another variant's posture | The variants inherit the main orc's idles by design (the binder gives them no idle codes), and the limp was applied to every clip kind. | `limp_for_kind`: an idle gets only `arm_forward_deg`. Documented: a B or C soldier changes posture at a halt. |
| 11 | LOW | The validator's lead-shoulder side could be `None` (data flow I4). | An optional key read as required | Not recorded. | Fixed in the working tree. |
| 12 | LOW | NOT FIXED, a follow-up: with `knee_max_deg` set, the pelvis drop that keeps a leg inside its limit can jump near full reach (tooling A). | A clamp with a discontinuity | Not recorded. | Smooth cap and a pelvis-height step check in the validator, listed under "Known Follow-ups" in `orc-goblin-locomotion.md`. |
| 13 | LOW | Cleanups (tooling A) and a dead viewer function (tooling B, T-B5). | Dead code | Not recorded. | Removed. |
| 14 | LOW | The scratch stage-1 copy script's own game and Kit process guard failed open (tooling B, T-B6). The live write itself was verified byte for byte. | A guard that fails open | A one-off script wrote its own guard instead of importing the shared one. NOT FIXED in the repo: the script is not in it. | The rule for stage 2: import `_gamedir.game_or_kit_running`. Recorded as a follow-up in `orc-goblin-locomotion.md` and in the lesson in `lessons/build-tooling-workflow.md`. |

### Binder (`bind_orc_goblin_locomotion.py`)

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 15 | MED | Rewriting the variant block silently deleted any foreign set that sat between the markers (tooling B, T-B2). | A tool that rewrites a shared-file region deletes what it does not own | Not recorded. The rewrite treated the region between the markers as the tool's alone. | The rewrite refuses (exit 2, even with `--allow-shrink`) when it would delete a set the tool does not own. Lesson in `lessons/xslt-moduledata.md`. |
| 16 | MED | Codex F1: the "defined outside the block" guard ran only when no generated block existed. With a block present and an earlier hand-kept `as_orc_warrior_b`, both definitions survived, the checker read the later one and passed, and the engine merges a repeated `action_set` into the FIRST definition (`Module.cs:1472`), keeping the wrong `base_set`. Codex reproduced it in memory against the live XML (`duplicate_count 2`, `check_problems 0`). | A guard that is off in the state that matters | The guard sat on the first-insertion path only, and the checker built its id map with a dict comprehension, which keeps the last duplicate. | A repeated owned id, a variant outside the block and a second block are refused on every mode (exit 2, bytes unchanged) and fail `--check`, whether or not a block exists. |
| 17 | LOW | A row without a type raised a `TypeError` (tooling B, T-B3). | Unhandled malformed input | Not recorded. | Fixed (bad input exits 2). |
| 18 | LOW | Raw tabs in the file (tooling B, T-B4). | Whitespace drift | Not recorded. | Cleaned. |
| 19 | LOW | `--check` passed when the variants had vanished with their clips, which is what a reinstall leaves (data flow I8). | A check that passes when it checked nothing | A skipped variant and an absent variant looked alike; skipping was right before the import and wrong after it. | `--check --require-variants`: the reinstall gate once the orc variants are imported. Added to the `moduledata-validation.md` rule row. |
| 20 | LOW | `as_orc_female_warrior` and its `_b` and `_c` twins were unreachable: the orc Monster declares no `female_action_set` (`monsters.xml:419-420`), so a female orc spawns on `as_orc_warrior` (engine lens; data flow I6). The goblin Monster has one (`monsters.xml:1392`). | A set for a path nothing takes | Not recorded. | Orc female twins and the `as_orc_female_warrior` base row removed; the goblin twin mechanism stays. |

### C#: gait variety and the swing probe

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 21 | MED | The probe was armed by data alone: any loaded `act_taom_*` code started a per-frame sweep in every mission (efficiency E1). | Diagnostic cost with no off switch | The probe disarms itself when it finds no alternatives; with alternatives loaded it had no off switch. | MCM switch `EnableSwingAlternativeProbe`, default off, read once at the first tick; classified as instrumentation in `CoopSettingsRelevance`. |
| 22 | MED | Codex F2: `OnAgentHit` recorded the attacker's current channel-1 action and progress for a missile hit too, so a projectile that lands during a later swing or while idle polluted the hit-progress statistics. | A callback shared by two hit kinds | The `AttackRelease` gate used for swing counting was never applied to hit recording; `Mission.cs:5649` routes missiles through the same `OnAgentHit` path. | `blow.IsMissile` is checked in `OnAgentHit` and again in `RecordHit`, with a regression test. The first version of the early return still allocated a closure on every hit (row 48); the capture now lives in `RecordMeleeHit`. |
| 23 | LOW | The probe compared clip durations but not blend-out starts, which the hit timing also reads (engine lens). | Half a comparison | Not recorded. | `MBActionSet.GetActionBlendOutStartProgress` is compared when both clips exist. |
| 24 | LOW | A missing alt clip was worded "CRASH RISK"; it disables the group safely because the parser writes the slot only after the clip resolves (engine lens). | Wording stronger than the mechanism | Not recorded. The parser writes the slot only after the clip resolves. | Reworded to "group disabled for that code (not a crash risk)"; only a missing base clip is a crash risk. |
| 25 | LOW | The probe keys action sets on `GetHashCode()`, which equals the index only by implementation (engine lens). | An unpinned engine assumption | Nothing tested it. | A wiring test, tagged `RequiresGame`. |
| 26 | LOW | The SandBox settlement and disguise handlers replace the action set after `SpawnAgent`, so a town orc gets one wasted re-init and is still counted in `n=` (engine lens; data flow I7). | A later swap overrides the earlier one | The feature was designed for battle spawns, which pass no suffix. | Documented in `animation-variety.md` ("Interplay with later set swaps"); no code change. |
| 27 | LOW | The probe has never tracked anything: no `act_taom_*` swing code ships (data flow I9). | An untested diagnostic | Nothing to feed it. | Stated in the doc; stays off. |
| 28 | LOW | Untested probe skip guards (standards lens). | Test gap | Not recorded. | Tests; 100% line and branch on the three services. |
| 29 | LOW | `ArmorItemEndSlot` used where the code means the horse slot (standards lens). | A named constant that is not the slot | Not recorded. | `EquipmentIndex.Horse`, pinned by a wiring test. |
| 30 | LOW | Scenario-named tests (standards lens). | Test naming | Not recorded. | Renamed. |
| 31 | n/r | Efficiency E3 and E6, applied. | Small waste | Not recorded. | Applied. |
| 32 | n/r | NOT APPLIED: efficiency E2, E4 (an int key), E5, E7, T1 and T4. | Micro-optimisation | The probe is off by default, the savings are microseconds, and the premise of some was unverified. | None. |
| 33 | n/a | Design lens, 18 KEEP proposals: 17 applied (#18, reference identity for the probe's agent map, among them). #2, park the probe, was declined: Mike asked for the logging, and the native `IMBAnimation.GetActionCodeWithName` (0x6EAF80) is silent on a miss, so the probe's cost in a mission is 256 silent lookups. #1 corrected the HIGH fix of row 1. | Design review | n/a | Applied as listed. |

### Engine claims and the MCM hint

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 34 | MED | The docs said nothing re-syncs the walk phase. The native update (0x7496D0) holds a fully idle agent's phase at the forward clip's `bip_mov_ik` snapping start (decompile lines 352-367 and 715-720), so soldiers on one clip re-align at every halt; a transition pull exists (463-512, trigger UNVERIFIED), and 0x74BF60 (lines 49-51) lifts a below-floor rate toward the clip's own rate (engine lens F1). | An absence claim written from a partial read | The claim was written from the walking case; the same function (0x7496D0) holds the idle case. | The system map and `animation-variety.md` now say a step size difference builds only while soldiers walk and a halt re-aligns idle ones. Instance of the existing lesson "A listing cut short, or an empty lookup, is not evidence of absence". |
| 35 | MED | `GaitVariantCatalog` probed ids with `MBActionSet.GetActionSet`, whose miss path (`IMBActionSet.GetIndexWithID` 0x6DF7D0 to 0x590060) logs `Action set "%s" could not be found, using default action set!`. The map's triage row attributes that line only to a broken `base_set`, and `lessons/animation-skeleton.md` (2026-09-30) already recorded the miss path (engine lens F2). | A recorded lesson not applied, and a triage row that named one cause | The 2026-09-30 lesson was not applied when the catalog was written. | The catalog lists the engine's sets once (`GetNumberOfActionSets`, `GetActionSetWithIndex`, silent), keeps an ordinal `HashSet` and never warns. The triage row names both causes; the map notes `GetActionCodeWithName` returns -1 silently. Instance added under the 2026-09-30 lesson. |
| 36 | MED | The MCM hint said the 0.92 default "stays inside what the game shows" (vanilla 0.8 to 1.0), but the factor multiplies vanilla's `min(0.8 + 0.2 * Athletics / 300, 1)`: 0.736 at Athletics 0 and default, 0.64 at factor 0.80, 0.782 for Athletics 75 (data flow I1; engine lens; Codex F3 LOW). | A hint written from the formula's range, not its product | The hint compared the factor's default with vanilla's range of step sizes, but the factor multiplies that step size. | The hint states the real range; the feature doc spells out the arithmetic. The default did not change, so no persisted-default note applies. |
| 37 | UNVERIFIED | Codex S4: the `OnAgentHit` callback reads `Channel1ActionName` and `Channel1Progress` before deferring; managed getters do not establish native thread safety. Codex disputed the recycled-slot suspect: the closure holds captured values and the map is reference-keyed. | Unproven thread safety | Needs a native read. | No change. Stays UNVERIFIED in `animation-variety.md`. |

### Completeness

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 38 | MED | After the row 1 fix the staged FBX and the Armory copy were stale: the clips on disk still carried the slide (completeness lens). | A fix that does not reach the artifact | The code fix and the exported clips are separate steps. | Re-export of all 36 clips (16, 10, 10 pass) and a second hash-verified copy at 00:02 on 2026-10-18. Backup `orc_locomotion_20261018-000215` holds the superseded main FBX; `orc_locomotion_20261017-224440` stays the rollback to the clips the game plays. |
| 39 | LOW | Test gaps: probe coverage, the B and C specs, and `-StepPointsFrom` itself (completeness lens). | Test gap | Features and tools were built ahead of their tests. | Tests for each. |
| 40 | LOW | `tools/README.md` rows were stale (tooling B, T-B7). | Doc rot | Not recorded. | Updated. |
| 41 | n/a | Records missing: feature doc sections, the snapshot refresh step after the binder apply, the system map, this RCA, lessons and review logs (completeness lens). | Docs owed | The review ran before the records. | This documentation pack. |

## Codex review (gpt-6-astra at max, working tree at HEAD `4bd4f3e54`)

Codex reported 0 CRITICAL, 0 HIGH, 2 MEDIUM, 1 LOW and 0 false positives. Its report states the hashes of all 46
reviewed files matched at the start and the end, and that it made no edits. It ran 71 pure Python tests (all passed), 10
in-memory binder checks, the live `--check` and the live `audit_action_set_parity.py`; it ran no build, no .NET suite, no
Blender validation and no Kit import. The native decompiler could not start (its Python 3.13 path was inaccessible), so
Codex treated the system map's address claims as inputs.

- **F1 MEDIUM** (row 16): reproduced against the live `action_sets.xml` in memory.
- **F2 MEDIUM** (row 22): read `Mission.cs:5649` for the missile path.
- **F3 LOW** (row 36): computed 0.85 x 0.92 = 0.782 for `mordor_orc_warrior` (Athletics 75).
- **Suspects:** S1 (the `OnAgentBuild` swap) no ordering defect; S2 (seeds) normal consecutive-rank 500-agent examples
  stay distinct; S3 (catalog and names) agrees, `_dw` ordering UNVERIFIED; S4 (native reads on the hit callback)
  UNVERIFIED, recycled-slot disputed; S5 (slide assurance) LOW observation; S6 (binder) confirmed as F1.

## Root-cause patterns

- **(a) Measuring against a moving reference** (rows 1, 36). The clamp measured from a pelvis that the same pass moves;
  the MCM hint compared a factor with the range of the quantity it multiplies. A bound needs a reference the pass does not
  touch, and one reference shared by the clamp, the metric and the gate.
- **(b) A gate judged on the producer's own claims and mismatched frames cannot catch the producer** (row 2, with row
  3). The validator read the intervals the restyle reported and compared plateau frames with the source's whole contact.
  It passed 36 of 36 clips with a slide four times the source's. Round 1 compared like with like but still read the
  report's frame selection, which a second pass beat with an executed counterexample (row 42). What shipped in round 2
  is intervals the validator finds itself on the exported and the source poses, tests, and one independent
  re-measurement. Round 2 then narrowed the gate to the plateau it found, which by definition leaves out the frames a
  foot stalls, skids or lifts on (row 57): a gate that narrows what it judges must keep judging everything the producer
  claims.
- **(c) A check that passes when it checked nothing** (rows 5, 19, 14). `-Verify` with "step points checked=0", a
  binder `--check` that treats "variants vanished" like "variants not imported yet", a stage-1 guard that fails open. A
  run that checked zero items is a failure, and "absent" needs its own flag once absence is no longer normal.
- **(d) Engine claims written from a partial read, contradicting an existing lesson** (rows 34, 35). "Nothing re-syncs
  the phase" came from the walking case of a function whose idle case does the opposite; the `GetActionSet` miss path was
  already in `lessons/animation-skeleton.md` when the catalog was designed around it. Read the subsystem's lesson file
  before the design and the whole native function before an absence claim.
- **(e) A tool that rewrites a shared file region deletes what it does not own** (rows 15, 16). The binder's block
  rewrite removed a foreign set, and its ownership guard ran only when no block existed. A tool that owns a region checks
  the whole file on every run and refuses before it deletes.

## Why each agent missed these

- **The restyle and validator builder (rows 1 to 3, 5 to 9):** it checked each metric against its own definition of the
  planted foot. The clamp, the achieved metric and the gate were three measurements, not one, and the validator that
  should have been the independent check shared the producer's intervals and had no tests. The 36 of 36 pass on
  2026-10-17 read as proof; it was the same code looking at the same frames.
- **The binder (rows 15, 16, 19):** its cases covered normal generation, idempotence, nesting and the no-block
  ownership guard (Codex ran them and they passed). The states that broke it (a block present plus an outside duplicate;
  variants gone after a reinstall; a foreign set between markers) were later runs against an existing file.
- **The C# builder (rows 21 to 26, 34 to 36):** the native miss path, the idle phase hold and the Athletics product
  each sat one read deeper than the claim written about them. The probe had no off switch because it disarms itself
  when it finds nothing, which is not the same as costing nothing while armed.
- **The earlier self-checks:** the validator's pass, the generator's `-Verify`, the binder's `--check` and the
  feature's own tests all passed before the review. Each counted what it was given, not what it should have been given.
- **Codex:** it found the binder duplicate (a state-dependent guard) and the missile path (a shared callback), both by
  reproducing the engine's behaviour in memory. It did not find the HIGH or the validator's frames: it flagged the shared
  intervals as an observation and left skating UNVERIFIED, because it did not re-sample a clip. The Claude tooling lens
  did, by porting the sampler to numpy.
- **The six general lenses:** standards, engine, efficiency, completeness, data flow and design each caught their own
  class (the hint, the log line, the always-armed probe, the missing records, the unreachable twins, the reference
  identity). Only the tooling lens re-measured a produced clip.

## Feedback memories to codify

None beyond the lessons. The five patterns are recorded in `docs/reviews/lessons/` (`animation-skeleton.md`,
`build-tooling-workflow.md`, `xslt-moduledata.md`), where the next session reads them before touching these tools. The one
review-process change is for the prompt writer: for a validator, ask the reviewer to re-sample one produced clip with
independent code and compare the number; for a guard, ask which file state turns it off.

## Lessons recorded

- `docs/reviews/lessons/animation-skeleton.md`: a clamp measured from the quantity it protects (rows 1, 36); a gate on
  the producer's own intervals (rows 2, 3); an instance under "A listing cut short, or an empty lookup, is not evidence
  of absence" (row 34); an instance under "A managed validity check is only as good as the native value behind it" (row
  35).
- `docs/reviews/lessons/build-tooling-workflow.md`: an instance under "A fail-open guard whose failure mode is silence"
  (rows 5, 14, 19).
- `docs/reviews/lessons/xslt-moduledata.md`: a tool that owns a region of a shared file (rows 15, 16, 45).
- Third pass: the validator entry in `animation-skeleton.md` gained the narrowing regression (row 57).
- Second pass: the validator entry in `animation-skeleton.md` was reworded to what shipped (row 53), and
  `docs/reviews/lessons/testing-qa.md` gained an instance under "Verify a new guard RED before accepting it GREEN"
  (rows 48 and 50).

## Second pass (2026-10-18)

After the round 1 fixes, Codex gpt-6-astra reviewed them again (307,765 tokens; prompt
`docs/reviews/codex-adversarial-animation-variety-fixes-2026-10-18.prompt.md`, raw output
`docs/reviews/raw/codex-adversarial-animation-variety-fixes-2026-10-18.md`, gitignored) against the same HEAD
`4bd4f3e54`, and a convergence pass read the fixed tree. Codex's verdict: the fixes remain incomplete, with 4 MEDIUM
(F1, F3, F4, F6) and 2 LOW (F2, F5), F6 a confirmed regression introduced by the fixes. It confirmed X1 (the moving
clamp reference) fixed: it rebuilt the six staged unarmed walk and run clips from their FBX hierarchy and keys, not from
the restyle's own numbers, and measured the right ankle's lateral speed at 0.178, 0.197 and 0.287 cm per frame for the
main, B and C walks against 0.313 in the source. It made no edits and ran no fresh C# build or test suite; it ran the 28 validator
tests and nine restyle helper tests, executed its counterexamples in memory, and read compiled IL and native disassembly. It
states that no shipped asset was shown to contain the synthetic counterexamples of F1, F4 and F5: they show the gates can
accept invalid evidence, not that a shipped clip is wrong.

Fixed and verified by the orchestrator (reported, not re-run for this document): tool tests 323 passed (+60 subtests),
C# AnimationVariety tests 173 of 173, all 36 clips re-exported and passing, the generator dry run accepting the three new
reports (strides 0.85, 1.0, 0.89) and refusing the stale 2026-10-14 report for missing provenance. The live
`AssetSources` hold the re-exported FBX: backup `orc_locomotion_20261018-005147` (stamp 00:51:47) holds the 16 superseded
main FBX, and I compared all 36 live FBX with the staging folders by sha256 for this document: 36 of 36 match.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 42 | MED | Codex F1: the validator still trusted the report's choice of frames. Its executed counterexample reported two quiet frames, had six grounded frames, and slid 1.5 cm per frame in the omitted ones; the gate passed. | A gate that trusts the producer's selection | Round 1 changed which source frames the clip's plateau was compared with, not who chose the clip's frames. | The validator finds each foot's grounded plateau itself on the exported poses and on the source poses (the restyle's own contact definitions), requires the reported frames to cover it within 1 frame, and gates over the independent plateau. The landing and take-off frame of a plateau of 4 or more frames are trimmed from the rate, residual, lateral and vertical gates and judged by their own lateral gate: the clip may exceed the source's largest speed within one frame of the same index by at most 1.0 cm per frame (largest real excess over the 36 clips: 0.64). |
| 43 | LOW | Not fixed, and closed 2026-11-04 (Mike: leave it, unless a player sees foot slide): the trimmed landing frame of `orc.json` `run_forward_1h` (strong foot) reads 1.86 cm per frame against the source's 1.80 at the same frame. Investigated: not a skid. The crouch (`knee_bend_deg` 30) and the knee limit (`knee_max_deg` 160) each lower the swing foot with the hips, so the clip's foot touches down two frames before the source's. | An approved posture with a side effect | Inherent to the approved crouch, so no gate on the earlier rows could see it as a fault. | Cap how far a swing foot may drop with the hips (for example never below half its source clearance). That changes every crouched spec's swing path, so it needs its own change and a full re-run. Listed under "Known Follow-ups" in `orc-goblin-locomotion.md`. |
| 44 | LOW | Codex F2 and convergence N1: `low_arm_swing_scale` accepted booleans (Python counts `True` as 1), and so could other limp numbers and the `--vanilla-step-points` file. | Input validation by type | Not recorded. | `_is_number` is the single check (refuses bool, null, NaN, Infinity) for the limp numbers and the step points file. |
| 45 | MED | Codex F3 and convergence D3: the binder's foreign-set guard compared sets of ids. A foreign definition inside the generated region vanished on rewrite when another definition with the same id survived elsewhere; the engine keeps the FIRST definition (`Module.cs:1467-1490`). | A guard that compares ids, not definitions | Round 1 fixed the deletion of a set whose id was unique (row 15); a repeated id made the id sets equal. | Any `action_set` the tool does not own inside the replaced region is refused (apply exits 2, bytes unchanged; `--check` fails), with a `Counter` of ids before and after as a backstop. |
| 46 | MED | Codex F4: `gen_troll_anim_clips.ps1` validated only the entries of `clips_index.json`, so a failed run (dropped from the index, kept in the report) passed, and it classified gaits by the report's `kind`, so a run labelled idle skipped the stride and STEP/LOOP checks. | A check on the index, not on the report | The restyle drops a failed clip from the index but keeps its entry; `-Verify` then counted its old tpac as another pipeline's. | Every report entry with an error or a bad `export.ok` is refused. A gait is a clip named `walk_*` or `run_*`, and a `kind` that disagrees is refused. Provenance: the restyle writes a top-level `spec_sha` and, per entry, `spec_sha` (the spec merged for that clip plus its kind) and `loaded_spec_sha` (the whole loaded spec). The generator requires every gait entry's `loaded_spec_sha` to equal the top-level `spec_sha` and refuses a report without provenance. `merge_report_clips` keeps older entries with their own shas, so a partial run cannot pass as a whole one. |
| 47 | LOW | Codex F5: non-finite and mistyped numbers defeated the generator's checks (a NaN compares false against every limit). | Fail-open on NaN | The checks converted before testing the type. | Null, bool, string, list, NaN and Infinity are refused before any conversion; the stride must be finite and above 0, a step point finite and in [0, 1]. `Get-LoopFault` and `Resolve-TravelScale` fail closed on NaN, and a new `Get-StepFault` closes the same gap in `-Verify`'s STEP check. |
| 48 | MED | Codex F6, a regression of round 1: the missile early return captured the new local `missile` in a lambda, so the compiler allocated the closure at method entry on every hit, before the guard. Proven from the compiled IL by Codex. | A fix that adds the cost it removes | Source order showed the guard first; the compiler hoists a capturing lambda's display class to the start of the method. The wiring test read source order. | `OnAgentHit` holds no lambda; the native reads and `RunOrDefer` moved to `RecordMeleeHit`, after the guard. `OnAgentHit_CompiledBody_AllocatesNoClosure` reads the compiled IL (red before the fix: 2 display classes). Instance of the guard-RED lesson. |
| 49 | UNVERIFIED | Codex X7: it confirmed `GetIndexWithID` 0x6DF7D0 to 0x590060 logging a miss, `GetActionCodeWithName` 0x6EAF80 returning -1, the 0x74BF60 rate lift and the silent indexed listing, but could only partly re-verify the 0x7496D0 idle phase hold (it found the conditional assignment and the alternate branch, no contradiction). | A native claim with a partial second read | Codex did not finish the field layout and forward layer selection. | The convergence pass read lines 352-369 as matching, so the claim stays with the label [Likely]; the transition-pull trigger stays UNVERIFIED. |
| 50 | LOW | Convergence D1: the unit test for the HIGH fix never passed a pelvis x, so it could not fail with the old reference. | A fix whose own test cannot fail | The test exercised the new function with inputs the old bug did not depend on. | A pure `foot_goal_x` helper with no frame-pelvis input, and a source-text test that the restyle passes `pmean.x`, proven to fail with the old reference. |
| 51 | LOW | Convergence D2: the validator did not apply `limp_for_kind`, so it checked idles against the whole limp while the restyle gave them only `arm_forward_deg` (row 10). | Producer and checker apply different specs | The restyle's fix (row 10) did not reach the checker. | The validator applies `rs.limp_for_kind`. |
| 52 | LOW | Convergence D4: Codex S4 (native thread safety of the channel-1 reads in the hit callback, now in `RecordMeleeHit`) was in the RCA but not in the feature doc's Unverified list. | A record split across two docs | Row 37 was written in the RCA only. | A bullet in `animation-variety.md` "Unverified". |
| 53 | LOW | Convergence D5 and Codex X7: after round 1 the RCA said the validator "derives its intervals itself" and the docs said the missile check ran "before any allocation"; neither was true yet (rows 42, 48). | A claim written ahead of its evidence | Not recorded. | Round 1's rows were reworded to what shipped, and the validator lesson now describes round 2 exactly. Both claims are true as of round 2. |
| 54 | LOW | Convergence N2: code still said "four base sets". | Stale count | The base set list lost `as_orc_female_warrior` in round 1 (row 20). | Fixed in code. |
| 55 | LOW | Convergence N3: `animation-variety.md` and the system map listed the addresses of `GetNumberOfActionSets` and the name lookup in reverse, and the map cited the older 0x6DF010 and 0x58FAC0 beside the verified 0x6DF7D0 and 0x590060. | Version labels missing on addresses | The v1.5.3 text and the v1.5.5 read sat side by side without labels. | Native count 0x6DF8F0 and name by index 0x6DF890 (checked in Codex's disassembly); the older addresses are labelled v1.5.3. |
| 56 | LOW | Convergence N4 and N5: a space in a test file and stale docstrings. | Polish | Not recorded. | Fixed. |

### Patterns the second pass confirms

- **A gate that trusts the producer's selection** (row 42, with row 2): round 1 fixed what the validator compared but
  not who chose the frames. The check that settled it was an executed counterexample, not a read.
- **A fix whose own test cannot fail** (row 50, and row 48's source-order test): the HIGH fix's test never varied the
  input the bug depended on, and the missile test read source order where the compiler had moved the allocation. A test
  of a fix is shown to fail with the fix reverted, on the input the bug needs.
- **A regression introduced by a fix** (row 48): the early return added a capture, and the capture added the allocation
  the return was meant to avoid. A fix to a hot path is judged on the compiled body.
- **Doc claims written ahead of the fix** (row 53): the round 1 text described the intended behaviour.

### Why each reviewer caught or missed these

Codex found rows 42, 44, 45, 46, 47 and 48 by executing counterexamples against the fixes (a clip with two reported
frames and six grounded, a duplicate id with a surviving copy, IL read from the built assembly), not by reading them.
The convergence pass found the rest: tests that could not fail, the checker that ignored `limp_for_kind`, the reversed
addresses.

## Third pass (2026-10-18)

A third pass, convergence only (no Codex), read the second-round fixes. It found one HIGH regression that the second
round introduced, and eight smaller items. Fixed and verified by the orchestrator (reported, not re-run for this
document): tool tests 342 passed (+109 subtests), C# AnimationVariety tests 173 of 173, all 36 clips re-exported after the
provenance change and passing, the generator dry run accepting the three reports (strides 0.85, 1.0, 0.89), and the FBX
copied to the live `AssetSources` again. The newest backup is `orc_locomotion_20261018-011522` (stamp 01:15:22, from the
folder listing); it holds the superseded main FBX.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 57 | HIGH | Convergence D1, a regression of round 2: the validator ran its gates only over the plateau it found itself (feet moving back at 85 per cent of the median speed or more), which by definition excludes the frames where a foot stalls, skids or lifts, so those frames went unjudged. Coverage was one-way (every found frame had to be claimed, but a claimed frame outside the plateau was never questioned) and the end frames were gated only sideways. Two executed scenarios passed in round 2 and failed in round 1. A: claimed frames 6 and 7 skating 10 cm per frame forward and drifting 2 cm per frame sideways. B: a claimed landing frame moving back 20 cm per frame against a planted speed of 10. | A fix that narrows what a gate judges | Round 2 answered row 42 (the validator trusted the report's frames) by judging only the frames the validator itself found, and the found set is the steady part of the stance. The fix was checked for the counterexample it answered, not for the one it opened. | Two-way coverage: any claimed frame outside the found core plus its end frames fails (`claimed_outside`). An end frame may not move back faster than the planted speed times (1 + `stride_rel`) plus 0.5 cm (`end_frame_back_cm`); the end-frame sideways gate stays. Scenarios A and B are tests. On the 36 shipped exports: 0 claimed frames outside, the tightest end-frame back margin is 0.62 cm per frame (`orc_main` walk, right foot: 4.18 against a limit of 4.80), and the largest end-frame sideways excess is 0.21 cm per frame. |
| 58 | LOW | Convergence D6: the end-frame sideways reference read the unwarped source at the clip's own index, so a `timing_split` below the source's could loosen it. | A reference read at the wrong time | Round 2 took the source at the same index as the clip; the time map moves the frame the source plays. | The reference reads the source where the warp plays that frame (`rs.output_positions`). |
| 59 | LOW | Convergence D2: the idle block of a spec was not number-checked, and a fractional `*_cycles` would leave the idle loop open at its seam. | Missing input validation | Round 2 hardened the limp numbers and the step points file (row 44), not the idle block. | The idle block is number-checked, and each `*_cycles` must be a whole number of at least 1. |
| 60 | LOW | Convergence D3: the generator required provenance on gait entries only, so a stale or provenance-free idle entry passed. | A check scoped to the clips that matter most | Not recorded. Round 2 required provenance of gait entries; an `--only` gait re-run after a spec change would leave old-spec idle FBX in the batch. | Every entry, idles included, needs `spec_sha` and a `loaded_spec_sha` equal to the report's top-level `spec_sha`. |
| 61 | LOW | Convergence D4: the provenance wiring in the restyle's `main()` had no test. | An untested wire | Not recorded. | Source-text tests pin the provenance wiring in `main()`. |
| 62 | n/r | Convergence D5: the provenance hash covered the loaded spec only, so a change to the tool's code with an unchanged spec file passed as the same provenance. | Provenance narrower than what it vouches for | Not recorded. | The hash behind the top-level `spec_sha` and every entry's `loaded_spec_sha` now covers the loaded spec plus the sha256 of `restyle_human_clip.py` and `retarget_mannequin_to_human.py`; the report lists them as `code_shas`. |
| 63 | n/r | Convergence D7: a stale docstring. | Polish | Not recorded. | Fixed. |
| 64 | n/r | Convergence D8: a vacuous test. | A test that cannot fail | Not recorded. | Replaced. |
| 65 | n/r | Convergence D9: the comment on the missile fix said the compiler allocates a capturing lambda's closure at method entry, which is true only for a variable declared at method scope (row 48). | A comment stronger than the mechanism | Not recorded. | It now says the compiler allocates on entry to the scope that declares the captured variable (method entry for a method local or parameter). |

### Pattern the third pass confirms

**A fix that narrows what a gate judges must keep judging everything the producer claims** (row 57). This adds to pattern
(b): round 1 left the producer's selection in charge of the gate, round 2 took the selection away and also took away
every frame the validator did not select. The gate's contract is the producer's claim, so the claim is checked both ways:
every frame the validator finds is claimed, and every frame claimed is explained by what the validator found. Rows 50 and
48 stand behind it as before: the check that settled it was an executed scenario against the fix, not a read of it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/animation-skeleton.md](lessons/animation-skeleton.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)
- [docs/reviews/lessons/xslt-moduledata.md](lessons/xslt-moduledata.md)

<!-- backlinks-end -->
