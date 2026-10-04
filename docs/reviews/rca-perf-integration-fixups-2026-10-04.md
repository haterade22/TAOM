# RCA: perf programme integration fixups review findings (2026-10-04)

Review: a six-lens deep review of `0afd95c8..f637d650` on `perf/integration-trial-2` (the three fixup commits integration
trial 2 needed after merging the programme's 18 branches onto trunk `7f0c8446`), then one convergence pass on the fixes.

## Top line

No lens found a HIGH or MEDIUM. One finding changed what a player's log says: the tick profiler's hooks-missing warning
said "not measuring" while the default-on hitch probe still measured the mission. That contradiction existed on no
branch; it appeared when plan 028's later hook health check met plan 041's probe-aware status lines in the merge. The
rest is text: claims the merges made stale (a Patch23 postfix plan 030 deleted, Harmony's rethrow on the targets D13
re-shielded, wording written before plans 040 and 042 merged), copies of plan 034's cost figures that had drifted, a
table the merges dropped and a test they doubled, and precision gaps in the hang analysis f637d650 restored. Every finding
is fixed in this review's commit, the log line test first, and the cost figures and the hang analysis now each have one
owning copy that the others point to.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | The hooks-missing warning said "not measuring" while the hitch probe measured the mission (lens 5 #1; lens 4 F5 added that the line is right when the missing hook is Patch98's frame boundary, which closes the probe's frames too) | Reason-line accuracy; a repeat of review 041's R3 | Plan 041 swept plan 028's "not measuring" lines on its own branch, as the 2026-10-02 lesson asks; plan 028's health check, and its line, came later on 028's branch, so no branch held both, and the trial checked compile, tests and lost lines, not what a line claims | `HitchProbeLines.BuildHooksMissingLine` with a probe-aware variant, chosen unless `MissionTickProfilerHealth.FrameBoundaryMissing`; 12 no-game tests on `MissionTickProfilerHooks.OnMissionCreated`, one RED first; the lesson now says to rerun the sweep at the integration merge |
| F2 | LOW | Plan 039's mission-side per-target PatchShield table was dropped at merge step 3 (lens 4 F1) | Merge debris | The trial's lost-hunk check recorded the drop (`merge-drops.txt`, plan 039 at `9792ad48`), but its summary raised two drops and not this one: the rows named Patch35, which plan 030 deleted, so they read as obsolete, though the table's purpose, which the 2026-09-26 lesson requires, still applied | Rewritten for the six mission targets of the merged tree; lesson in `build-tooling-workflow.md` |
| F3 | LOW | `PatchShieldPolicyTests` held two tests from plans 028 and 041, one a subset of the other with a stale comment (lens 4 F4) | Merge debris | Git auto-merged both (the merge map predicted no conflict in the file); the check looked for lost lines, not doubled tests | The subset deleted; the same lesson |
| F4 | LOW | Four places described a Patch23 postfix on `Mission.SpawnAgent` that plan 030 deleted (lens 2 W1, lens 5 #2) | Stale text after a merge | f637d650 swept for Patch35, which plan 030 also deleted, by name, not for every member plan 030 removed | Fixed; the lesson asks for a grep of every deleted member |
| F5 | LOW | "A void finalizer keeps Harmony's rethrow" on the three targets D13 re-shielded, and on Patch91's shielded `MissionState.TickMissionAux` (lens 2 W2; the Patch91 sentence is older, found by lens 4) | Stale engine claim after a policy change | The sentences were written when every finalizer on those methods was void; PatchShield's value-returning finalizer makes Harmony `throw` | Fixed; a repeat of the `harmony-il.md` lesson on value-returning finalizers |
| F6 | LOW | Plan 034's cost figures, copied into nine places, had drifted: 63 against 64 ns, a Release total quoted as an increment, "plan 034 measured the finalizer" where it benchmarked a stand-in (lens 2 W3, lens 1 #1 and #2, lens 5 NIT 6) | Duplicated fact | The 2026-10-03 lesson's Prevent asked each entry to carry the figure beside it, so each plan restated it; four correction rounds on this figure were already recorded | One owning statement in `PatchShieldPolicy`'s `ExcludedTargetNamespacePrefixes` comment, naming both builds; the copies this diff touched point to it; the Prevent changed (lens 6 proposal 2) |
| F7 | LOW | The restored hang analysis left out the `Managed.ApplicationTick` frame, which carries Patch37's and PatchShield's finalizers, and its three copies disagreed about a throw before the `tickCompleted` clear; the window, the remedy's test positions and "every frame" were imprecise (lens 5 #3, lens 2 N1 to N4, lens 6) | Duplicated analysis; precision | The analysis lived in three copies, and f637d650 restored two of them by hand after the merges dropped them | One owning copy in `mission-perf-heartbeat.md`, "PatchShield"; the policy comment, the registry and the tests point to it; the precision fixed against the v1.5.3 decompile (lens 6 proposal 3) |
| F8 | LOW | The restored 2026-09-26 lesson's rule contradicted the D13 lesson's Prevent on per-frame targets (lens 4 F3) | Contradictory rules | The restore put back plan 039's narrowing; the D13 lesson, written the same day on plan 028's branch, had replaced the same half of the rule | The D13 Prevent defers to the 2026-09-26 rule |
| F9 | LOW | Stale wording elsewhere: swallow logging in `map-perf-profiler.md` (decision D16), "at the end" in `PatchShieldPolicy`, a lesson pointer, "What stays on the list" without plan 040's entries, the agent tick given as a main-thread example, branch-relative plan 040 and 042 text, a "(Patch97)" test sample, and a Known limits bullet on a stripped frame boundary (lenses 1, 2, 4 and 5) | Stale text after a merge | Each sentence was true on its own branch | Fixed |
| F10 | LOW | Nothing pinned the health check's frame-boundary hook to the method that closes frames (lens 4 F6) | Gate coverage | The required hook was resolved from its class's attributes, never checked against the call that does the work | `FrameBoundaryHook_IsTheOnePatchMethodThatClosesFrames`, an `IlCallScanner` test |
| F11 | LOW | The catcher list this review added named `Managed.ApplicationTick` as `Module.OnApplicationTick`'s caller; `CoreManaged`'s component method sits between them, and native's `Managed.ApplicationTickLight` reaches the same method with no TAOM catcher (convergence pass) | Engine claim written from a lens report | The fix took lens 5's frame list as written and checked the frames' `try` blocks, not the call edge between each pair | Rewritten from the v1.5.3 decompile (`CoreManaged.cs:120-123`, `Managed.cs:290-313`); when the light tick runs is UNVERIFIED and goes to the PatchShield follow-up plan |
| F12 | NIT | The new pointers to the hang analysis named the heading "PatchShield", which the feature doc used twice (convergence pass) | Ambiguous pointer | The owner was chosen by section, and the second section's same name was not checked | The hitch probe part's heading is now "PatchShield on the probe's targets" |

Design (lens 6 proposal 1, applied): `LogMissionStatus` left the `MissionLogic` for `MissionTickProfilerHooks.OnMissionCreated`,
beside the probe's `OnMissionFirstTick`, and the partial file went. The partial split met ADR-002 only if partial files
count separately (FOR-MIKE 16l), and the profiler's status logic ran only in game-required tests, which CI excludes; its
12 tests now pass in CI's reference-assembly step (96 of 96 in the filtered run).

The convergence pass (one reviewer, then an adversarial verifier per finding) confirmed that the moved status writer
is the old logic line for line apart from F1, that no path picks the wrong variant, and that every other text fix
holds against the decompile; it found F11 and F12, both fixed.

## Root-cause pattern

Two causes cover every finding.

1. **A merge composes branches that never saw each other's later work.** F1, F2, F3 and F8, and the stale text in F4, F5
   and F9, are each true on their own branch and false only in the merge. The per-branch reviews could not see them, and
   the trial verified that the tree built, that the suites passed and that no line was lost, not what the merged lines
   claim.
2. **Copied facts drift.** The cost figure (F6) and the hang analysis (F7) lived in up to nine places. Each correction
   round fixed some copies, the merges dropped others, and the copies disagreed. With one owner and pointers, a
   correction is one edit and a merge cannot keep half of it.

## Why each agent missed these

The six lenses of this review found every finding; this section is about the work before it.

- **The per-branch reviews** (plans 028, 030, 034, 039 and 041) each saw one branch, and F1's two halves, F2's drop and
  F3's duplicate exist only in the merge.
- **The trial's merge agent** resolved conflicts to keep the build green, and took one side's paragraph where both
  branches had edited it (F2, and the two drops f637d650 restored).
- **The trial's check agent** recorded F2's drop in its raw output and raised two drops in its summary, not this one; it
  compared lost lines, not doubled tests (F3) or meaning (F1).
- **The fixups agent (f637d650)** worked the list it was given and swept for Patch35 by name, so Patch23's postfix (F4)
  and the rethrow claim (F5), the same class of staleness, stayed.
- **In this review:** Standards found F6's `map-perf-profiler.md` lines and parts of F9; Engine compatibility found F4,
  F5, F6 and F7's precision; Efficiency found nothing, as no hot path changed; Completeness found F2, F3, F8, F10, F1's
  frame-boundary condition and parts of F9; Data flow found F1 and F7's missing frame; Design found the duplication
  behind F6 and F7 and the shape applied above.

## Follow-ups (not fixed here)

- For the PatchShield follow-up plan (decision D13): one per-target table merging the mission doc's two PatchShield
  sections (lens 6 proposal 4); the diag.log skip line's "a hot target" label, which changes a log line (lens 6
  proposal 5); the older copies of plan 034's figure that still restate it (`PatchShield.cs`, five test files,
  six docs and the `harmony-il.md` lesson "A blanket 'shield everything patched' mechanism costs what OTHER mods
  patch"); when native runs `Managed.ApplicationTickLight`, and whether
  Patch37 should wrap it (F11).
- A design question: with Patch98's frame boundary missing and the probe on, the mission still counts as measuring,
  so its first-tick header says "mode probe" while the profiler's warning (correctly) says nothing is measured.
  Changing that changes what a measuring mission means; it predates this review.
- Tooling: `tools/snapshot_api_surface.ps1` drops the auto-generated backlinks block the docs tooling keeps in the two
  snapshot files, so its `-Check` reports drift for that block on any branch; this review's refresh put the block back
  by hand. The generator should keep the block, or the check ignore it.

## Feedback memories to codify

- Rerun the second-measurer sweep at the integration merge, not only on the branch that adds the second measurer
  (`lessons/misc.md`, a repeat of the 2026-10-02 lesson).
- A merge of parallel branches drops text and doubles tests without a conflict: raise every dropped hunk, rewriting
  stale-looking content rather than dismissing it, compare each multi-branch test file's merged test set, and grep for
  every member a merged branch deleted (`lessons/build-tooling-workflow.md`).
- State a measured figure or a hazard analysis once and point to it (`lessons/harmony-il.md`, the 2026-10-03 lesson's
  Prevent, changed in this review).
