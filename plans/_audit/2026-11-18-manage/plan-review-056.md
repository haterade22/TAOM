# Cold review: plan 056, round 1

Plan: `plans/056-agent-removed-off-thread-guards.md`. Code read from the run worktree at `3164713da`
(`git rev-parse --short HEAD` printed `3164713da`). No earlier `plan-review-056*.md` existed in this
folder, so there are no prior blocking items to re-check. dotnet was not run (read-only role): the
baseline totals in Status (lines 23 to 27) are UNVERIFIED by this review.

## Verdict

Not yet executable by a weak executor: two verification gates cannot pass as written. Both fixes are
one-line edits. Everything else checked out: every Current state excerpt but one citation matched the
code, the census is exact, the engine facts hold, and the Step 2 to Step 4 RED/GREEN sequence is sound
when traced by hand.

## Blocking

1. **The `_pending.Remove(` grep can never be empty** (plan line 786, Step 5 Verify; line 861, Done
   criteria). `git grep -n "_pending.Remove(" -- Main` at `3164713da` also matches three lines this
   plan does not touch: `Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs:113` and
   `:170` (`_pending.Remove(index);`) and `Main/Features/Realms/Domain/Ledger/TitleLedger.cs:110`
   (`_pending.Remove(titleId)`). After a correct Step 5 the command still prints those three lines and
   exits 0, so the executor fails the gate and STOPs. Fix: scope it to
   `-- Main/Features/MissionDiagnostic` (at base that prints exactly the two lines this plan removes,
   `MissionDiagnosticBehavior.cs:74` and `:76`).

2. **Wrong RED oracle for `RemoveOrDefer_OffTheMainThread_WaitsForTheNextDrain_WhichAppliesItBeforeReading`**
   (plan lines 637 to 638). The test adds `gone` and `kept`, and under the Step 2 stub the worker
   removes `gone` at once, so `_sut.Count` is 1, not 0: MSTest prints `Expected:<2>. Actual:<1>`. The
   plan says "(the count is 0, expected 2)". The executor is told to confirm exact expected results
   and the RED set is a STOP condition (line 879); a literal executor seeing 1 where the plan says 0
   may stop. Fix: "the count is 1, expected 2".

## Non-blocking

1. Line 61 (census row 9): `FordsCampAdapter.TryRemove` takes its lock at
   `Main/Adapters/FordsCampAdapter.cs:142`, not `:141` (`TryRemove` is declared at `:135`; line 141
   is blank). The verdict "guarded" stands.
2. Lines 338, 817, 855: the full-suite expectations are absolute (`Passed: 42918 ... Total: 42930`),
   while Step 1 (line 410) allows a base that differs "with the difference explained". If the base
   differs, the later gates cannot match. Phrase them as "the Step 1 totals plus 7 passed tests, the
   same failing names".
3. Lines 825, 832, 865: "`git status --porcelain` lists only the six in-scope paths" / "prints
   nothing" assumes a tree with no untracked files before Step 1. The run worktree at review time
   holds untracked `plans/055-*.md`, `plans/056-*.md` and `plans/_audit/2026-11-18-manage/`. If the
   executor's worktree has any, these gates fail through no fault of its own. Add "apart from
   untracked paths that `git status --porcelain` listed in Step 1".
4. Step 7.2 (lines 819 to 824) builds `TAOM.Tests` with `-p:TaomGameRefs=RefAsm` into the same
   `bin`/`obj` as the normal build. If the executor re-runs the Done criteria test command afterwards,
   it is UNVERIFIED here whether the incremental build recompiles against the real game references.
   Safer: run the RefAsm step before Step 7.1, or rebuild with the normal command before checking
   the Done criteria.
5. Step 1 (lines 404 to 407) checks for an `Equals` override in `TaleWorlds.MountAndBlade.Agent.cs`
   only. `Agent` derives from `DotNetObject`. This review checked
   `TaleWorlds.DotNet.DotNetObject.cs`: it has no `Equals` or `GetHashCode` override (only a
   finalizer at `:77`), so the claim holds at v1.5.7. Add the base-class check so the STOP condition
   (line 877) also covers it.
6. The class comment the plan supplies (line 659: "Add, Drain and Clear run on the main mission
   thread") asserts that `OnAgentCreated` is main-thread. The plan quotes no evidence for that, and
   the engine thread table row at
   `docs/reference/engine/mission-frame-threads-and-native-costs.md:141` does not list it. Either cite
   the source or soften the comment to what is verified ("the tick drains and clears").
7. Step 5 items 5 and 6 (lines 761 to 774): the code blocks sit inside a numbered list, so each line
   carries 3 extra spaces of markdown indent. Say explicitly that the replacement lines take the
   file's 4-space member indent.
8. Step 3 (line 648) replaces the whole file; the file is CRLF (`file` confirms) and a Write-tool
   rewrite may produce LF. No gate checks this (`.gitattributes` pins only `*.sh`, `*.bash`,
   `.githooks/*`), but say "keep CRLF" to avoid a whole-file line-ending diff.
9. Line 340, line 785: "`EntryPointLineCountGateTests`: all pass" gives no count. The template asks
   for an exact count on a filtered run, so the executor can tell a match from an empty filter.

## Checks that passed

- Census: `git grep -n "override void OnAgentRemoved" -- Main` lists exactly the 16 file:line pairs
  in the table (lines 53 to 68). The 9 `OnAgentDeleted` overrides match "8 other" (line 358).
  `git grep -n "MarkMainThread()" -- Main` prints 15 lines in 14 files, consistent with "14 call
  sites in 13 files ... also prints the declaration" (line 920).
- Excerpts: `PendingAgentReads.cs` (whole file, lines 94 to 133), `MissionDiagnosticBehavior.cs`
  lines 1 to 8, 21 to 44, 46 to 49, 59 to 76 and the cited ranges `:78-92`, `:124-146`, `:148-160`
  match byte for byte apart from CRLF. Class line 13, closing brace 161, file ends `}\r\n`; the
  plan's `awk` prints 149 at base. The Step 5 edit arithmetic (149 + 1 + 1 - 4 = 147) is right.
- `DeferredCallbackQueue.cs` `:22`, `:25`, `:39-49`, `:56-66`, `:69-72` and `MissionThreadGuard.cs`
  `:25`, `:34-41`, `:48-60`, `:62-67` match; the report text contains
  `"{site} ran off the main mission thread"`, so the test oracles at lines 507, 598 and 613 hold.
- Exemplar: `MountDespawnMissionBehavior.cs:50`, `:80`, `:120-121` and the test pattern in
  `MountDespawnOffThreadTests.cs` match.
- Other citations: `Main/SubModule.cs:2380`, `Main/IoC.cs:276`, `FileLogger.cs:129`,
  `harmony-patches.md:113`, `mission-frame-threads-and-native-costs.md:141`,
  `csharp-architecture.md:125`, `traps/mission.md:32`, `harmony-il.md:619` and `:633`,
  `rca-offthread-agent-removed-2026-09-22.md:100`, and the census row citations spot-checked
  (`HeroKillTally.cs:15-18`, `CareerPerkMissionBehavior.cs:163`, `CreatureScoreboardBridge.cs:26`,
  `FieldCommissionMeritService.cs:84-91`, `HeroAuraLedger.cs:16,46`, `CreatureBanditDiag.cs:36,39`,
  `CreatureDiagLedger.cs:95,125`, `EnlistmentMeritMissionBehavior.cs:41`) all match.
- Engine facts via `pwsh tools/taom-src.ps1 path` (v1.5.7 cache): `Agent` sealed at `:14`,
  `GetHashCode` returns `_creationIndex` at `:5026-5029`, `_creationIndex` at `:606`, 0 `Equals`
  overrides, `ActionSet` at `:722`; `MissionBehavior` `:11`, `:57`, `:85`, `:89`, `:125`, `:150`;
  `KillingBlow` struct at `:9`; `AgentState` in `TaleWorlds.Core`; both `Mission` callbacks are
  `[MBCallback(null, false)] internal`.
- Hand trace of the tests: Step 2 RED set (2 of 4 queue tests, 3 of 3 behavior tests) is right; Step 3
  makes `Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt` fail as stated; Step 4 turns it green; the
  behavior tests pass after Step 5 (the tick marks, then `Drain` applies the parked removal before
  `_order` is read, so `TryMarkActionSetKey` is never called). Filters: `~PendingAgentReadsTests` does
  not match `PendingAgentReadsOffThreadTests`; `~PendingAgentReads` matches 9 + 4; the existing
  classes hold 9 and 3 `[TestMethod]`s as stated.
- Build settings: `Directory.Build.props` sets `Nullable` enable and LangVersion 10, so
  `Action<string>?` compiles; `WarningsAsErrors` adds only NU1903/NU1904, so the stub's unused
  parameters do not break the Step 2 build. No assembly-level test parallelism (only one
  `[DoNotParallelize]` class), so the static guard mark is safe across test classes with the planned
  `[TestCleanup]`s.
- Template items: issue #607 named; ADR-002, 003, 004, 005, 007, 008 named; no protected file; no
  single-owner edit (`SubModule.cs` constructor call unchanged); STOP conditions are specific;
  planned-at `3164713da` and drift-check paths cover Scope; build and test commands carry
  `-p:DisableModuleCopy=true -p:ModuleId=`; no worktree path or branch name; CHANGELOG only listed as
  out of scope; subject is 58 characters with `v2.0.40` matching `SubModule.xml:6`.
- Prose: no U+2014 or U+2013 in the plan; no absolute local path; no secret values.
