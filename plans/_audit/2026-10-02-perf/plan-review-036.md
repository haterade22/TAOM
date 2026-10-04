# Plan review 036, round 1 (cold)

Plan: `plans/036-anim-memory-probe.md` (903 lines). Code read from the worktree, whose HEAD is
`9efd3230`; every Main, TAOM.Tests and docs path the plan cites is identical at `0d1e91f0` except one
docs file (see N1). No earlier `plan-review-036*.md` exists, so nothing carried over.

## What I verified (all match the plan)

- **Binary and pin**: `.claude/pinned-game-version.txt` is `v1.5.3`; the installed
  `TaleWorlds.Native.dll` is 14,209,376 bytes.
- **Step 2 commands, run for real**: `native_sig_author.py disasm 0x21DFFF --n 30` prints exactly the
  plan's lines 60-71 (with `8B 05 2B DE B8 00` at `0x0021E00F` and `F3 0F 5C 05 A0 02 91 00` at
  `0x0021E034`). `scan "<pattern>"` prints `matches: 1` and `  RVA=0x21E00F`.
- **Counter accesses**: `lock xadd dword` at `0x591319` and `0x21E0EF`, and `mov eax, dword` at `0x830A3`,
  all resolve to `0xDABE40`. The counter is 32 bits wide, so `Marshal.ReadInt32` is the right width.
  `cmp ecx, 0xf00000` is at `0x591327`.
- **PE layout** (my scratch parser): e_lfanew `0x198`, Machine `0x8664`, 8 sections, SizeOfOptionalHeader
  `0xF0`, magic `0x20B`, SizeOfHeaders `0x400`, ImageBase `0x180000000`, table at `0x2A0`. The `.text`,
  `.rdata` and `.data` rows match the table at lines 105-109 exactly. The budget float at raw offset
  `0xB2C0DC` is `00 00 40 4B` = 12582912.0. The counter lies inside `.data`'s VirtualSize but past its
  raw data. The 50-byte real site occurs once in raw `.text`, at RVA `0x21E00F`.
- **Synthetic oracles**: disp `0x1FCA` and `0xFB3`; loadSite `0x1040` and budgetSite `0x1065`; the
  `Contains` bounds; `0x4B400000` = 12582912f and `0x4B000000` = 8388608f; the 50-byte pattern has 16
  wildcards; every AnimMemLine string in Step 5 (KB, pct, `{0:0.0}` of 23.44); the Step 8 session
  sequence (drops=2, min 9216, max 12288, samplesAtOrAbove90Pct=4, the 90% boundary pair); the commit
  subject is 71 characters.
- **Engine decompile (taom-src v1.5.3)**: `MBAnimation` is a public struct and
  `IsAnyAnimationLoadingFromDisk` is at lines 143-146. `FinishMissionLoading` calls `AfterStart` between
  0.48f and 0.56f. `MissionBehavior.AfterStart` is at :33, `OnEndMission` (protected) at :130 and
  `OnMissionTick` at :150.
- **TAOM excerpts**: MissionPerfHeartbeatBehavior (119 lines; line 57; catch at 83-88).
  SubModule.cs:2112 and :2121. FeatureModules.cs list. BattleCorpsesModule shape.
  BattleLoadDiagnosticsSettings (62 lines; group at 58-61). CoopSettingsRelevance 70-72 and :142.
  SettingsFingerprintTests:211 and the doc-count test. coop-interop.md:310/312/316.
  bannerlord-together-compat.md:291. feature-map.md:72. Engine doc section 6 at 130-165.
  FileLogger 85-88. IModLogger is public. TAOM.csproj IVT 112-120. GameAssemblies :123/:133.
  `RepoPaths.ReadSource(string, bool stripComments)`. csharp.yml:67. MissionAdapterFactoryTests:67.
  Version `v2.0.32`.
- `python -B tools/lint_docs.py --fail-on-drift` exits 0 at the worktree tip. The plan itself has no em
  or en dash and no secret.
- **Not run (read-only role)**: dotnet. The baseline totals at lines 29-33 are UNVERIFIED by me.

## Blocking

**B1. RED gates that will see CS0234 (lines 437-439, and 698-699).** Step 3's Verify requires every
error to be a CS0103 or CS0246 naming `ClipBudgetSignature`, `PeSectionTable` or `PeSection`, "and
nothing else". The tests live in `TAOM.Tests.Features.MissionPerf.AnimMemory`, which does not enclose
`TAOM.Features.MissionPerf.AnimMemory`. So each test file needs
`using TAOM.Features.MissionPerf.AnimMemory;`. Before Step 4 that namespace does not exist, while
`TAOM.Features.MissionPerf` does, so the compiler reports CS0234 ("'AnimMemory' does not exist in the
namespace 'TAOM.Features.MissionPerf'") alongside the CS0246s. A literal executor fails the gate and,
after two tries, STOPs. Step 9's RED hits the same diagnostic for
`using TAOM.Features.MissionPerf.AnimMemory.Hooks;` (`Hooks` does not exist yet).
**Fix:** in both gates, also accept CS0234 naming the namespace `AnimMemory` (Step 3) or `Hooks`
(Step 9).

**B2. Contradictory session oracles (lines 659-663).** `Tick_ReaderThrows_LogsOneErrorAndStops` says
"`ReadLoadedBytes` throws" and that `End` "still logs the summary with `stopped=1`".
`End_WithNoSamples_LogsNothing` says a session whose `Start` threw logs nothing at `End`. Read
literally (the substitute throws on every call), `Start`'s own read throws first. The session then
has zero samples, and the two tests demand opposite `End` behaviour for the same state. No
implementation satisfies both, and a weak executor loops on it.
**Fix:** say that in `Tick_ReaderThrows` the first read (in `Start`) returns `10485760` and the next
one throws, e.g. `probe.ReadLoadedBytes().Returns(x => 10485760, x => throw new
InvalidOperationException("boom"))`. Then the single `LogError` comes from `Tick`, and `End` logs a
summary with `samples=1`. Also say whether `End_WithNoSamples` asserts no `LogInfo` call at all
(`Start`'s catch has already made one `LogError`).

## Non-blocking

- **N1 (drift, lines 9-14):** commit `4a909f20`, after `0d1e91f0`, edited the in-scope
  `docs/reference/engine/mission-frame-threads-and-native-costs.md`, in section 7 only. Section 6 is
  still lines 130-165, ending "...(a guarded four-byte native patch).", so the excerpt comparison
  passes. The drift check will nevertheless print that file. Re-anchor "Planned at" to the commit that
  lands the plan, or note this expected hit.
- **N2 (lines 24, 756-757): the "two reads per second" claim is wrong.** Both lines say two aligned
  32-bit reads per second. Under the plan's own design the budget is read once at arming (Step 7.5)
  and the counter once per sample. So it is one `ReadInt32` plus one `IsAnyAnimationLoadingFromDisk`
  per second. Step 10 prescribes the wrong sentence for the feature doc; correct it.
- **N3 (line 778): the dash check skips new files.** `git diff 0d1e91f0 -- docs Main TAOM.Tests` does
  not include untracked files, which here means every new `.cs` file. Run it after staging
  (`git diff --cached 0d1e91f0 -- ...`), or grep the new paths directly.
- **N4 (lines 466-468 vs 838): RVAs in comments.** Step 4 tells the executor to "copy the facts from
  Current state" into `ClipBudgetSignature`'s summary, and those facts are full of RVAs. The Done grep
  for `0x21E00F|0xDABE40|...` over `Main` matches comments too. Say "no RVA in comments either".
- **N5 (line 16): local path.** It is a local absolute path (`E:/Steam/...`), against the quality
  bar's "No local absolute paths". Point to the install via `BANNERLORD_GAME_DIR` or the tool's default
  instead.
- **N6: RefAsm step missing.** The new tests touch engine types (`MissionBehaviorDecl`,
  `AnimationLoadingAdapter` through the singleton test, `MissionLogic`), but the RefAsm unit step from
  `.ai/verification.md` (dispatch rule 7) is in neither Commands nor Done criteria.
- **N7 (line 27): category.** `diagnostics` is not one of the template's Category values.
- **N8: settings table not updated.** `docs/features/battle-load-diagnostics.md` (Configuration table,
  about lines 585-595) lists the page's settings and is untouched by the plan. It already omits
  `EnableMissionPerfHeartbeat`. Either add both rows or mark the table out of scope.
- **N9: oracles left implicit.** Some tests have no values:
  - `Tick_DropAcrossWindowBoundary_CountsInTheNextWindow` has no sequence or expected line.
  - `EnsureArmed_CounterTargetMisaligned` (0x3011) and the missing `.text` and `.rdata` cases have no
    reason strings, only an implied format.

  Spell them out.
- **N10: `Start` cadence unstated (lines 666-673).** That `Start` takes the first sample, sets
  `nextSample = 1.0`, and applies the negative-read rule is implied (via `Tick_BeforeOneSecond` and the
  start line), not stated.
- **N11 (lines 192, 693): missing usings.** The module exemplar's using list omits
  `System.Collections.Generic` and `TAOM.Adapters` (`BattleCorpsesModule.cs:1-6`).
  `RepoPaths.ReadSource` needs `using TAOM.Tests.Infrastructure;` (`BattleCorpsesWiringTests.cs:10`).
  Name them.
- **N12 (lines 455, 461-464): Parse checks.** `PeSectionTable.Parse`'s summary says "PE32+", but the
  check list never tests Machine `0x8664` or magic `0x20B`. Decide whether Parse checks them, and pin it
  with a test if it does.
- **N13 (line 844): the plan file in `git status`.** "`git status --porcelain` lists only in-scope
  files" will also list the plan file if it is untracked in the executor's worktree.

## Checklist summary

- **TDD**: RED precedes GREEN in Steps 3-9. The C# order is right apart from B1 and B2.
- **Issue**: the line is present ("filed by the orchestrator").
- **Conventions**: ADR-002, 003, 005, 007 and 008 and the rules are named, one line each.
- **Protected and single-owner files**: none are touched; Step 0 says so, and a Done grep checks
  `SubModule.cs`, `IoC.cs` and the csproj.
- **STOP conditions**: specific to this plan (binary size, signature count, targets, net472 surface,
  native writes).
- **Done criteria**: machine-checkable apart from the standard re-check line.
- **Drift paths**: consistent with Scope.
- **Commands**: build and test carry `-p:DisableModuleCopy=true -p:ModuleId=`.
- **Hygiene**: no worktree path, no branch name, no CHANGELOG step, no dash, no secret.
