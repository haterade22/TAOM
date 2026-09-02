# RCA: memory diagnostics (crash-bundle verdict + `[MemStation]` anchors), 2026-09-01

**Scope.** The crash-report System Memory verdict (`SystemMemorySnapshot`, `MemoryPressureVerdict`,
collector/service/renderer wiring), the `[MemStation]` screen-transition anchors
(`MemoryStationSampler`, the `FormatSampleTokens` extraction, SubModule wiring), the Python mirror in
`tools/triage_battle_load.py`, and `tools/Invoke-CommitMatrix.ps1`.

**Review shape.** 9 agents (5 core + 4 specialised to this changeset: cross-language contract,
crash-path robustness, repeat-defect, tooling/test-quality). **Outcome: 1 CRITICAL, 5 HIGH, 6 MED,
6 LOW; all fixed this session.** Suites after: C# 7,856 pass / 0 fail, BindingVerification 198,
Python 847, `lint_docs` 0 dead links and 0 style findings.

Two agents cleared things worth recording as positives: the threshold was single-sourced rather than
copied a third time (the "textbook correct response" to the prior `[MemSample]` drift RCA), and the
toggle gating correctly distinguished an I/O budget from externally-consumed state, checking both
counter-examples inside this same feature.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | **CRITICAL** | `[MemStation]` cannot observe the encyclopedia. It is a `MapEncyclopediaView` overlay on `MapScreen`, not a `ScreenBase`; no `ScreenManager` event fires for it. Docs, CHANGELOG and both sides of the cross-language contract named `GauntletEncyclopediaScreen`, a type that exists nowhere in v1.4.8 | Engine API / instrument aim | I invented a plausible fixture name and never resolved it against the engine. The tests exercise string formatting only, so a fake type name passes identically to a real one | New lesson (below). Fixture names that denote ENGINE types must be resolved before use |
| 2 | HIGH | `format_report` raised `ValueError: max() iterable argument is empty` on a log whose stations are all `exit` with no `enter`. Killed the whole run: no verdict, no `--json`, exit 1, which is the same exit code as a diagnosed hang | Python correctness | `classify_stations` returns a *truthy dict* containing an *empty list*; `if stations:` passes. My test covered `classify_stations` in isolation, not the seam that breaks | Regression test through `format_report`, not just the classifier |
| 3 | HIGH | The documented per-**session** cap was per-**process**: `_emitted` / `_capReported` were never reset, so a second campaign inherited an exhausted budget and logged nothing, its one cap warning sitting in a previous campaign's log | State lifecycle | I mirrored `MemoryPressureSampler` ("Singleton like its sibling") and inherited its un-reset latch instead of auditing it | `ResetSessionBudget()` on `Start()`, which fires on every main-menu return. Lesson below |
| 4 | HIGH | `Test-CommitMatrixLabel`'s `^[A-Za-z0-9_.:-]+$` accepted a trailing newline (.NET `$` matches before one), diverging from the `IsValidLabel` it claims to mirror and injecting a physical line break into `stations.csv` | Cross-language mirror | I mirrored the validator's *intent* with an equivalent-looking regex instead of its *predicate* | Rewritten as the same per-character test. Lesson below |
| 5 | HIGH | `Get-CommitMatrixProcess` silently picked the first of N matching processes, and its comment claimed `-Report` was the cross-check. `-Report` captured `procid` to CSV and never printed it | Tooling | I wrote the mitigation into a comment and never built it | Warn on >1 process; print pid per row; flag a pid change between stations |
| 6 | MED/HIGH | Negative `SysCommitUsedMb` was unguarded in `FormatHeadline` / `FormatDetail`, printing `commit -1/31646MB, headroom 31647MB (100%)` beside the "no memory pressure" label the guard had correctly produced | Render/decision asymmetry | I mirrored `IsLowHeadroom`'s garbage guard in the DECISION (by delegating) and not in the RENDER | `HasUsableCommit` applied at both render sites. Lesson below |
| 7 | MED | `PercentOf` did unchecked `part * 100`, so `PercentOf(long.MaxValue, long.MaxValue)` wrapped to **0**: a fabricated zero indistinguishable from a real 0%, reached through arithmetic rather than through a null | Degenerate-input arithmetic | The class's own omit-on-failure discipline covered the null path only. Overflow was an unconsidered second route to the same false value | Bound before multiplying; return null when the product would wrap or the quotient exceeds `int` |
| 8 | MED | `max_visit_delta_mb` seeded at `0`. For a screen where memory fell on every visit the seed won, printing `(max visit +0 MB)` beside `-1500 MB total` | Reduction seeding | Seeded a running max with a value outside its data's domain | Seed with `None`, take the first observed delta |
| 9 | MED | Pairing is by screen name with no nesting awareness, so an outer screen's delta re-includes an inner screen's growth and both report it. Summing the column double-counts | Analysis correctness | I modelled a screen *stack* as independent per-name visits | `nested_visits` / `nested_overlap`, surfaced in the report output rather than only in doc prose |
| 10 | MED | `Export-Csv` threw `IOException` when the CSV was open in Excel, with no handler. In a one-shot capture session that station is lost | Tooling robustness | Did not consider the operator's other tools holding the file | Retry once, then print the row for hand-recovery |
| 11 | MED | vmmap can fail **without throwing** (exits clean, writes nothing), so a csv-succeeded / mmp-failed capture reported success | Tooling robustness | The `catch` could only see throwing failures | Per-extension `Test-Path` check with its own warning |
| 12 | MED | Untested guards: `IsUnderPressure(null)`, `FormatDetail`'s duplicated commit-limit and managed-share guards, over-commit rendering, 64-char pass-through | Test coverage | Tested `FormatHeadline`'s guards and assumed the duplicated copies in `FormatDetail` behaved identically | One test per guard per code path |
| 13 | MED | `NoteStation_ThrowingLogger_DoesNotPropagate` asserted nothing, so a regression reducing the catch to a bare `catch { }` would still pass | Assertion strength | Treated "does not throw" as the whole contract | Assert the warning is emitted too |
| 14 | LOW | `Start()` set `_started` before subscribing; a throw between the two `+=` would leave the singleton permanently half-subscribed with every later `Start()` a silent no-op | Lifecycle ordering | Latch-then-act ordering written without considering partial failure | Subscribe first, latch after; mirror in `Dispose()` |
| 15 | LOW | "All main-thread-asserted" was overstated: `Debug.FailedAssert` does not throw and nothing checks it, and 2 of 6 raise sites carry no check at all | Doc accuracy | Read the assert as enforcement | Documented as caller discipline, with the accepted consequence stated |
| 16 | LOW | The unmatched-pair rationale cited `CleanAndPushScreen`, which provably fires `OnPopScreen` per removed screen. The tolerance was right for the wrong reason, and the real shape is an unmatched **exit** | Reasoning provenance | Hypothesised a mechanism instead of decompiling it | Corrected in code comment, feature doc and Python docstring |
| 17 | LOW | The anti-drift boundary test pinned one side of the 31646 edge and neither side of 20481, under-delivering on its own name | Test precision | Chose values that looked like boundaries without deriving them | Added 18433/18434 and 28483 |
| 18 | LOW | `classify_stations` computed twice; `MemStation.headroom_mb` was dead surface | Hygiene | Wrote both without asking who consumes them | Threaded as a parameter like `mem`/`timings`; dead property removed |
| 19 | LOW | Four doc counts my diff made staler (21 tests, 18 sections, 18 record types, 182 tests) | Doc drift | Added to the counted sets without updating the counts | Corrected to 64 / 19 / 19 / 211 |

## Root-cause patterns

**A. A claim was mirrored, its guard was not.** Findings 6, 4 and 3 are one shape. In each I copied a
sibling's *structure* and left its *protection* behind: `IsLowHeadroom`'s garbage guard reached the
decision but not the render; `IsValidLabel`'s predicate became an equivalent-looking regex with a
different anchor semantic; `MemoryPressureSampler`'s singleton shape came across along with its
un-reset latch. Mirroring is how this codebase stays consistent, and it silently transports defects
in the same motion.

**B. A test can only fail on what it actually resolves.** Findings 1, 12 and 13. A formatting test
handed a fake engine type name passes exactly as a real one does; a guard tested on one of two
duplicated code paths proves nothing about the other; a test with no assertion pins only the absence
of a throw. All three were green throughout and none of them was testing what its name implied.

**C. Degenerate values reach the render, not just the decision.** Findings 6, 7, 8 and 2. The repo
already has a five-times-shipped lesson about degenerate values defeating guards, scoped to floats
and float-to-int casts. This changeset produced four instances in the `long` and empty-collection
domains: an unguarded negative, an unchecked multiply that wraps to a plausible zero, a reduction
seeded outside its domain, and an empty list inside a truthy container. The category is wider than
the existing rule's scope, which is exactly how that lesson has recurred before.

## Why the agents caught what I did not

- The **API agent** grepped the engine for the type name instead of trusting the prose. That single
  step is the difference between finding #1 and shipping it.
- The **cross-language agent** ran the tool against a constructed log rather than reading the code,
  which is how #2 surfaced with a live traceback.
- The **repeat-defect agent** read the lessons corpus first and matched #3 to two existing entries by
  shape, not by keyword.
- The **tooling agent** executed the PowerShell, including against a locked file and a protected
  process, surfacing #4, #5, #10 and #11 as reproductions rather than suspicions.

The common factor: every finding that mattered came from *executing or resolving* something, not
from reading it. Findings that came from reading alone were, without exception, the LOW ones.

## Lessons to codify

Appended to the category files (not this file, which is the incident report):

1. **`testing-qa.md`**, *A test fixture naming an ENGINE type is a coverage claim; resolve the type
   before you pin it.* A formatting test cannot distinguish a real engine type from a plausible
   invention, so a fake name in a fixture propagates into docs and CHANGELOG as a capability claim
   with a green suite behind it. Resolve every engine type named in a fixture against the installed
   assembly.
2. **`gamemodels-services.md`**, *A guard that rejects an input class must be applied at every site
   that RENDERS that input, not only at the site that DECIDES on it.* Otherwise the artefact prints
   numbers the verdict beside them was computed to ignore, and the two contradict each other in
   front of the reader.
3. **`state-lifecycle-save.md`**, *"Same shape as the sibling" is a design statement, not a
   correctness one.* Before mirroring a class, audit its state for the defect classes in this file;
   otherwise a known-unfixed instance is duplicated rather than caught.
4. **`build-tooling-workflow.md`**, *Mirror a validator's PREDICATE across languages, never an
   equivalent-looking pattern.* `^[...]+$` and a per-character membership test are not the same
   function: .NET's `$` matches before a trailing newline. Same family as the prior C#/Python
   integer-floor drift.

## Codex adversarial round (same day, after the 19 fixes above)

`gpt-5.6-sol` at max reasoning, pointed at the FIXED state and told not to re-report the 19 findings
unless a fix was itself wrong. It returned 4 HIGH, 5 MEDIUM, 4 LOW. **Every HIGH was real and I
confirmed all four against source before acting; none was a false positive.** Three of the four were
defects in the review fixes themselves, which is exactly what that instruction was for.

| # | Sev | Finding | Verified how | Fix |
|---|---|---|---|---|
| C1 | HIGH | **The sampling window excludes the screen's own construction.** `PushScreen` raises `OnPushScreen` AFTER `HandleInitialize`/`HandleActivate`/`HandleResume`, and `PopScreen` raises `OnPopScreen` AFTER `HandleFinalize`. So a screen that allocates on open and retains reads **~0**, and one that allocates on open and frees on close reads **negative** | Read both methods in the installed v1.4.8 decompile myself | Documented in the class, the feature doc and the report OUTPUT; the real fix (a `ScreenBase.HandleInitialize`/`HandleFinalize` seam) is recorded as owed |
| C2 | HIGH | **My finding-#6 fix produced a false healthy verdict.** Suppressing the derived clauses for a rejected commit reading, then still printing "no memory pressure", asserts something the rejected input never established. My own new test pinned the false claim | Read `MemoryPressureVerdict.cs:48` and the test at :139 | Third state: `MEMORY STATUS UNKNOWN - invalid commit reading`, in header and detail, with the detail explicitly saying it is NOT a statement of health |
| C3 | HIGH | **A failed managed-heap read became a measured `heapMB=0`.** `catch { heapMb = 0; }` then `TryRead` returns true, so the new crash verdict renders it as measured and can print `managed 0% of private`, falsely strengthening the native-dominance reading the verdict exists to support. Reachable precisely under an OOM-shaped crash | Read `MemorySampleReader.cs:74-76` | `HeapMbValid` carried on the sample; `SystemMemorySnapshot.ManagedHeapMb` is now `long?`; renders `heapMB=<unavailable>` and omits the share. The numeric LOG token is unchanged, so the pinned cross-language contract is untouched |
| C4 | HIGH | **My finding-#11 fix accepted a stale artifact.** vmmap names are deterministic, so `Test-Path` after a run that wrote nothing passes on a same-named file from an earlier session | Reproduced the predicate in isolation over stale / empty / fresh / missing | Require `LastWriteTime >= invocation` and non-zero length; warn when a stale or empty file is found |
| C5 | MED | The session reset was a silent discontinuity: two 2,000-line segments in one log, with pending enters able to pair across the boundary | Read my own `Start()` | `[MemStation] session-reset` marker emitted on re-entry; the parser segments on it and clears pending enters |
| C6 | MED | A transient reader failure dropped a transition silently, and an unmatched exit was silently discarded, so an incomplete series looked complete | Read both sites | Unmatched exits are counted per screen and surfaced in the report |
| C7 | MED | **My own reset tests never reached the path they were named for.** They never called `Start()` before spending the budget, so the "second" `Start()` was the first and took the subscribe path, not the `_started` early-return. They also leaked a static engine subscription per test | Read the tests | Start first, then spend, then re-Start; assert `SubscribeCount == 1`; dispose every subscribing fixture |
| C8 | MED x2 | `MEMORYSTATUSEX.ullTotalPageFile` is documented as the smaller of the system and process commit limits, and the WMI pair in the PowerShell tool is not the documented exact commit charge either. `GetPerformanceInfo.CommitTotal` is the exact counter | Not re-derived; Codex cited the Microsoft docs | **NOT fixed.** Pre-existing in shipped #386 and mirrored deliberately. Recorded as owed below |
| C9 | LOW | Same-type nested screens escaped overlap detection because the predicate excluded the screen's own name, though `ScreenManager` permits two instances of a type stacked | Read the predicate | Any nested enter counts |
| C10 | LOW | The reordered latch still left the first handler installed if the second `+=` threw | Read `Start()` | Roll back the first subscription and rethrow |
| C11 | LOW | **Remediation drift, twice.** Finding #1 said the fake engine type was removed from the docs, but the feature doc's own example block still used `GauntletEncyclopediaScreen` thirty lines above the paragraph explaining it does not exist. Finding #16 said the teardown rationale was corrected, but the Python docstring still carried the disproven version | Grepped both files | Both corrected |
| C12 | DISPUTED | The `PercentOf` bounds could hide a legitimate reading | Codex computed the thresholds: ~81.92 ZiB and 2.1 billion percent | No change; the guards are corrupt-input only, as intended |
| C13 | DISPUTED | The `FormatSampleTokens` extraction or the sanitizer might break the contract | Codex diffed both and ran the Python suite | No change; confirmed byte-preserving and regex-safe |

### What this round says about the internal review

Nine agents read this code and none found C1, which is the finding that most limits the instrument's
value. The agents that came closest each asked "what does this measure?" and answered from the
subscription site. Codex asked "at what INSTANT does the engine raise this event, relative to the
work the screen does?" and read the raise site's neighbouring statements. That is a different
question, and it is the one that mattered.

C2, C3, C4 and C7 are all defects in fixes written during the review itself. A fix is new code and
deserves the same scrutiny as the code it replaces; the deep-review pass that produced them did not
get one. **Re-reviewing the fixes, not just the original changeset, is the process gap this round
exposes** -- and the instruction to Codex to check whether a claimed fix was actually applied
everywhere is what caught C11 twice.

## Owed

- **An encyclopedia anchor off `MapEncyclopediaView`** (finding #1's real fix, as opposed to its
  honest documentation). `IsEncyclopediaOpen` is a public property with a `protected set` on a
  SandBox base whose behaviour lives in a GauntletUI subclass; it needs research, a patch, a category
  and tests. Until then the live protocol brackets the encyclopedia by hand with `taom.print_memory`.
- **`MemoryPressureSampler`'s own un-reset latches** (`_sessionLineEmitted`, `_warnLatched`,
  `_readFailureWarned`) still carry finding #3's defect. Not touched here because it is pre-existing
  behaviour outside this changeset's scope; it should be fixed with the same `ResetSessionBudget`
  shape.
- **No golden/schema test exists for `JsonCrashReportRenderer`**, so "the JSON shape is unaffected"
  is true only because nothing checks it.
- **A `ScreenBase.HandleInitialize` / `HandleFinalize` seam** so a station pair brackets the screen's
  own construction rather than starting after it (C1). This is the difference between "what happened
  while this was on screen" and "what this screen cost", and the second is the question the
  investigation actually asks. Needs two Harmony patches, a category, a SubModule registration and
  binding tests.
- **Exact commit counters** (C8): `GetPerformanceInfo.CommitTotal` / `CommitLimit` in
  `MemorySampleReader`, and the matching performance counter in `Invoke-CommitMatrix.ps1`, replacing
  `MEMORYSTATUSEX.ullTotalPageFile` and the `Win32_OperatingSystem` pair. Both are pre-existing and
  both are close enough for a delta-based matrix, which is why this round did not change them; they
  are wrong for any absolute claim.
- **Operational note for the capture session:** vmmap BLOCKS when attaching to a process it cannot
  open (observed against a protected process from a non-elevated shell, killed at 2 minutes). Run the
  capture shell elevated and target only Bannerlord.
- In-game smoke of both instruments.
