# Merge map for the perf branches

Written for: Mike, when the branches are merged on your word. No probe changed a branch, a ref or a
worktree: every probe result is a tree object or a dangling commit that no ref points to.

## Full probe, 2026-10-03 (all 15 branches onto 7f0c8446)

Probed on the afternoon of 2026-10-03, against trunk `bannerlord-1.5.x` at `7f0c8446`, the program branch at `bb376789`,
and these branch tips: 028 `765d3759`, 029 `76878ffd`, 030 `37dab128`, 031 `6d17e69d`, 032 `e6343e53`,
033 `06307997`, 034 `04894078`, 035 `e403dc7c`, 036 `50df8cbf`, 037 `c8c96c09`, 038 `072d46dc`, 039
`db777487`, 040 `4e4e65e5`, 041 `d7208235`, 042 `af50ab17`. If a tip moves, re-run the probe.

**How it was folded.** Each step runs `git merge-tree --write-tree` of the next tip onto the previous
step's probe commit. A fold that skips each conflicted branch stops being useful at step 3, because every
branch appends to `docs/reviews/REVIEW-LOG.md`: run that way, only the program branch, 028 and 039 fold
in. So each conflicted step was resolved inside the probe (both sides kept, the earlier one first), its
tree was written as objects only and committed as a dangling probe commit, and the next branch was probed
against that. Then every hunk was read, and the resolutions below were written from those reads. Where
keeping both sides is wrong for a hunk, the step says so. Four orders were compared, from 87 to 92 conflict
hunks; the order below has the fewest.

**Totals:** 3 clean steps and 13 conflicted ones, with 76 conflicted files and 87 hunks. 41 of the hunks are in
`REVIEW-LOG.md` or `docs/reviews/lessons/*.md`, every one at the end of the file, where keeping both is
mechanical. No conflict touches `Main/SubModule.cs` or `Main/IoC.cs` (see "Single-owner files").

### Order

Each step's conflicts are marked like this. **Append:** both sides appended at the end of the file, so keep the
entry merged earlier, then the incoming one. **Keep both:** each side added different entries,
properties or sections at the same spot. **Hand:** both sides edited the same line, and the text to write is
given.

1. **Program branch** (`bb376789`): clean. It goes first because every branch forks from one of its
   commits (see "Ancestry"), so each later step brings only that branch's own commits. Its own changes are
   the run folder `plans/_audit/`, the 15 plan files, `plans/README.md` and nine docs files. Since the
   last branch forked (`ba3f2a57`, 04:45) it has changed only `plans/_audit/` and `plans/README.md`, so
   if it moves again before the merge (this file's commit moves it), merge its new tip once more at the end.
2. **028** (`765d3759`): clean.
3. **039** (`db777487`): clean. 039 is built on 028's tip, so after step 2 it adds only its own five
   commits.
4. **041** (`d7208235`): 16 files, 19 hunks. 041 carries 028's first four commits, not `765d3759`, and
   meets 039 in every file that both of them extend.
   - Keep both: `Dependencies/Foundation/PatchShieldPolicy.cs` (041's three Patch98 targets next to the
     Mission entries, then 039's five map targets), `BattleLoadDiagnosticsSettings.cs` (041's
     `EnableHitchProbe` in the Mission Performance group, then 039's `EnableMapProfiler`),
     `BattleLoadDiagnosticsSettingsProvider.cs`, `CoopSettingsRelevance.cs`,
     `BattleLoadDiagnosticsSettingsProviderTests.cs`, the allowlist row in
     `SettingRequireRestartPostureTests.cs`, and `harmony-patch-registry.md` (two new sections, in patch
     number order: Patch98 before Patch101).
   - Hand: in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` and
     `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, keep one `Mission.WaitTickCompletion`
     row, citing `MissionTickProfilerInstaller.cs:31,54` (041's; the merged installer makes its two
     `AccessTools.Method` calls at lines 31 and 54). Drop 039's `29,52` row. Then add 041's
     `ScriptComponentBehavior.OnTick` row and 039's four `MbEvent` listener rows. Keeping both sides would
     list `WaitTickCompletion` twice.
   - Hand: the class comment in `SettingRequireRestartPostureTests.cs` becomes "every TAOM setting but three",
     naming `EnableTickProfiler` (Patch97), `EnableHitchProbe` (Patch98) and `EnableMapProfiler`
     (Patch101). In `docs/features/mcm.md`, "Four are allowlisted" (`EnableNativeSkinFixes` and the three
     toggles), with one paragraph that covers all three restart toggles: the two mission toggles are read
     again at each mission start, the map toggle at each campaign session start.
   - Hand: in `docs/reference/feature-map.md`, use 041's MissionPerf row (it replaces 028's) and add 039's new
     MapPerf row. Keeping both sides would list MissionPerf twice.
   - Hand: the excluded count in `coop-interop.md`. Both sides wrote 124; see "Settings counts".
   - Hand: `REVIEW-LOG.md`. 041 carries the older copy of 028's "Final suite" line, which `765d3759`
     updated (12,453 passed at `7ca09cc2`). Keep 028's updated line and 039's entry, then 041's entry
     without that stale first line. A plain keep-both leaves the stale line at the end of 039's entry.
   - Append: `lessons/harmony-il.md`, `lessons/misc.md`, `lessons/testing-qa.md`.
5. **036** (`50df8cbf`): 12 files, 17 hunks.
   - Same file, added by both: `Main/Adapters/IAnimationLoadingAdapter.cs` and `AnimationLoadingAdapter.cs`.
     Both copies declare the same single member, `IsAnyAnimationLoadingFromDisk()`; 036's copy adds XML doc
     comments. Take 036's copy. Keeping 041's, which merged first, also works, because the code is the same.
   - Keep both: `BattleLoadDiagnosticsSettings.cs` (`EnableAnimMemoryProbe` after the other Mission
     Performance settings) and `CoopSettingsRelevance.cs`.
   - Hand: `docs/features/mission-perf-heartbeat.md`, four hunks. In Configuration, keep the merged wording
     ("Read from the MCM instance at most once a second (`ToggleRefreshSeconds`)...") and add 036's
     sentence about `EnableAnimMemoryProbe`. Keep the tick profiler and hitch probe sections, then 036's
     clip memory section after them. The file table takes both sides' rows, with one settings row (the
     heartbeat toggle, the three tick profiler settings, the hitch probe toggle and `EnableAnimMemoryProbe`)
     and the adapter pair listed once. Both tests paragraphs stay.
   - Hand: `feature-map.md`, one MissionPerf row naming the instruments of 028, 041 and 036.
   - Hand: the settings counts in `SettingsFingerprintTests.cs`, `coop-interop.md` (three lines) and
     `bannerlord-together-compat.md`; see "Settings counts".
   - Append: `REVIEW-LOG.md`, `lessons/misc.md`, `lessons/testing-qa.md`.
6. **040** (`4e4e65e5`): 12 files, 14 hunks.
   - Keep both: `PatchShieldPolicy.cs` (040's seven load-time targets, `MBObjectManager.CreateMergedXmlFile`
     among them, after the per-frame entries), `Main/Composition/FeatureModules.cs`
     (`AnimMemoryProbeModule`, then `LoadTimeStampsModule`), `BattleLoadDiagnosticsSettings.cs`
     (`EnableLoadTimeStamps`, in its own group), `IBattleLoadDiagnosticsSettingsProvider.cs`, and
     `harmony-patch-registry.md` (Patch100's two sections, in number order).
   - Hand: `CoopSettingsRelevance.cs`. 040 rewrites the existing `"CultureDoctrineDebug",
     "EnableMissionPerfHeartbeat",` line and its comment to add `"EnableLoadTimeStamps"`. Use 040's
     comment and line in place of the old pair, and keep the tick profiler, map profiler, hitch probe and
     clip memory entries. Keeping both sides would list the doctrine pair twice.
   - Hand: the settings counts again (same files as step 5).
   - Append: `REVIEW-LOG.md`, `lessons/build-tooling-workflow.md` (against trunk's `d9a8f46f` entry),
     `lessons/misc.md`.
7. **042** (`af50ab17`): 7 files, 8 hunks.
   - Keep both: `FeatureModules.cs` (then `XmlMergeModule`), `ReflectionSiteBindingTests.cs` (040's ten
     `MbEvent` rows, then 042's `CreateDocumentFromXmlFile` row), `reflection-sites.md` (both sides' rows
     and both status paragraphs), and `harmony-patch-registry.md` (the Patch99 section, before Patch100).
   - Append: `REVIEW-LOG.md`, `lessons/harmony-il.md`, `lessons/testing-qa.md`.
   - Not a text conflict, but an overlap: 040 (Patch100, finalizer) and 042 (Patch99, prefix) both
     patch `CreateMergedXmlFile`, and 040 puts it on PatchShield's exclusion list. This is unchanged from the
     06:05 probe below and FOR-MIKE 16h.
8. **029** (`76878ffd`): append only, in `REVIEW-LOG.md`, `lessons/build-tooling-workflow.md` and
   `lessons/testing-qa.md`.
9. **034** (`04894078`): append only, in `REVIEW-LOG.md`, `lessons/harmony-il.md` and
   `lessons/testing-qa.md`.
10. **030** (`37dab128`): append only, in `REVIEW-LOG.md` and `lessons/misc.md`.
11. **031** (`6d17e69d`): append only, in `REVIEW-LOG.md`, `lessons/harmony-il.md` and
    `lessons/testing-qa.md`.
12. **032** (`e6343e53`): 5 files.
    - Hand: the Performance list in `docs/features/mixed-formations.md`. In order: 032's "Service state"
      bullet (the `ConcurrentDictionary` layouts), 031's "Per-frame work" bullet (it adds the
      `CachedEnumParse` clause; 032 left that bullet as it was), 031's "Cycle hotkey log line" bullet, and
      032's "Per-position-query work" bullet (the lock-free lookup).
    - Append: `REVIEW-LOG.md`, `lessons/adapters-taleworlds-api.md`, `lessons/harmony-il.md`,
      `lessons/testing-qa.md`.
13. **033** (`06307997`): append only, in `REVIEW-LOG.md`, `lessons/adapters-taleworlds-api.md`,
    `lessons/state-lifecycle-save.md` and `lessons/testing-qa.md`.
14. **035** (`e403dc7c`): append only, in `REVIEW-LOG.md`, `lessons/build-tooling-workflow.md` and
    `lessons/testing-qa.md`.
15. **037** (`c8c96c09`): append only, in `REVIEW-LOG.md`, `lessons/build-tooling-workflow.md` and
    `lessons/gamemodels-services.md`.
16. **038** (`072d46dc`): append only, in `REVIEW-LOG.md`, `lessons/adapters-taleworlds-api.md` and
    `lessons/build-tooling-workflow.md`.

Why this order: 039 must follow 028, and the five branches that change the settings, PatchShield and
registry files (039, 041, 036, 040, 042) go together, so the real conflicts sit in steps 4 to 7. 040
goes between 039 and 042, which keeps `SubModule.cs` free of text conflicts. Steps 8 to 16 add only
append-only records, except the one bullet list in step 12. 030, 031 and 032 go next to each other because
they also share `companion-tactics.md` and `creature-bandits.md`, which merge cleanly.

### Settings counts: one recount for steps 4 to 6

Five branches add `BattleLoadDiagnosticsSettings` properties, and each edits the same count lines with
numbers that are right for that branch alone. The merged probe tree has 16 properties (trunk has 9): 028
adds `EnableTickProfiler`, `TickProfilerTopN` and `HitchThresholdMs`; 039 adds `EnableMapProfiler`; 041
adds `EnableHitchProbe`; 036 adds `EnableAnimMemoryProbe`; and 040 adds `EnableLoadTimeStamps`. All seven are on
the co-op exclusion list, so the covered count stays 217.

| Where | Trunk | Merged |
|---|---|---|
| `SettingsFingerprintTests.cs`, `AssertSplit(typeof(BattleLoadDiagnosticsSettings), ...)` | `reflected: 9` | one line, `reflected: 16, covered: 0` |
| `coop-interop.md`, "TAOM ships **N** settings" | 337 | 344 |
| `coop-interop.md`, "the split is 320 in `TaomSettings`, N in `BattleLoadDiagnosticsSettings`" | 9 | 16 |
| `coop-interop.md`, "The N excluded (...)" | 120 | 127; the parenthesis after "#704" names the three tick profiler settings and the hitch probe, clip memory probe, map profiler and Load-Time Stamps toggles, each with the date its branch gives |
| `bannerlord-together-compat.md`, "TAOM's N MCM settings" | 337 | 344 |

Then run `SettingsFingerprintTests` on the merged tree.

### Single-owner files (for the orchestrator)

- **`Main/IoC.cs`:** no branch changes it.
- **`Main/SubModule.cs`:** 028, 030, 039, 040, 041 and 042 change it. There is no text conflict in this order,
  nor in the three other orders tried, all of which merge 040 before 042. The 13:10 pairwise probe's
  039-against-042 conflict appears only without 040 between them. The merged file has one
  `MissionTickProfilerInstaller.InstallIfEnabled` call (041's comment) and one
  `MissionTickProfilerBehavior`. It also has 039's `MapSessionHooks.EndSession` in `OnGameEnd`, 040's
  stamps and `EndPhase` calls, and 030's removal of the agent colour store. 039's
  `MapFrameProfilerInstaller.OnGameInitialized` and 042's `LogWindowSummary` both sit before the
  once-per-process guard, as each plan requires. Both come after 040's `hookStamp?.End()`, so
  040's `OnGameInitializationFinished` stamp does not time them. If it should, move that `End()` below them.
  Read the merged method under the owner's eye even though git reports no conflict.

### Found by this probe, not yet in FOR-MIKE

- **Patch35 descriptions go stale when 030 merges.** 030 (`57c6876f`) deletes `Patch35_Mission_OnTick.cs`,
  the empty `Mission.OnTick` postfix. The merged tree still says `Mission.OnTick` carries Patch35's postfix in
  the PatchShield exclusion comment (`PatchShieldPolicy.cs`, from 028 and 041), in `Patch98_HitchProbe.cs`'s
  header comment and in two places in `mission-perf-heartbeat.md`; FOR-MIKE 16a also names Patch35
  among the patches a rescue would strip. Nothing in the code depends on it: the exclusion still holds
  for Patch98, which patches `Mission.OnTick` for every player. Reword those lines after the merge.
- **The three hand-merges that keep-both would break:** the `WaitTickCompletion` row (step 4), the stale
  "Final suite" line in `REVIEW-LOG.md` (step 4), and 040's in-place edit of `CoopSettingsRelevance.cs`
  (step 6). These are on top of the settings counts that FOR-MIKE 16g already lists.

### What blocks a clean replay

Nothing blocks it: every step resolves. Thirteen of the sixteen `git merge` steps will stop for
conflicts. Steps 8 to 11 and 13 to 16 hold only append hunks; steps 4, 5, 6 and 12 need the hand
resolutions above, and step 7 needs only keep-both. The history rewrite that FOR-MIKE 16g asks for (commit
message spec) is separate from this, and it changes every hash above, so re-run the probe after it.

### Ancestry

`git merge-base --is-ancestor` over every ordered pair of the 16 tips found one containment: 028's tip
`765d3759` is an ancestor of 039. Every tip's merge base with trunk is `dffdf879`, two commits behind
`7f0c8446` (`d9a8f46f`, `7f0c8446`). Every branch forks from a program branch commit, so each one carries
some of the run's docs commits:

| Branch | Forks from the program branch at | Own commits | Contains, or is contained in |
|---|---|---|---|
| 028 | `0d1e91f0` (21:42) | 5 | Contained in 039 (all 5); 041 has 4 of them, all but `765d3759` |
| 029 | `0d1e91f0` | 9 | |
| 030 | `0d1e91f0` | 9 | |
| 031 | `0d1e91f0` | 5 | |
| 032 | `80176e2a` (01:33) | 5 | |
| 033 | `d50bf962` (22:37) | 7 | |
| 034 | `64dbcac8` (02:19) | 4 | |
| 035 | `d50bf962` | 4 | |
| 036 | `141a20f1` (23:30) | 4 | |
| 037 | `ba3f2a57` (04:45) | 8 | |
| 038 | `ba3f2a57` | 5 | |
| 039 | `0d1e91f0` | 10 | Contains all of 028; its own five are `c4ee9373` to `db777487` |
| 040 | `67fa6c1a` (04:14) | 6 | |
| 041 | `0d1e91f0` | 9 | Contains 028's commits up to `9d52bb86`; its own five are `1639147d` to `d7208235` |
| 042 | `efb70763` (03:33) | 4 | |

"Own commits" means the commits not on the program branch. 041's nine and 039's ten include 028's four and five.

### Files touched by three or more branches

Counted over each branch's own commits (from its fork point to its tip). 039 and 041 count 028's files
because they carry 028's commits.

| File | Branches | Conflicts at steps |
|---|---|---|
| `docs/reviews/REVIEW-LOG.md` | all 15 | 4 to 16 (append; step 4 by hand) |
| `docs/reviews/lessons/testing-qa.md` | 028 029 031 032 033 034 035 036 039 041 042 | 4 5 7 8 9 11 12 13 14 (append) |
| `docs/reference/harmony-patch-registry.md` | 028 030 032 034 039 040 041 042, and trunk | 4 6 7 (keep both) |
| `docs/features/bannerlord-together-compat.md` | 028 033 034 036 039 040 041 | 5 6 (recount) |
| `docs/reviews/lessons/harmony-il.md` | 028 031 032 034 039 041 042 | 4 7 9 11 12 (append) |
| `docs/reference/feature-map.md` | 028 036 039 040 041 042, and trunk | 4 5 (hand) |
| `Main/SubModule.cs` | 028 030 039 040 041 042 | none |
| `docs/features/coop-interop.md` | 028 034 036 039 040 041 | 4 5 6 (recount) |
| `docs/reviews/lessons/misc.md` | 028 030 036 039 040 041 | 4 5 6 10 (append) |
| `docs/reviews/lessons/build-tooling-workflow.md` | 029 035 037 038 040, and trunk | 6 8 14 15 16 (append) |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | 028 034 039 040 041 | 4 6 (keep both) |
| `BattleLoadDiagnosticsSettings.cs` | 028 036 039 040 041 | 4 5 6 (keep both) |
| `CoopSettingsRelevance.cs` | 028 036 039 040 041 | 4 5 (keep both), 6 (hand) |
| `SettingsFingerprintTests.cs` | 028 036 039 040 041 | 5 6 (recount) |
| `ReflectionSiteBindingTests.cs` | 028 039 040 041 042 | 4 (hand), 7 (keep both) |
| `reflection-sites.md` | 028 039 040 041 042 | 4 (hand), 7 (keep both) |
| `docs/features/mission-perf-heartbeat.md` | 028 029 036 039 041 | 5 (hand) |
| `Dependencies/Foundation/PatchShield.cs` | 028 034 039 041 | none |
| `BattleLoadDiagnosticsSettingsProvider.cs` | 028 039 040 041 | 4 (keep both) |
| `IBattleLoadDiagnosticsSettingsProvider.cs` | 028 039 040 041 | 6 (keep both) |
| `BattleLoadDiagnosticsSettingsProviderTests.cs` | 028 039 040 041 | 4 (keep both) |
| `PatchShieldPolicyTests.cs` | 028 034 039 041 | none |
| `docs/features/mcm.md` | 028 037 039 041 | 4 (hand) |
| `docs/reviews/lessons/adapters-taleworlds-api.md` | 032 033 038 042 | 12 13 16 (append) |
| `Main/Composition/FeatureModules.cs` | 036 040 042 | 6 7 (keep both) |
| `SettingRequireRestartPostureTests.cs` | 028 039 041 | 4 (keep both, comment by hand) |
| 028's MissionPerf code and tests (20 files under `Main/Features/MissionPerf/` and `TAOM.Tests/Features/MissionPerf/`), `Patch91_MissionTickStallProbes.cs`, `IGraphicsOptionsAdapter.cs`, `GraphicsOptionsAdapter.cs`, 028's review report and RCA | 028 039 041 | none |

Files shared by exactly two branches: the animation adapter pair (036, 041; step 5),
`mixed-formations.md` (031, 032; step 12), two append-only lessons files (`lessons/state-lifecycle-save.md`,
033 against 036 at step 13; `lessons/gamemodels-services.md`, 037 against 030 at step 15), and three that
merge cleanly: `companion-tactics.md` (030, 031), `creature-bandits.md` (030, 032) and
`troll-brute-force.md` (030, 034).

### After merging

Run the full suite, the reference-assembly step and the binding gate on the merged tree, then one
`/verify-bindings` (the reviews of 028, 030, 039, 040, 041 and 042 each ask for it). Then run
`tools/perf_runs.py` over one profiled battle log to check that plan 029 reads the lines of plans 028,
036 and 041 as extra tags. Reword the Patch35 lines listed above.

## Earlier probe (06:05, ten branches)

Kept as written at the time. Its tips are older than the full probe's, and it did not cover 035, 037,
038 or 039.

### First probe, about 04:15

Probed with `git merge-tree --write-tree` (tree objects only; no branch or ref was changed) by
`merge_probe.py` beside this file, at the branch tips of about 04:15 (041 `56b2b327`, which contains 028;
029 `f92a0bb4`; 036 `2f560c6c`; 030 `37dab128`; 031 `6d17e69d`; 032 `a00363cc`; 033 `d45774a5`; 034
`31f59582`; 042 `19be93be`). Branches still under review will move; re-run the probe before merging.

#### Order

041 (it carries 028), then 029, 036, 030, 031, 032, 033, 034, 042. 032 and 033 fold in cleanly; every
conflict below is between two branches that each add to the same file.

#### Conflicts by kind

| Kind | Where | Resolution |
|---|---|---|
| Append-only records | `docs/reviews/REVIEW-LOG.md` (029, 030, 031 against 041 and each other); `docs/reviews/lessons/testing-qa.md`, `misc.md`, `harmony-il.md` (034 too) | Keep both sides: each branch appended its own entry at the end |
| Appended registry entries | `docs/reference/harmony-patch-registry.md` (042 against 041); `Main/Composition/FeatureModules.cs`'s list (042 against 036) | Keep both entries |
| Same files, both new | 036 and 041 both add `Main/Adapters/IAnimationLoadingAdapter.cs` and `AnimationLoadingAdapter.cs` | Keep the first merged (the same members, one specification); confirm the build |
| Settings counts | 036 and 041 (with 028's three) each add a `BattleLoadDiagnosticsSettings` property, a `CoopSettingsRelevance` entry, and move `SettingsFingerprintTests`' reflected count and the co-op docs' totals | Keep every property; recount the reflected settings and the totals in `coop-interop.md` and `bannerlord-together-compat.md` from the merged code; run `SettingsFingerprintTests` |
| Feature docs | `mission-perf-heartbeat.md` and `feature-map.md` (036 against 041); `mixed-formations.md` (031 against 032) | Keep both sections; one MissionPerf feature-map row naming every instrument |
| A plan file | `plans/041-profiler-extensions-and-hitch-probe.md`: 041's branch carries the re-anchored copy, 032's base an earlier one | Take the program branch's copy (`1a702d25` and later) |

### Plan 040, re-probed at 06:05

Probed with 040 (`a07546a7`) folded in last, after 042, and against every branch's tip at that time (041
now `0d030997`, its review's lead commit; the probe output is in the run's scratch as
`merge-probe-040.txt`). The rest of the order folds as before.

| Pair | Paths | Resolution |
|---|---|---|
| 040 against 041 (028 inside) | `Dependencies/Foundation/PatchShieldPolicy.cs` (each appends to `ExcludedTargetMethods`), `BattleLoadDiagnosticsSettings.cs` and `IBattleLoadDiagnosticsSettingsProvider.cs` (each adds a setting), `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md`, `harmony-patch-registry.md` | Keep every entry and every setting; recount the settings totals from the merged code |
| 040 against 036 | `Main/Composition/FeatureModules.cs`, the settings files, `coop-interop.md` | Keep both module entries and both settings |
| 040 against 042 | `Main/Composition/FeatureModules.cs`, `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, `harmony-patch-registry.md` | Keep both sides' rows |

No textual conflict in `Main/SubModule.cs`, and none between 040 and 042 in `PatchShieldPolicy.cs`. The
overlap no text conflict shows: both patch `MBObjectManager.CreateMergedXmlFile`, 042 with a prefix
that replaces the merge (Patch99) and 040 with an observe-only finalizer (Patch100). The merged build
must show the `[LoadXml]` lines still correct with 042's fast path on; 040's review is asked to check it.

### After merging (06:05)

The full suite, the reference-assembly step and the binding gate on the merged tree; then
`tools/perf_runs.py` over one profiled battle log to check that plan 029 reads plans 028, 036 and 041's lines
as extra tags (029's own tests pin 028's shapes; 036's `[AnimMem]` and 041's `[HitchDetail]`,
`[SpawnProfile]`, `[ScriptProfile]`, `[AnimLoad]` and `[TickSummaryExtra]` are covered by its generic rule
only).
