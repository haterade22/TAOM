# Deep review: plan 036, animation clip memory probe (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: Animation clip memory probe ([AnimMem] lines against the engine's 12 MiB on-demand clip budget), plan 036
Date: 2026-10-02

Scope:   C# (24 files: probe, session, signature scan, PE parser, line formats, two adapters, the
         mission behaviour, module wiring, one setting, one co-op exclusion, 8 test files) and docs
         (feature doc, engine notes, feature map, two co-op docs). Branch perf/036-anim-memory-probe,
         diff 141a20f1..2f560c6c, worktree wt-036.
Blast radius: the reviewed types are new in this diff; the one member this review removed
         (IAnimClipMemoryProbe.DisableForProcess) had one production caller (AnimMemorySession) and two
         tests, by git grep. graphify UNCHECKED: "No unique node match" (see "Blast radius").
Waves:   one wave: Agents 1 (Standards), 2 (Engine compatibility), 3 (Efficiency), 4 (Completeness),
         5 (Data flow), 6 (Design). Agent 7 (XML) NOT IN SCOPE (no XML or XSLT). Tooling NOT IN SCOPE
         (no hook, validator or CI step changed). Codex not run.

STANDARDS:     PASS: 4 LOW violations (lifecycle caller comment, duplicated negative rule, lost tail
               window, unparsable trailers); 3 fixed here, 1 needs a history rewrite (orchestrator)
COMPATIBILITY: PASS: 0 incompatible, 4 unverified (the job handed to 0x44400, the per-call cost of
               IsAnyAnimationLoadingFromDisk, the clip table never reallocating mid-mission, the
               quit-to-menu path; the last one is resolved below)
EFFICIENCY:    PASS: 0 high, 0 medium, 1 low (per-frame counter read; behaviour-changing, not applied)
COMPLETENESS:  INCOMPLETE until the orchestrator files the issue (D4); the issue draft says
               "default-off" while the code ships default on
DATA FLOW:     PASS: 16 flows traced, 2 gaps (tail window, no-write guard), 1 inconsistency
               (silent later missions after a disable; by design)
DESIGN:        3 KEEP proposals (2 apply, 1 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Native facts, re-checked by the lead

Every lens re-derived the native facts on the installed v1.5.3 `TaleWorlds.Native.dll` (14,209,376
bytes). The lead re-ran the two load-bearing disassemblies with `tools/native_sig_author.py disasm`:

- `0x21E00F 8B 05 2B DE B8 00 mov eax,[rip+0xb8de2b]` resolves to the counter at `0xDABE40`;
  `0x21E034 F3 0F 5C 05 A0 02 91 00 subss xmm0,[rip+0x9102a0]` resolves to the budget float at
  `0xB2E2DC`, followed by `cvttss2si` and `js`.
- `0x591319 lock xadd [rip+0x81ab1f], ecx` (the counter), `add ecx,[rdi+0x80]`, then
  `0x591327 cmp ecx, 0xf00000` and `jle`: eviction is scheduled only past 15 MiB, and the pass frees the
  excess over 12 MiB it measured at its start, evicting idle clips only, so it can end above 12 MiB
  (restated in convergence round 1). This is the basis of finding C4.
- `0x6EAAC0` (`GetNumAnimations`) and `0x6EAAE0` (`IsAnyAnimationLoadingFromDisk`) both read the
  vector end at `0xDB00D0`, so the loading query walks the whole animation table (finding C5).

The read-safety order holds (Agents 1 to 6 agree; the lead re-read `AnimMemoryProbe.Arm`): the only
reads before the checks are the 4 KB header page and the `.text` range the headers declare; both
targets pass the alignment and section checks before the first `ReadInt32`; every mismatch goes through
the latched `Disable` with one reason line; the adapter has no write method; `AfterStart`, `Start`,
`Tick` and `End` all catch.

## Findings and classification

| # | Lens | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| C1 | 1 | LOW | `AfterStart` (and the tick and end overrides) quoted no caller line, which `harmony-patches.md` "MissionBehavior lifecycle" requires | CONFIRMED | Fixed: each override quotes its v1.5.3 caller (`Mission.cs:3831`, `:3841`, `MissionState.cs:345`, `Mission.cs:3759`, `:4656`, `:4716`) |
| C2 | 1, 2, 5 | LOW | `End` wrote only the summary, so the window since the last 5 s line lost its min, max and window counts (D6: never drop) | CONFIRMED | Fixed: `End` writes one periodic line for a partial window before the summary. RED then GREEN: `End_WithPartialWindow_LogsTailLineBeforeSummary`; `End_RightAfterAPeriodicLine_LogsNoTailLine` pins no duplicate |
| C3 | 2 | LOW | The summary was written only from `OnEndMission` | CONFIRMED in part | The quit-to-menu path is a false positive: `MBGameManager.EndGame` calls `EndMission` and waits for the mission (`MBGameManager.cs:202-208`), so `EndMissionInternal` runs. But a teardown that never calls `Mission.EndMission` skips `EndMissionInternal`, while `OnMissionStateFinalize` removes every behaviour (`Mission.cs:2228`, `:4716`). (This row first said a network client never reaches `EndMissionInternal` because `CheckMissionEnd` is gated on `!GameNetwork.IsClient`, `:4852`; that gate covers only the first branch, and a client reaches it through the else branch at `:4885-4888`. Corrected in convergence round 1.) Fixed: `OnRemoveBehavior` ends the session too; the session is null after a normal end, so nothing logs twice. Test `Behavior_EndsTheSessionOnRemoveBehaviorToo` (it pinned only `base.OnRemoveBehavior();` until convergence round 1, which pins the `EndSession();` call) |
| C4 | 2 | LOW | `mission-perf-heartbeat.md:53` said the engine holds only 12 MiB before evicting; eviction is scheduled past 15 MiB (`0x591327`) and the pass frees the excess over 12 MiB it measured at its start (convergence round 1: not a guarantee of 12 MiB) | CONFIRMED | Fixed in the feature doc (Overview and "Reading it", with the inference kept as such). The same wording in the engine notes (`:151`, older text) was corrected after convergence round 2, with the Eviction and Probe rows; the first sentence of the `2f560c6c` body is in NEEDS MIKE 4 |
| C5 | 2, 4 | LOW | The cost paragraph did not say `IsAnyAnimationLoadingFromDisk` walks every clip record | CONFIRMED | Fixed in the feature doc |
| C6 | 4, 5 | LOW | `Behavior_MakesNoNativeWrite` banned three strings but not `Marshal.Copy(byte[], int, IntPtr, int)`, the write overload of the adapter's own call, nor `unsafe` | CONFIRMED | Fixed: every `Marshal.Copy(` in the guarded files must take `new IntPtr(` first, and `unsafe` is banned. Proven by mutation (swapping the adapter's arguments fails the test) |
| C7 | 1, 4 | LOW | Untested guard branches: `ReadLoadedBytes` before arming, a negative first read in `Start`, `Pct` with a zero budget, `PeSectionTable.Parse` with null, a short buffer or a negative `e_lfanew` | CONFIRMED | Fixed: five tests, each proven by mutating its guard |
| C8 | 4 | LOW | The feature doc's `## Configuration` named only the heartbeat toggle | CONFIRMED | Fixed |
| C9 | 3 (noted outside its lens) | LOW | After a mid-mission disable, the disabled line said "No [AnimMem] samples will be logged", yet the summary of the samples already taken still prints | CONFIRMED | Fixed: "No further [AnimMem] samples will be taken". RED then GREEN: `Disabled_FormatsReason` |
| C10 | 1 | LOW | The `Refs:` and `Not-tested:` trailers of `2f560c6c` do not parse (`git interpret-trailers --parse` prints nothing; the wrapped continuation lines are not indented) | CONFIRMED | Not fixable here: it needs a history rewrite, which this role may not do. Orchestrator: reword at squash or merge. This review's own commit indents its continuation lines |
| F1 | 1 (nit) | - | "Use a `using` for `System.Globalization.CultureInfo` in `AnimMemoryProbe.cs`" | FALSE POSITIVE | `TAOM.Adapters.CultureInfo` exists, and the short name fails with CS0104 (tried this run); the full name is required |
| F2 | 5 | - | Later missions skip with no line once the probe is disabled for the process | FALSE POSITIVE (by design) | The disable line is in the same per-launch log and says no further samples follow |
| N1 | 1 to 6 | - | `EnableAnimMemoryProbe` defaults to true for every player | NEEDS MIKE | See below |
| N2 | 3, 5 | - | Read the counter every frame so `drops`, min and max see evictions inside one second | NEEDS MIKE | Behaviour-changing; Agent 6 argued against it. See below |
| N3 | 4 | - | The issue is not filed (D4), and its draft says "default-off" | NEEDS MIKE | Orchestrator files it; the draft must match the default Mike picks; label `triage-needs-ingame` |

## NEEDS MIKE

1. **Default on for every player.** Cost per armed mission, from the lenses' evidence: one aligned read
   and one native walk over every clip record per second; one INFO line every 5 s, plus a start line and
   one or two lines at the end, each a synchronous `WriteLine` and `Flush` under `_writeLock` on the
   main thread (`Main/Core/Logging/FileLogger.cs:85`, `:90-129`). That doubles the `[MissionPerf]` rate,
   about 110 KB of log per hour of missions. Once per process: a copy and scan of about 10 MB of `.text`
   under the first mission's loading screen (Agent 3 measured 25 ms unoptimised in a replica). The page
   persists as json2, so a default flipped after release never reaches players who saved it: rename the
   property to change it later. Options: keep on (D6, D7 "diagnose now"); default off after the two
   measurement battles; or keep on with the 5 s line at DEBUG (conflicts with D6's INFO reading).
   FOR-MIKE's measurement step 1 says to turn the probe on and restart, which contradicts the shipped
   default on and `RequireRestart = false`. The Codex review of 2026-10-03 adds two facts to weigh: the
   loading query's table lifetime is untraced, and neither its per-second cost nor the synchronous flush
   has been measured in a battle (last section).
2. **Per-frame counter read** (Agent 3 finding 1, Agent 5 trace 16). About 5 ns per frame would let
   `drops`, `minKB` and `maxKB` see a load and an eviction inside the same second. It changes the
   in-game meaning of those fields and the plan specified 1 s; Agent 6 rejected it, because a completed
   eviction falls from above 15 MiB to at most 12 MiB, which a one-second sample misses only when as many
   bytes reload in the same second. Decide after the first battle logs. (Convergence round 1: the
   "at most 12 MiB" premise holds only when a pass reaches its target; clips in use, or loads that land
   during the pass, can leave the total above 12 MiB. The decision stays Mike's.)
3. **Issue text.** The draft in the run folder (`issue-drafts.md:110`) said "default-off"; it must
   match the default chosen in item 1. (After convergence round 2 the orchestrator changed the draft
   and the first measurement session's step 1 in `FOR-MIKE.md` to the shipped default, on, with the
   choice left to Mike.)
4. **Commit messages to reword before merge.** Branches land as merge commits, so each is a history
   rewrite on this branch before it merges; otherwise the release note repeats false claims:
   - the `Refs:` and `Not-tested:` trailers of `2f560c6c`, which do not parse (C10);
   - the first sentence of the `2f560c6c` body, "The engine keeps only 12 MiB of on-demand animation
     clip data and evicts clips past that" (C4);
   - in the `78f654d9` body, "a network client never reaches Mission.EndMissionInternal, so it had no
     summary" (R1-1) and "trims it back to the 12 MiB budget" (R1-3);
   - in the `97b97c50` body, "and the feature doc says so and names the A/B that measures it" (R3-3):
     the feature doc says the heartbeat's A/B only bounds the aggregate effect, and names plan 041's
     per-call timing and a `Stopwatch` around a sample and its INFO write as the direct measurements
     (the `7a596a01` body states the correction).
5. **In-game check owed:** one Custom Battle run, which `harmony-patches.md` requires the first time an
   `AfterStart` added by `AddTaomBehavior` runs, and a spot check of the armed header, one 5 s line, the
   tail line and the summary in a real `taom_debug_*.log`.

## Blast radius

`python tools/graphify_taom.py affected "IAnimClipMemoryProbe" --depth 2`, after
`refresh --if-stale`, printed "No unique node match for IAnimClipMemoryProbe": the graph does not hold
this branch's new types, so graphify is UNCHECKED here. `git grep DisableForProcess` before the change
found the probe, the interface, the session and two tests only (plus the plan file); after it, the plan
file only. Every other type in the diff is new.

## Test evidence

- Base (`2f560c6c`, this run): `Failed! - Failed: 1, Passed: 12416, Skipped: 2, Total: 12419`; the
  failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- RED: `Failed! - Failed: 3, Passed: 76, Skipped: 0, Total: 79` (the three behaviour tests:
  `Disabled_FormatsReason`, `ReadLoadedBytes_NegativeAfterArming_DisablesForProcessWithOneLine`,
  `End_WithPartialWindow_LogsTailLineBeforeSummary`).
- Mutation run for the guard and coverage tests (files copied to scratch, mutated, restored by copy):
  `Failed! - Failed: 8, Passed: 71, Skipped: 0, Total: 79`, the eight being
  `Behavior_EndsTheSessionOnRemoveBehaviorToo`, `Behavior_MakesNoNativeWrite`,
  `ReadLoadedBytes_BeforeArming_Throws`, `MissionStart_ZeroBudget_PctIsZero`,
  `Parse_NullOrShorterThanDosHeader_ReturnsNull`, `Parse_NegativeELfanew_ReturnsNull`,
  `Start_NegativeFirstRead_LogsNothingAndStops` and `Tick_NegativeRead_StopsWithoutAnError`.
- GREEN, feature filter: `Passed! - Failed: 0, Passed: 79, Skipped: 0, Total: 79`.
- Reference-assembly unit step (`.github/workflows/csharp.yml` filter, game variables unset):
  `Failed! - Failed: 3, Passed: 10119, Skipped: 28, Total: 10150`, the three known failures
  (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `Patch93_HasTheSevenPatchesInItsCategory`,
  `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`). The executor's run had 10111 passed: +9 new tests,
  -1 removed.
- Final full suite: `Failed! - Failed: 1, Passed: 12424, Skipped: 2, Total: 12427`; the failure is the
  known `EveryLanguage_DeclaresARowForEveryEnglishKey`, as at the base (+8: 9 tests added, 1 removed).
- `python -B tools/lint_docs.py --fail-on-drift`: exit 0.

## IMPROVEMENTS (Step 4)

APPLIED:
- `AnimMemoryProbe.cs` `ReadLoadedBytes` and `Arm`, `IAnimClipMemoryProbe.cs`, `AnimMemorySession.cs`
  `TakeSample`: the negative-counter rule and its reason text now have one owner, the probe's private
  `Checked`; `DisableForProcess` left the interface (Agents 1 and 6, PRESERVING: the same line is logged
  once, the session stops the same way). Proof: `ReadLoadedBytes_NegativeAfterArming_DisablesForProcessWithOneLine`
  (RED, then GREEN), `EnsureArmed_CounterNegative_DisablesWithReason`, `Tick_NegativeRead_StopsWithoutAnError`
  and `Start_NegativeFirstRead_LogsNothingAndStops`, green after.
- `AnimMemorySession.cs`: `_hasPrevious` deleted; a drop is `_samples > 0 && bytes < _previous`
  (Agent 6, PRESERVING). Proof: `Tick_AtFiveSeconds_LogsPeriodicLine` (drops=2),
  `Tick_DropAcrossWindowBoundary_CountsInTheNextWindow` (drops=1) and `End_LogsSummary` (drops=2), green
  before and after.
- `AnimMemoryProbeMissionBehavior.cs`: the redundant `using` of its own parent namespace removed
  (Agent 1 nit).

Convergence pass (Step 4.6): not run. This review lead cannot spawn agents; the orchestrator owns the
one `deep-reviewer` pass over this commit's diff.

NOT APPLIED:
- `AnimMemorySession.cs:73-76`, per-frame counter read (Agents 3 and 5): behaviour-changing; NEEDS MIKE 2.
- `AnimMemoryProbe.cs`, `using System.Globalization` (Agent 1 nit): disproved, CS0104 against
  `TAOM.Adapters.CultureInfo`.

FOLLOW-UP (pre-existing code or outside this diff; no issue filed by this role, the orchestrator
owns issues):
- Plan 041 adds the same `IAnimationLoadingAdapter` pair: keep one adapter and one owner of the loading
  flag at merge (Agent 6); never two DryIoc registrations (Agent 1).
- `AnimMemoryProbeMissionBehavior` and `MissionPerfHeartbeatBehavior` read the MCM static directly; route
  both through `IBattleLoadDiagnosticsSettingsProvider` after plan 028 merges.
- `docs/features/battle-load-diagnostics.md` Configuration table lacks both Mission Performance toggles
  (the plan deferred it); `mission-perf-heartbeat.md:38` says the heartbeat toggle is read once per tick,
  the code reads it once a second.
- Engine notes `mission-frame-threads-and-native-costs.md:140` ("walks the on-demand clip records": it
  walks every record) and `:151` ("only 12 MiB stays resident": up to 15 MiB before a pass); strengthen
  the eviction-scheduling inference with the once-flag evidence (`0xDABE44` cleared at `0x21E151` and
  `0x21E165`, pass pointer in `.rdata` at `0xAE59F8`). Done after convergence round 2, with the
  Eviction row (`:139`), which round 2 found: the three rows now follow the decompiles of `0x21DEA0`,
  `0x6EAAE0` and `0x5911A0`, and the scheduling note says the pass clears `0xDABE44` at every return.
  Still open: tracing the `.rdata` pass pointer, which would turn the scheduling inference into a fact.
- `/engine-bump` Phase 4 runs no LiveInstall test, so only an unfiltered local run catches drift of this
  signature (the probe itself disables with one line). Add one line naming
  `ClipBudgetSignatureInstalledBinaryTests`.
- A future budget raise that patches the float switches this probe off (the exact `12582912` check):
  build the two together.
- `tools/native_sig_author.py xref` decodes `.text` linearly and stops at RVA `0x48B38`; it reported 0
  references to `0xDABE40` and `0xB2E2DC` with no warning (Agents 2 and 5). Route to `/investigate`; until
  then use a brute-force disp32 scan.
- Plan 041, not plan 028 (plan 028 leaves the clip-loading flag to it), samples
  `IsAnyAnimationLoadingFromDisk` every frame: a walk over every clip record per frame. It times 32 calls
  first and samples only when the median is within 20 us. (This line first named plan 028; corrected in
  convergence round 3.)

## CODEX REVIEW

Codex not run: no paid dispatch was authorized for this item. (One was run afterwards; see the last
section, "Codex review (2026-10-03)".)

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| - | - | - | - | Codex not run |

AGENTS.md lessons (pending): none from Codex (not run). For the orchestrator's consolidation, two
Claude-side patterns worth a "Look harder here" line: a source-text guard against a dangerous API must
name the overload direction, not only the method name (C6); a per-mission summary on `OnEndMission`
alone misses a teardown that never calls `Mission.EndMission`, and a claim that a path never reaches a
method needs every branch of the gate read, the else included (C3, as corrected in convergence round 1).

VERDICT: READY FOR COMMIT (every confirmed defect in the changed code is fixed; the commit-message
corrections listed in NEEDS MIKE 4, C10 among them, need a history rewrite and are the orchestrator's
before merge)

## Convergence round 1

One `deep-reviewer` pass over `2f560c6c..78f654d9` found four LOW items. The review lead re-checked each
against the code and the installed v1.5.3 engine before acting. All fixes are in the commit
`fix(mission-perf): v2.0.32 - convergence fixes for plan 036`, the commit after `78f654d9`.

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| R1-1 | LOW | The reason given for C3 is false: "a network client never reaches `EndMissionInternal`" | CONFIRMED | Fixed (wording only). `taom-src` v1.5.3 `Mission.cs:4850-4889`: `!GameNetwork.IsClient` guards only the first branch of `CheckMissionEnd`; the `else if (CurrentState != State.Continuing && currentTime > NextCheckTimeEndMission)` branch (`:4885-4888`) runs `EndMissionInternal` on a client once `EndMission` (`:4635-4642`) has set `EndingNextFrame`. The `OnRemoveBehavior` fallback stays, for a teardown that never calls `EndMission`: `MissionState.OnFinalize` calls `OnMissionStateFinalize` unconditionally (`MissionState.cs:49-52`), which removes every behaviour (`Mission.cs:2228`, `:4716`). Reworded: the code comment, the test comment, the lesson in `lessons/state-lifecycle-save.md` (heading, body, "Why missed" and "Prevent"), RCA C3 and the root-cause and Agent 2 lines, this record's C3 row and closing note, and the REVIEW-LOG scope line and C3 row. The body of `78f654d9` carries the same false sentence and needs a reword at squash or merge, as for C10 |
| R1-2 | LOW | `Behavior_EndsTheSessionOnRemoveBehaviorToo` passed with `EndSession();` deleted | CONFIRMED | Fixed. The test now requires the comment-stripped override body to be exactly `EndSession();` then `base.OnRemoveBehavior();`, and `OnEndMission` to be `=> EndSession();`. RED against a mutant whose `OnRemoveBehavior` only calls base (source copied to scratch, mutated, restored by copy, `cmp` identical): `Failed! - Failed: 1, Passed: 6, Skipped: 0, Total: 7`, the failure being this test. GREEN, feature filter: `Passed! - Failed: 0, Passed: 79, Skipped: 0, Total: 79`. The feature doc's test list (`OnRemoveBehavior` ends the session) and the `78f654d9` trailer are now accurate for this unit test |
| R1-3 | LOW | "Each pass trims it to 12 MiB or less" is not what the binary does | CONFIRMED | Fixed. `python tools/native_decompile.py --rva 0x21E00F` (exit 0) decompiles `FUN_18021dea0` (entry `0x21DEA0`): the excess `(float)(int)DAT_180dabe40 - _DAT_180b2e2dc` is computed once; the loop stops once the bytes freed reach it; a record is evicted only when `+0xE0 == 2` and `+0xD8 == 0`, after a locked swap of `+0xD8` to `0xFFFFFFFF`; an exhausted list clears `DAT_180dabe44` and returns. Reworded in the feature doc (Overview and "Reading it"), the lesson in `lessons/native-cpp-port.md`, RCA C4, this record's native facts and C4 row, and the premise of NEEDS MIKE 2 (now noted as holding only when a pass reaches its target; the decision stays Mike's). The body of `78f654d9` ("trims it back to the 12 MiB budget") needs the same reword at merge |
| R1-4 | LOW | Default on for every player: cost against the simplicity criterion; `FOR-MIKE.md` says to turn the probe on and restart | NEEDS MIKE | Not changed, by the orchestrator's instruction. Verified: `BattleLoadDiagnosticsSettings.cs:66` defaults to `true` with `RequireRestart = false` (`:64`); `FileLogger.cs:85` routes `LogInfo` through the durable path; `AnimMemorySession.cs:17` sets `LineSeconds = 5.0` and `:120` calls `IsAnyClipLoading` on each one-second sample. The cost of that walk per call is UNVERIFIED (it needs a running game). `plans/_audit/2026-10-02-perf/FOR-MIKE.md:98` (step 1 of the first measurement session) says to turn on the anim memory probe and restart, which contradicts default on and no restart; that file is the orchestrator's, so this role did not edit it. Mike's options, from the finding: keep on (D6, D7 "diagnose now"); ship off and turn it on for the two measurement battles; or keep on and first time the walk (for example, the window's slowest sample in microseconds). FOR-MIKE and the issue draft's "default-off" text follow his choice |

Focus checks for this round. The native read order is unchanged by this round (no code path changed;
the only C# edits are one comment and one test). Every test that reads the installed
`TaleWorlds.Native.dll` is in `ClipBudgetSignatureInstalledBinaryTests`, tagged `LiveInstall` at class
level (`:15`); the other files only use the name as a string or a stub. The setting is read in one
place (`AnimMemoryProbeMissionBehavior.cs:39`), gates only logging, and is listed as instrumentation
(`CoopSettingsRelevance.cs:74`), so a peer with a different value changes nothing simulated.

Evidence, this round:

- Base (`78f654d9`): `Failed! - Failed: 1, Passed: 12424, Skipped: 2, Total: 12427`, the known
  `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- Reference-assembly unit step (`.github/workflows/csharp.yml` filter, game variables unset):
  `Failed! - Failed: 3, Passed: 10119, Skipped: 28, Total: 10150`, the three known failures
  (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `Patch93_HasTheSevenPatchesInItsCategory`,
  `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`).
- Final full suite: `Failed! - Failed: 1, Passed: 12424, Skipped: 2, Total: 12427`, the same known
  failure as at the base (one test strengthened, none added).
- `python -B tools/lint_docs.py --fail-on-drift`: exit 0.
- Gate sweep: none; no hook, validator or CI step changed.

## Convergence round 2

The convergence reviewer read `78f654d9..89f1efea` and raised two LOW findings, both in the records.
The review workflow's last round runs no fix pass, so the orchestrator checked both and closed them in
the commit `docs(mission-perf): v2.0.32 - record plan 036's convergence rounds`.

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| R2-1 | LOW | Round 1 created two owed corrections to the `78f654d9` body (R1-1, R1-3) but recorded them only in those table rows; NEEDS MIKE 4, the VERDICT and the REVIEW-LOG entry named only C10, the entry recorded nothing of round 1, and the C4 row pointed at a FOLLOW-UP item that did not exist | Confirmed: `git log -1 --format=%B 78f654d9` carries both sentences, and branches land as merge commits with no reword step, so the release note would repeat them | NEEDS MIKE 4 is now the one list of commit-message corrections (C10, C4, R1-1, R1-3), the VERDICT and the C4 row point at it, and the REVIEW-LOG entry's heading, **Claude** line and owed list record both rounds and the full list |
| R2-2 | LOW | The engine notes' Eviction row (`mission-frame-threads-and-native-costs.md:139`) said the pass frees clips while the live counter exceeds 12 MiB; the binary measures the excess once and stops when it has freed that much | Confirmed by the orchestrator's own `python tools/native_decompile.py --rva 0x21E00F` (exit 0): the excess is computed once before the loop, the loop exits when the freed total reaches it, and only state-2 clips with no readers are freed | Corrected in the engine notes, with the Probe row (`:140`, from the decompile of `0x6EAAE0`) and the residency bullet (`:151`, from the decompile of `0x5911A0`: a load past `0xF00000` sets the pass flag) |

No code changed in round 2. The RCA gains a "Convergence rounds" section covering both rounds.

## Codex review (2026-10-03)

After the convergence rounds the orchestrator ran one Codex adversarial review over `141a20f1..50df8cbf`,
the branch head (issue #718). Codex read the diff from git and inspected the installed v1.5.3
`TaleWorlds.Native.dll` and the decompiled engine read-only; it ran no build, test or game, and it
established no crash. It raised three items. The fix pass that follows re-read each against the code and
the installed binary (`.claude/pinned-game-version.txt` `v1.5.3`, `TaleWorlds.Native.dll` 14,209,376
bytes) before changing anything. Of the suspects it was handed, Codex disputed those about the
environment and reachability (version and size, signature and RVAs, a pattern forced to be unique, net472
APIs, protected files, the plan's excerpts, stale lifecycle state, dead code, a moved structure), left
the verification-history suspect UNVERIFIED, and confirmed the tests that do not prove behavior (X2) and
the fail-enabled default (NEEDS MIKE 1); none of the disputed ones needed action.

| # | Codex | Finding | Verdict | Action |
|---|---|---|---|---|
| X1 | P2 | The feature doc, the plan and the MCM hint present a low sampled total as ruling out a remedy ("a total far below 12 MiB rules both out") | CONFIRMED | Feature doc, plan, engine notes, the MCM hint and two code comments corrected; no behaviour changed |
| X2 | P3 | `Behavior_EndsTheSessionOnRemoveBehaviorToo` reads source text, so it proves neither that a callback writes the summary nor that the second callback is silent | CONFIRMED | `AnimMemoryProbeMissionBehaviorTests`: seven behavioural tests (RequiresGame). The source test stays, for hosted CI, with a comment that says what it pins |
| X3 | P3 observation | "Every address is proven" says more than the code checks; the module's lifetime is assumed; the loading query walks a native list with no lock; the rollout cost is unmeasured | CONFIRMED as a documentation gap | The probe's comments, the probe interface, the feature doc, the engine notes and the plan now separate what is checked, what is trusted and what is untraced, and record the cost as unmeasured. The default and the loading query are unchanged: Codex's "keep that exposure opt-in" is the maintainer's call (NEEDS MIKE 1) |

### X1: what the binary and the decompile show

- Clip acquire, `0x474140`, disassembled this pass. For a type 2 clip whose loaded byte (`+0xDC`) is
  clear, the thread swaps the state at `+0xE0` from 0 to 1 (`0x4741EF`), starts the loader (`call
  0x474660`), and waits on the clip's mutex and condition variable (`0x474224` to `0x474280`) until the
  loaded byte at `+0x180` is set. No instruction of the function names the byte counter or the budget:
  the four direct references to the counter (`0x21E00F`, `0x21E0EF`, `0x591319`, `0x830A3`) and the one
  to the budget (`0x21E034`) are all in other functions. The record initialiser (`0x473090`) stores 0 in
  the reader count, the loaded byte and the state (`xor r15d, r15d`, then three stores), so an on-demand
  clip starts unloaded. A first use blocks whatever the total.
- `Mission.cs:3546-3552` (`OnPreTick` runs `WaitTickCompletion()` first), `:3756-3760` (`tickCompleted =
  false`, then every behavior's `OnMissionTick`) and `:3784-3791` (the agent tick is launched after
  them): the probe samples after the previous parallel agent tick finished and before the next starts,
  so a load that delayed that tick has ended when `loadingNow` is read.
- So occupancy can inform the budget lever and cannot reject the resident-clip lever, and `drops` is a
  count of decreasing one-second intervals: a pass followed by a reload of the same bytes inside one
  second leaves none.

### X2: mutation proof for the new tests

Each mutant was applied to `AnimMemoryProbeMissionBehavior.cs`, run with the new class and
`AnimMemoryProbeWiringTests`, and the file restored by copy (`cmp` identical, SHA-256 unchanged).

| Mutant | Totals line | Failing |
|---|---|---|
| M1: delete `_session?.End(Seconds());` | `Failed!  - Failed: 3, Passed: 11, Skipped: 0, Total: 14` | `OnEndMission_AfterStart_WritesTheSummaryOnceAsTheLastLine`, `OnRemoveBehavior_WithoutOnEndMission_WritesTheSummaryOnce`, `OnRemoveBehavior_AfterOnEndMission_WritesNothingMore`; the old source test stayed green |
| M2: delete `_session = null;` | `Failed!  - Failed: 2, Passed: 12, Skipped: 0, Total: 14` | `OnRemoveBehavior_AfterOnEndMission_WritesNothingMore`, `OnMissionTick_AfterTheSessionEnded_TakesNoSample`; the old source test stayed green |
| M3: `OnRemoveBehavior` stops calling `EndSession()` | `Failed!  - Failed: 2, Passed: 12, Skipped: 0, Total: 14` | `OnRemoveBehavior_WithoutOnEndMission_WritesTheSummaryOnce` and the old source test |
| M4: `OnEndMission` does nothing | `Failed!  - Failed: 4, Passed: 10, Skipped: 0, Total: 14` | three new tests and the old source test |
| M5: `_session?.End` becomes `_session!.End` | `Failed!  - Failed: 3, Passed: 11, Skipped: 0, Total: 14` | `TeardownCallbacks_WhenTheProbeNeverArmed_WriteNothing`, `OnRemoveBehavior_AfterOnEndMission_WritesNothingMore`, `OnEndMission_ThenOnRemoveBehavior_TakesNoFurtherSample` |
| M6: one more sampling tick before `End` | `Failed!  - Failed: 2, Passed: 12, Skipped: 0, Total: 14` | `OnEndMission_ThenOnRemoveBehavior_TakesNoFurtherSample`, `OnMissionTick_AfterTheSessionEnded_TakesNoSample` |
| M7: `OnMissionTick` does nothing | `Failed!  - Failed: 1, Passed: 13, Skipped: 0, Total: 14` | `OnMissionTick_WhileTheSessionRuns_SamplesOnceTheSecondHasPassed`, the control that keeps the aged-clock test from being vacuous |

M1 and M2 are Codex's two surviving regressions: the old source test stayed green under both. The
unmutated tree: `Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14`.

### X3: native evidence and what stays open

- `0x6EAAE0`, disassembled this pass: it computes the count once from the list's end and begin pointers
  (`0xDB00D0`, `0xDB00C8`), then for each index re-reads the begin pointer, loads the record pointer and
  tests the state word at `+0xE0` against 1. The loop holds no call, no lock-prefixed instruction and no
  thread check. The managed chain is `MBAnimation.IsAnyAnimationLoadingFromDisk()` to
  `MBAPI.IMBAnimation.IsAnyAnimationLoadingFromDisk()`, which add none.
- A brute-force rip-relative scan of `.text` (forms with 0, 1, 2 or 4 trailing immediate bytes, each hit
  confirmed by decoding the instruction) finds 96 sites naming the begin pointer: 92 `mov` reads, 2
  `lea`, 1 `sub` and one store (`0x55A748`, in the function around `0x55A3A0`); and 12 naming the end
  pointer, one of them a store (`0x55A751`, a 16-byte `movdqa`). The record initialiser `0x473090` has
  one direct caller, `0x5925AA`, in the function at `0x592520`. Nothing was concluded from this: an
  append or a free through a pointer to the list is invisible to such a scan, so whether the list can
  change while a mission runs is still untraced. That stays UNVERIFIED, as the deep review's lens 2
  left it.
- Rollout cost: `FileLogger.LogInfo` is durable (`Enqueue(..., durable: true)` then `Drain()`, which
  writes and flushes under `_writeLock` on the calling thread, `FileLogger.cs:85-129`), so each INFO line
  costs the main thread a flush. The per-call cost of the walk and the flush were not measured in a
  battle. The feature doc says so. (It first gave the toggle-off against toggle-on A/B as the way to
  measure them; convergence round 3 showed that A/B only bounds the aggregate effect, and the doc now
  names plan 041's per-call timing and a `Stopwatch` around a sample and its INFO write.)

### Not applied, and a precision fix outside the three items

- "Keep that exposure opt-in" or "omit the loading query" (X3): a behaviour change, and the default is
  the maintainer's decision (NEEDS MIKE 1, which also covers the persisted json2 default). Not applied.
- The `?? true` fallback when no MCM instance exists follows the plan's explicit expression and the
  page's "diagnose now" posture. Unchanged.
- Codex's note that `BudgetBytes` is cached after arming (a later budget patch would go undetected)
  is the limitation already listed under FOLLOW-UP for a future budget lever. Unchanged.
- Outside the three items, Codex observed that "before the first mission tick" in the behavior's comment
  is true only of this behavior's own ticks: the engine runs two preliminary `Tick(0.001f)` calls
  (`MissionState.cs:338-341`) before `AfterStart`. CONFIRMED against the decompile; the comment and the
  plan's engine fact now say so.

### Evidence, this pass

- Base, as recorded for `89f1efea` (the one later commit, `50df8cbf`, changes docs only): `Failed: 1,
  Passed: 12424, Skipped: 2, Total: 12427`. This pass reproduced it on the final tree with the new class
  excluded: `Failed!  - Failed: 1, Passed: 12424, Skipped: 2, Total: 12427`.
- Final full suite: `Failed!  - Failed: 1, Passed: 12431, Skipped: 2, Total: 12434`; the failure is the
  known `EveryLanguage_DeclaresARowForEveryEnglishKey` (+7: the new class).
- Reference-assembly unit step (the `.github/workflows/csharp.yml` filter, game variables unset):
  `Failed!  - Failed: 3, Passed: 10119, Skipped: 28, Total: 10150`, the three known failures; the new
  class carries `RequiresGame`, so the total is unchanged.
- Feature filter `FullyQualifiedName~AnimMemory`: `Passed!  - Failed: 0, Passed: 86, Skipped: 0, Total: 86`.
- No Python test was run: no file under `tools/` changed.

## Convergence round 3

The convergence review of the Codex fix pass (`97b97c50`, on `50df8cbf`) raised four LOW findings. Each is a
leftover: a claim that pass corrected (X1 to X3) still stood in a text it did not reach. The fix pass that
follows re-read each against the code, the plans, the other perf branches and the installed v1.5.3
`TaleWorlds.Native.dll` before changing anything (issue #718). Comments and docs only: no behaviour, test or
setting changed.

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| R3-1 | LOW | `AnimMemoryProbeMissionBehavior.cs:10-11` still said the choice between making hot clips resident and raising the budget rests on the `[AnimMem]` measurement (X1's framing) | CONFIRMED: the Codex fix pass edited lines 13-14 of that summary only, and the feature doc says the probe "does not decide the other lever" | Reworded: raising the budget rests on a measurement of budget pressure; making hot clips resident also needs hitch timing. The plan's commit-body draft says the measurement is "evidence for the choice", which claims no more, and stays |
| R3-2 | LOW | `INativeModuleMemoryAdapter.cs:4-6`, `PeSectionTableTests.cs:8-9` and the plan's Step 7 summary still said every address read lies inside a mapped section (X3's overclaim) | CONFIRMED: `AnimMemoryProbe.Arm` copies the 4 KiB header page at the module base (`:72`) before any section is known, then the `.text` range named by that same table (`:87-88`). A read-only PE parse of the installed `TaleWorlds.Native.dll` (14,209,376 bytes, `SizeOfHeaders` `0x400`) puts the first section, `.text`, at RVA `0x1000`, so RVA `0` to `0xFFF` lies in none | The adapter says callers pass addresses inside the module's mapped image and that it checks nothing; the test summary says the parser finds the code range and checks the two targets; the plan's Step 7 text matches the adapter |
| R3-3 | LOW | The Cost text told readers to measure the walk and the flush with the heartbeat's A/B | CONFIRMED, with the p95 sub-claim refined (below). `AnimMemorySession.cs:18` and `:73-76` sample once a second and `:79-81` writes one line per 5 s; "Reading an A/B" asks for three runs per cell, not two | The doc says the A/B only bounds the aggregate effect, to compare `maxMs` too, and names the direct measurements: plan 041's `anim-loading sample` line (the median of 32 timed calls, `AnimLoadingSampler.MeasureCost`) and a `Stopwatch` around a sample and its INFO write. Two records that repeated the claim are corrected: the REVIEW-LOG NEEDS MIKE item and the rollout-cost note under X3 above |
| R3-4 | LOW | The feature doc and the engine notes name plan 028 for the clip-loading record per frame and per hitch | CONFIRMED: `plans/028-mission-tick-profiler.md:1277-1279` lists "the clip-loading flag" as out of scope ("They are plan 041"). `git grep IsAnyAnimationLoadingFromDisk` over `Main` and `docs/features` finds nothing on `perf/028-mission-tick-profiler`; on `perf/041-profiler-extensions-and-hitch-probe` it finds `AnimLoadingSampler.cs`, and `HitchProbeLines.cs:148` writes `animLoading=` on the `[HitchDetail]` line. `Mission_OnPreTick_HitchProbe_Patch.Prefix` samples before the original `OnPreTick` runs, and that body starts with `WaitTickCompletion()` | The feature doc and the engine notes give plan 028 the wait and agent-tick timing and plan 041 the clip-loading sample; this report's FOLLOW-UP line and plan 036's maintenance note are corrected |

### R3-3: what the A/B can and cannot resolve

The sampling cadence makes the effect small on every column. A paired simulation of the heartbeat's own
statistic (`FrameStats`: the mean of all frames, the nearest-rank p95, the maximum) shows how small. It is a
model and not a measurement: 20,000 windows of 300 frames (5 s at 60 fps), frame times lognormal around
16.7 ms (sigma 0.12) with 1% of frames 2 to 4 times slower, five random frames per window carrying X ms, the
same base frames with and without X. The script is a scratch file and is not kept.

| X (ms) | `avgMs` shift | `p95Ms` shift | `maxMs` shift | Windows where `maxMs` changed |
|---|---|---|---|---|
| 0.5 | 0.0083 | 0.0103 | 0.0085 | 1.7% |
| 2 | 0.0333 | 0.0671 | 0.0422 | 2.5% |
| 10 | 0.1667 | 0.5622 | 0.5047 | 8.0% |
| 30 | 0.5000 | 0.5590 | 3.3193 | 26.2% |

With no cost at all, the window-to-window standard deviation in the same model is 0.233 ms for `avgMs`,
0.358 ms for `p95Ms` and 12.9 ms for `maxMs`, a floor on the spread of real battles, which are not
stationary. So the finding's core holds: `avgMs` moves by exactly X/60, and none of the three columns
resolves a cost of a few milliseconds on one frame in sixty. Its sub-claim that `p95Ms` "barely moves" does
not: five raised frames can push a nearest-rank percentile up to five places along the sorted window, and
the model shows it moving by about twice the `avgMs` shift at X = 2 ms (still well inside one window's
spread). The
doc therefore says "at most five places", not "barely". `maxMs` stays in the advice because it is the one
column that follows a single long stall (a mean shift of 3.3 ms at X = 30 ms), and the doc says it changes
only when the stalled frame becomes the window's slowest.

### Evidence, this pass

- No test was added. Comments and docs are asserted by no test in this feature (the wiring tests read source
  with comments stripped, `RepoPaths.ReadSource(..., stripComments: true)`), so a failing test first would
  have to read what that design excludes, and a phrase blacklist is the kind of guard the simplicity
  criterion rejects. The RED and GREEN here are a scratch phrase check, not a guard: with whitespace and
  `///` wrapping normalised it found 11 stale phrases in 8 files before the edits (exit 1) and none after
  (exit 0). Its first version missed two phrases that wrap across a `///` marker; that was corrected before
  the RED run was counted.
- Build, `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: `Build succeeded.`, 2
  warnings (BHA0001 in `Patch88_InitializeLordPartyPropertiesScope.cs`, BHA0006 in `TeamTacticProbe.cs`;
  neither file touched), 0 errors.
- Feature filter `FullyQualifiedName~AnimMemory`: `Passed!  - Failed:     0, Passed:    86, Skipped:     0,
  Total:    86`.
- Final full suite: `Failed!  - Failed:     1, Passed: 12431, Skipped:     2, Total: 12434`; the failure is the
  known `EveryLanguage_DeclaresARowForEveryEnglishKey`. These are the totals the Codex pass recorded at
  `97b97c50`; no test was added.
- `python -B tools/lint_docs.py --fail-on-drift`: exit 0. Added lines holding an em or en dash: 0.
- Not run: the reference-assembly unit step (no production code changed and one test file changed in a
  summary comment) and the Python suite (no file under `tools/` changed). Gate sweep: none; no hook, validator
  or CI step changed.
- Not touched, the orchestrator's: the REVIEW-LOG entry's heading and **Claude** line, the RCA's "Convergence
  rounds" section and the lessons do not mention this round yet (a fix pass records a round only here);
  `plans/_audit/2026-10-02-perf/issue-drafts.md:110-111` (the #718 draft, and the public body the finding
  reports) still says "the decision on resident clips or a larger budget rests on a measurement"; and the
  body of `97b97c50` says the feature doc "names the A/B that measures it", which needs a reword at squash
  or merge, as for the other bodies in NEEDS MIKE 4 (added there afterwards, as its last bullet).
