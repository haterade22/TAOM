# RCA: yotthani review code items C1 to C4 (deep review, 2026-10-15)

## Top-line

The `/deep-review` of the four code items from the 2026-10-15 yotthani review ran seven lenses in two waves on the
uncommitted worktree `E:\repos\taom-yotthani-1015`: 1 Standards, 2 Engine compatibility, 3 Efficiency and 5 Data
flow, then 4 Completeness, 6 Design and Tooling. XML was not in scope. It found no HIGH or CRITICAL defect, five
MEDIUM findings and a set of LOW ones. Every finding below was checked against the code, the v1.5.5 decompile, or
Harmony 2.4.2's decompiled `PatchClassProcessor` before it was fixed. A convergence pass on the fixes found four LOW
doc and comment defects (below), also fixed. At the maintainer's request, a fresh `/deep-review` and a
`/review-codex` follow on the fixed tree.

The items:

| Item | Issue | Change |
|---|---|---|
| C1 | #806 | The v1.5.5 `KnownBuilds` row for the skeleton-buffer pin; both native pin classes tagged `BindingVerification`, so an unlisted build fails the binding gate; `NativePinGateTests` keeps every table in the gate |
| C2 | #807 | `MainMenuStep.Run` (named `LifecycleStep` until the second review) guards `CustomizeMenu` in `OnBeforeInitialModuleScreenSetAsRoot`, which the engine retries every tick after a throw |
| C3 | #808 | `VanillaTuningOverlapContractTests` pins the four TAOM type names VanillaTuning v0.1.1 stands aside for |
| C4 | #809 | A `cameraMs` column: the camera and native PreDisplay time split out of `missionTickMs` and `otherMs` |

Review record: [adopt-yotthani-2026-10-15.md](adopt-yotthani-2026-10-15.md).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The camera bracket was declared last in `Patch98_HitchProbe` "so a failure costs only this class". It did not: `PatchCategoryIndex.Apply` throws at the first class that fails, `SubModule.TryPatchCategory` returns false, and `HitchProbeInstaller` then turns off the whole probe and the full profiler for the session (Lenses 2 and 5) | A containment claim not traced to its consumer | The design reasoned about Harmony keeping the earlier classes patched, and never followed the category's result into the installer's flags | The camera class is its own category, `Patch98_HitchProbe_Camera`, applied only after Patch98 and kept out of `ProbeInstalled`; the claim is gone from the code and both docs. New lesson in `lessons/harmony-il.md` |
| 2 | MED | Once the camera category can fail alone, `cameraMs=0.00` would read as zero camera time, and the install line would still list `MissionScreen.UpdateCamera` (Lens 4) | A REPEAT: an install line that claims more than was checked (2026-10-08 finding 22) | Found only after fix 1 was chosen | `CameraNotInstalledLine` (one WARNING), and the install line omits the camera when its category failed. Recurrence added to `lessons/harmony-il.md` ("An install line claims only what was checked") |
| 3 | MED | `perf_runs.py compare` refused logs with and without `cameraMs`, but compare reads no tick-phase column, so the refusal guarded nothing and blocked every A/B across the change (Lenses 3 and 5) | A guard written without listing what it protects | The plan took the refusal from the design checker, and nobody listed which metrics compare reads | Refusal, `_camera_column` and its tests deleted; `test_compare_accepts_rows_with_and_without_camera_ms` added. Covered by the new lesson for finding 1 |
| 4 | MED | The new `/engine-bump` sentence said the pin tables FAIL in the binding gate; the two Python tables still skip on an unknown build and run only with `TAOM_GHIDRA_IT=1` (Lenses 1 and 5) | A list sentence edited for one member | The edit was written for the two C# tables and not re-read against the Python ones in the same sentence | Split into the C# rule and the Python rule (read the skip count). One-off |
| 5 | MED | The new frame-table clause carried yotthani's 1.1 to 3 ms and the culling and posing description under a `[TAOM-verified]` table tag, as "measured upstream" (Lens 1 rated it MEDIUM, Lens 2 LOW) | Attribution: a REPEAT of 2026-10-08 finding 26 (upstream figures stated as TAOM facts), its second recurrence | The implementer copied the figure from the issue body, which names yotthani, but not the name | Tagged `[yotthani, MithrilForge docs/engine/perf.md, measured in game, v1.5.3 and v1.5.4]`; TAOM's own decompile (`pre_display` 0x6FCDB0 on v1.5.5 is a thunk to 0x7D800 that builds the frame's render context) is stated separately. Recurrence added to `lessons/native-cpp-port.md` ("A count in a reference doc names its method"): a copied figure takes its source tag in the same edit |
| 6 | LOW | Docs said the camera runs "after the behaviour loops" (it runs between them, v1.5.5 `Mission.cs:3741-3753`) and "once per tick" (one call site, made only when the mission is Continuing, not on a dedicated server, and with `updateCamera` true; fast-forward sub-ticks pass false, `MissionState.cs:171-219`) (Lenses 1, 2 and 5) | Engine order written from a summary | The text followed the design's summary of the call chain and was not re-read against `Mission.OnTick` and `MissionState` | Corrected from the decompile |
| 7 | LOW | The doc samples and their pinned literals no longer summed to `wallMs`/`frameMs` (they overshot by `cameraMs`) (Lens 5) | Pinned examples not re-derived | The samples were edited for the new column only | `otherMs` re-derived (4019.50, 13.20, 60289.75) in the doc, `TickProfileLinesTests` and `test_perf_runs.py` |
| 8 | LOW | The cost evidence (benchmark, `ProbeCostMeter`) covered five brackets while the docs quoted it for six (Lenses 3 and 5) | Evidence not extended with the change | The design listed the benchmark as unchanged | Camera added to both; re-measured at 0.70 to 0.75 us per simulated frame (four saved runs, desktop). The fixer's first figures (0.75 to 0.96 us) had no saved output; the convergence pass marked them UNVERIFIED and the runs were repeated |
| 9 | LOW | Two camera tests and the benchmark passed only when an earlier test had loaded the game assemblies; alone they threw `FileNotFoundException` for `TaleWorlds.MountAndBlade.View` (found by the fixer) | Hidden test-order dependency | Every run was the full suite or the feature filter | `GameAssemblies.EnsureLoaded()` guard (Inconclusive without the game) |
| 10 | LOW | `NativePinGateTests` matched the category tag anywhere in the file (a method tag would pass) and scanned the test tree twice (Lenses 3 and 4) | Gate test too loose | Written to pass on the tree it was written for | Tag match anchored to the class declaration, with a test that a method tag does not count; one cached scan |
| 11 | LOW | The main-menu shape test matched the guarded call by its shape, not by a count, so a second, bare call could pass; the `Stop()` wrap guarded a call that only sets a volatile flag (Design lens) | Over-wide guard, under-strict test | The design wrapped both calls by symmetry | `Stop()` is a direct call; the test counts the `CustomizeMenu()` calls |
| 12 | LOW | The warning tag `[Lifecycle]` belongs to Load-Time Stamps' line family (Lens 5) | Log-tag collision | Not checked against `load-time-stamps.md` | Tag `[MainMenuStep]` |
| 13 | LOW | The provenance register had no line for `VanillaTuningOverlapContractTests.cs`, and its MithrilForge row did not cover `mission-perf-heartbeat.md` (Lens 1) | Register not updated with the code | The register pass belonged to the docs batch | Written in the docs batch's register pass, which lands in the same push; the C3 commit body says so |
| 14 | LOW | Smaller items: stale counts in comments ("three shielded targets", "five exits"), a comment filing the camera under decision D13, timing tests with fixed ceilings, missing asserts on `CameraMs`, two lists of the same target names, two redundant tests, an unused general `OPTIONAL_PHASES`, a `__main__` guard above a test class, the hitch probe's MCM hint (all lenses) | Polish | Within normal review scope | Fixed |

## Convergence pass

One `deep-reviewer` re-checked the fix round. It found no runtime or behaviour-parity defect and four LOW defects in
docs and comments, all fixed:

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| D1 | LOW | Four places said a camera category failure "joins the startup notice". The category applies at game init (`SubModule.cs` `InstallIfEnabled` call), whose failures go to a red chat line; the startup inquiry belongs to the MainMenu phase, and `TakeFailureSummary` clears its list after each phase | The orchestrator's fix list (item C4-15) stated the notice from memory, and the fixer copied it | "joins the game-initialization notice, a red chat line" in the code comment, the feature doc (twice) and the registry |
| D2 | LOW | The D13 paragraph added `MissionScreen.UpdateCamera` to its list but still said D13 "took them off" and "those three brackets"; UpdateCamera was never excluded | A list sentence edited for one member, the same shape as finding 4 | D13's three methods kept; one sentence says UpdateCamera is shielded by the 2026-10-15 decision (#809). The code comment's "plan 041 camera bracket" became #809: plan 041 is the original probe |
| D3 | LOW | `HitchProbeInstaller`'s summary still said "Logs one line either way" | Summary comment not re-read after finding 2's fix | Names the install line and the camera WARNING |
| D4 | LOW | The camera cost sentence counted PatchShield's finalizer, then pointed to a benchmark that attaches only the probe's pairs | Pointer not checked against what the benchmark measures | The sentence says the Cost section measures the probe's pair only and points to `PatchShieldPolicy` for the shield's figures |

It also marked the benchmark figures UNVERIFIED (finding 8), and listed cosmetic items (an unwrapped comment and doc
line, three blank lines before a Python test class, a stale step name in `LifecycleStepTests`), all fixed.

## Root-cause pattern: a claim about another component written without reading that component

Findings 1, 3, 4 and 6, and D1 of the convergence pass, share one cause. Each statement described what another
piece of code does: the installer after a category failure, `compare` after a column change, the Python pin tests,
`Mission.OnTick` and `MissionState`.
Each was written from the author's picture of that code rather than from the code. The 2026-10-08 RCA found the same
shape twice (items 10 and 11: a log line and an in-game step not traced to their consumers). The preventive rule
already exists in `evidence-over-claims.md`; the new lesson in `lessons/harmony-il.md` makes it concrete for patch
categories, where the cost is a whole diagnostic switched off.

## Why each lens caught what it did

- **Engine compatibility (2) and Data flow (5)** both found finding 1, by following `TryPatchCategory`'s result into
  the installer.
- **Efficiency (3) and Data flow (5)** found finding 3: the refusal cost every A/B and protected no metric that
  compare prints.
- **Completeness (4)** found finding 2, which only exists once finding 1's fix is chosen.
- **Design (6)** chose the fix for finding 1 (a category, not a `Prepare()`, which would cover only a missing target)
  from Harmony 2.4.2's `PatchClassProcessor`, and found finding 11.
- **Standards (1)** found findings 4, 5 and 13 and most of the polish.

## Second review (fresh, at the maintainer's request)

After the fixes above, the maintainer asked for a fresh `/deep-review` and a Codex review. Both read the five local
commits `caefaa1f3..382f5190a`: seven `deep-reviewer` lenses in two waves (1, 2, 5 and 3, then 4, 6 and Tooling) and
Codex (`gpt-6-astra`, reasoning `max`, prompt in
`docs/reviews/codex-adversarial-yotthani-review-2026-10-15.prompt.md`). No CRITICAL or HIGH. Codex rated its first
finding MEDIUM; the Tooling and Completeness lenses showed that no TAOM log can produce its input, so it is LOW here.
The one MEDIUM left was in the docs batch that ships in the same push.

| # | Sev | Finding | Source | Why the first review missed it | Fix |
|---|---|---|---|---|---|
| S1 | LOW | `perf_runs.py aggregate_ticks` dropped the camera column when only some windows carried it, while still summing `missionTickMs` and `otherMs`, whose definitions differ between the schemas: measured camera time left the breakdown | Codex F1; Tooling | The first review's test helper `_no_camera` deleted the column without putting its time back in `otherMs`, so the mixed test pinned the wrong result as intended | The column shows when any window has it and sums only the values present; `_no_camera` writes what an old build wrote (it reproduces `caefaa1f3`'s pins) and a fidelity test checks that the old line sums to its wall clock |
| S2 | LOW | `NativePinGateTests` still read raw text, so a commented-out `[TestCategory("BindingVerification")]` passed, and it took the class name from the file name | Codex F2; Lens 4 | Finding 10's fix anchored the regex to the class declaration but kept the text match | Reflection: the types that declare `KnownBuilds`, their compiled `TestCategory` attributes, both known tables pinned by name |
| S3 | LOW | The frame table's PreDisplay clause put a v1.5.5 address (0x6FCDB0) under a table that pins native offsets to v1.5.3 (0x6FC5F0 there) | Lenses 1, 2 | Introduced by finding 5's fix, which read the decompile but not the doc's tag legend: the third instance of that class (2026-10-08 finding 26, finding 5) | Both builds named in the clause |
| S4 | LOW | "CameraTick, then PreDisplay()" in five places: `MissionScreen` calls `PreDisplay()` only while the mission is Continuing and not `MissionEnded` (v1.5.5 `MissionScreen.cs:4159-4162`), so after a battle's result `cameraMs` times `CameraTick` alone | Lenses 1, 2; Codex S7 | Finding 6 corrected the caller's conditions and did not read the callee | One clause in the column row, the patch header, the camera hooks and the frame row |
| S5 | LOW | "A swallowed missing-API exception skips that frame's PreDisplay()": on the v1.5.5 client, native `pre_display` (0x7D800) is latched once per frame and the engine's frame function (0x344A0) calls it too in one branch, so the work most likely moves to `otherMs` | Lens 2 | Written from the managed call alone | Restated as UNVERIFIED in game, with the native evidence |
| S6 | LOW | The camera target was called shielded without a condition; it applies in TAOM's game-init step, after PatchShield's pass 2, so it is shielded only from a process's second game start (never with co-op) | Lenses 2, 3, 5 | The doc already says this for Patch97 and Patch98; the new text did not read it | The PatchShield section gets the camera row, and its timing paragraph covers all four shielded targets (Patch23 on `SpawnAgent` applies in the same late batch); the first fix wrote the timing for the camera alone, which the convergence pass corrected |
| S7 | LOW | If `MissionScreen` cannot load, the index skips the camera class, its category applies nothing and returns true, so the install line still names the camera and no camera WARNING is written | Lenses 2, 5 | Finding 2's fix covered a category that throws, not one with no class | A doc clause (the skip writes its own ERROR into the startup notice); a code check was rejected (below) |
| S8 | LOW | The Cost section set the new benchmark beside a five-bracket figure from 2026-10-03, implying the camera costs about 0.2 us | Lens 3 | Figures from different days on a machine whose timings drift by about 50% | A same-session A/B (five brackets 0.646, 0.665, 0.767 us; six brackets 0.836, 0.775, 0.715 us): the camera's cost is inside run noise |
| S9 | LOW | The full-mode `[HitchDetail]` sample kept `onTickMs=6.30`, less than its `[Hitch]` partner's PreDisplay, camera and MissionTick time (8.85) | Lens 5 | Finding 7's re-derivation covered the three phase samples, not their detail partner | 9.55 in the doc and the pinned test |
| S10 | LOW | The main-menu WARNING carried only the exception's type and message; before #807, Patch37's crash report logged the stack | Lens 5 | It was written as a one-line status line, and nobody compared it with what Patch37 logged before | The warning carries the full exception; a test with a named throwing method proves the stack is there |
| S11 | LOW | `LifecycleStep` was general by name and documentation but logged a main-menu tag, with one caller, and its name shared the `Lifecycle` prefix with Load-Time Stamps | Lens 1; Lens 6 | Finding 12 renamed the tag, not the class | Renamed `MainMenuStep` |
| S12 | MED | Docs batch: the lifecycle reference said every step of the hook runs through the guard and that the test pins that shape; the coexistence section promised a control test for an unknown name. Neither is in the code (finding 11 narrowed the guard; the control test was dropped as redundant) | Lens 4 | The docs batch was written from the design files in parallel with the code fixes | Both sentences now describe the code |
| S13 | LOW | `main-menu-customizer.md` still showed a direct `CustomizeMenu()` call and never named the guard or its WARNING; `skeleton-buffer-guard.md`'s Changelog and Issue sections stopped at #776 | Lenses 1, 4 | The first review's scope list named the docs the items changed, not the feature docs that describe the same code | Both docs updated |
| S14 | LOW | Polish: a redundant camera test and a duplicate profiler test, two assembly scans where one does, three inline class lists, stale comment counts, unwrapped lines and a 21-line paragraph, a lesson sentence that said "categories" for targets, and four gendered pronouns in the docs batch for people whose pronouns are not stated | Lenses 1, 3, 5, 6; orchestrator | Polish | Fixed |

**Rejected, with the reason:**

- **Moving the guard into `MainMenuCustomizerService`** (Lens 6): about 190 lines fewer, but `IoC.Resolve` would sit
  outside the catch, and no test resolves this service through the container, so a broken registration would bring
  back the per-tick loop that #807 removes.
- **Writing `na` for an unmeasured `cameraMs`** (Lens 5, option): the sibling phase columns whose hook can be missing
  (`waitTickMs`, `preTickMs`) read 0 with one status line; one phase column should not behave unlike the others.
  A change for all three is a separate decision.
- **A code check for S7** (Lens 2, Lens 5): the skip already writes an ERROR into the startup notice; a check would
  need a second way to resolve the camera type. Lens 6's idea that the index read a skipped class's category
  attribute on its own changes every category's result, so it needs the maintainer's decision.
- **The install line's bookkeeping figure counts the camera bracket even when its category failed** (Lens 1):
  `ProbeCostMeter` measures before either category applies, so the figure overstates a sub-microsecond cost by one
  bracket pair in that rare case.
- **`HitchProbeLines` drops the camera with `BracketMethods.Length - 1`** (Lens 1), which holds only while the camera
  is last: `BuildProbeInstallLine_CameraCategoryFailed_OmitsTheCameraTarget` already fails on a reorder.
- **Coverage labels for a mixed row in `perf_runs.py`** (focused Codex pass): no TAOM log produces a mixed row, so
  the code comment states that its camera rate is a lower bound, and nothing more is added.
- **Failing `NativePinGateTests` on any type that does not load** (focused Codex pass): it would break the no-game
  runs Codex itself named; the two known tables are pinned by name, and the class summary states the limit.

**A wave-1 note corrected:** Lens 5 said two of the four VanillaTuning stand-aside features have an MCM toggle. All
four do (the mission-start guard's is "Survive Mission Start Failures"), so the docs batch's sentence about "one of
the four" was right as written.

**Pattern:** the second review found the same root cause as the first, in two new forms. A fix was checked against
its finding and not against its neighbours (S3, S9: finding 5's fix broke the doc's own tag rule; finding 7's missed
the detail line). And text was written from the design, not the code that the review had changed (S12, S11).

**Checks of the fix commits** (`382f5190a..b3f79a7be`): a convergence `deep-reviewer` and a focused Codex pass
(`gpt-6-astra`, reasoning `high`, prompt in `docs/reviews/codex-adversarial-yotthani-review-fixes-2026-10-15.prompt.md`).
Neither found a defect in the code or the tests; both confirmed the helper reproduces `caefaa1f3`'s three pins and
that the engine claims hold on v1.5.5. They found five LOW items in comments, docs and this record:

| # | Finding | Source | Fix |
|---|---|---|---|
| V1 | S6's fix gave the second-game-start timing to the camera alone; Patch23 on `SpawnAgent` and both Patch98 categories all apply in the same once-per-process game-init block, after PatchShield's pass 2 | Convergence | The PatchShield paragraph, the D13 paragraph and the patch header cover all four targets |
| V2 | The native RVAs 0x344A0, 0x7D800 and 0x7DD60 were added without their build, and 0x344A0 calls 0x7D800 in one branch only | Convergence | "on the v1.5.5 client" and "in one of its branches" in the doc and the S5 row |
| V3 | Two Lens 1 notes (the bookkeeping figure, `Length - 1`) were in no row | Convergence | Listed under "Rejected" with their reasons |
| V4 | A mixed row's camera rate is spread over the whole row with no label | Codex | Comment only (rejected above) |
| V5 | A pin table on a type that fails to load would escape the scan | Codex | Class summary states the limit (rejected above) |

V1 and V2 are the same pattern again, in the fixes themselves: a statement about one member of a set (one target,
one address) written without checking the set (all four targets, the doc's build rule).

## Engine bump

The missing v1.5.5 skeleton-buffer row is recorded as F10 in
[rca-engine-bump-v1.5.5-2026-10-14.md](rca-engine-bump-v1.5.5-2026-10-14.md).

## Follow-ups (not in this change)

- **Decision, not filed:** TAOM's Patch37 finalizer on `Module.OnApplicationTick` swallows a throw that the engine
  then retries every tick, so any module's main-menu throw becomes a loop. Filing it needs the maintainer's word.
- **Shared test helpers:** the tests carry several copies of a brace-matching method-body walker and of an
  obj/bin-skipping source walk; one `RepoPaths` helper each would replace them (Design lens).
- **Python native pins** still skip without `TAOM_GHIDRA_IT=1`; the engine-bump skill now says to read their skip
  count.
- **`TICK_PROFILER_STATUS`** in `tools/tests/test_perf_runs.py` claims every `[TickProfiler]` status line shape but
  lacks the camera lines, and its prefix-missing lines drifted from `HitchProbeLines` before this change (Tooling).
- **Issue bodies:** #807 still says two calls go through the guard, and #809 still describes the deleted compare
  refusal; both are corrected when the issues are updated at the push (a public edit, on the maintainer's word).
  #809 closes with `triage-needs-ingame` (one Custom Battle start must show `cameraMs`).

## Feedback memories to codify

None. The lessons:

- `lessons/harmony-il.md`: a new lesson for findings 1 and 3 ("A patch category fails as one unit"), and a
  recurrence on "An install line claims only what was checked" for finding 2.
- `lessons/native-cpp-port.md`: a recurrence on "A count in a reference doc names its method" for finding 5, and a
  new lesson for the engine bump's F10 ("A native pin table fails the engine-bump gate on a build it does not list").

Findings 2 and 5 are repeats, so their preventive action is the existing lesson plus a concrete rule for this shape;
no new rule file, since `evidence-over-claims.md` already states the general rule.

The second review added:

- `lessons/harmony-il.md`: a second instance on "An install line claims only what was checked" for S7 (a category
  left with no class still returns true).
- `lessons/native-cpp-port.md`: a third instance on copied figures for S3 (read the target doc's tag legend before a
  figure or address goes in).
- `lessons/misc.md` (with the knowledge-base batch): "A doc batch written beside the code is re-read against the
  reviewed code before it ships", for S12.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/adopt-yotthani-2026-10-15.md](./adopt-yotthani-2026-10-15.md)
- [docs/reviews/rca-engine-bump-v1.5.5-2026-10-14.md](./rca-engine-bump-v1.5.5-2026-10-14.md)

<!-- backlinks-end -->
