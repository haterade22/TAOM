# Cold review: plan 015 (warg tick costs)

Reviewer: cold read against `.claude/skills/improve/references/plan-template.md` ("Quality bar").
Every excerpt was compared with `git show b2e387db:<path>`, and the engine facts with the
`C:\Users\mikew\.taom-src\v1.5.3\` cache. Plan line numbers are cited as `L<n>`.

## Verdict

The plan is careful and nearly executable as written. Every C# excerpt matches `b2e387db` exactly.
The Step 0 excerpt block, simulated against `b2e387db`, prints `2 1 2 1 1 1 1 1 1 1`. The drift
check comes back empty against the current `bannerlord-1.5.x` (`4b5662b2`). The RED counts I derived
by hand match the plan: Step 1 gives 8 failures and 1 pass, Step 5 gives 343 and 8, and Step 8 gives
2 failures and 3 passes. One blocking defect remains: every test expectation in the plan is written
against a console format that `dotnet test` does not print.

## Blocking

**B1. The expected test output does not match what `dotnet test` prints**
(L651, L760-762, L870, L1010-1013, L1119-1121, L1239, L1269, L1293, L1466, L1585, L1642, L1726, L1734-1737).

- The Commands table and Step 0 expect "a `Total tests:` line". With default verbosity, the
  plan's command prints only the one-line banner, for example
  `Passed!  - Failed:     0, Passed:  9692, Skipped:     2, Total:  9694, Duration: 16 s - TAOM.Tests.dll (net472)`.
  That example is a real TAOM run, from `fullsuite4.txt` in an earlier session's scratchpad. The
  same run has no `Total tests` line (`grep -c "Total tests"` gives `0`). The `Total tests:` block
  shows up only in logs made with a verbose console logger.
- Counts are padded with spaces (`Failed:     0, Passed:     9`). So the literal `Passed: 9`,
  `Failed: 0` in each step never matches. Two plans from the same audit already record this:
  `plans/008-binding-gate-no-silent-skips.md:257-259` ("Never grep a literal `Failed: 0`") and
  `plans/010-ci-on-hosted-windows.md:383`.
- The default output names a failing test by method only (`  Failed EveryDoc... [5 ms]`), never by
  class. The Step 0.6 STOP rule (L756-758, "any failure in a class under `TAOM.Tests/Features/Warg/`
  or `AdvancedCombat/`") therefore depends on a mapping the plan never provides.
- **Effect:** a literal executor fails the Step 0 check and, under "fails twice", stops, or it
  improvises a match.
- **Fix:** expect the `Passed!`/`Failed!` banner with `Total:`. Check counts with a regex, for
  example `grep -E 'Failed:\s+0,'` and `grep -E 'Passed:\s+9,'`. Map a failing method to its file
  with `git -C W grep -n "void <Name>(" -- TAOM.Tests`.

## Non-blocking

1. **Mismatches in "Current state" (the code is right; the claims around it are not).**
   - L458: "The only production `new BoneCheckDuringAnimation` is `AgentAdapter.cs:215`". There is a
     second one at `Main/Features/AdvancedCombat/Services/BoneCollisionService.cs:25`
     (`CreateAnimationBoneCheck`). Nothing in the plan depends on it, since both go through the class
     being changed. Correct the sentence so an executor who greps does not stop on it.
   - L351: the command `git grep -n '"_grid"\|GetCell' -- Main TAOM.Tests` is said to find nothing.
     At `b2e387db` it finds `SpatialGrid.cs:43` and `:77`, which are in the file itself. The
     conclusion (nothing outside the file) still holds; reword it as "finds only SpatialGrid.cs".
   - L594: "`Main/SubModule.cs:2115-2135` (`ResetForUnload` calls after `IoC.Dispose()`)". At
     `b2e387db`, `IoC.Dispose()` is at line 2127 and the `ResetForUnload` calls run from 2133 to 2146.
   - L243: `git grep -n "WargRiderHandManager\." -- Main` returns three lines (`:68` comment,
     `:73` `OnMainAgentDismount`, `:115`), not just the `Tick` call. The claim about `Tick` is right,
     but the command, quoted as its proof, prints more than that.
2. **The note on order within a column understates the change** (L1794-1797). Two agents in the same
   (x, y) column land in different z bands whenever a multiple of 20 m lies between their heights, so
   agents in adjacent bands also reorder. "More than one height band apart" is wrong. Any slope that
   crosses z = 20k can produce two such targets in bone reach. So L61 ("identical hit results") and
   the CHANGELOG text at L1689-1694 overclaim: the set of hittable targets is unchanged, but with
   `stopOnFirstHit` the target that takes the hit can change. Reword both.
3. **No fix is given for a failed length check after a commit** (L713-718, L1745-1747). If the awk or
   python check does not print `0` or a number of 72 or less, the executor is not told whether to
   `git commit --amend` (allowed on an unpushed branch it owns) or to stop.
4. **Step 3 refactors before its characterising tests exist** (L1025-1121, then Step 4). This is
   acceptable because the brute-force sphere scan is the specification and passes either way, but the
   step order is REFACTOR then characterise, not the other way round. It is worth one sentence saying so.
5. **Data command, UNVERIFIED** (L653): `validate_moduledata.py` imports `validate_mesh_refs`, which
   also prints an `error(s)` summary (`tools/validate_mesh_refs.py:995`). The grep may print more than
   the "one summary line" promised. The check compares with Step 0, so this is low risk.
6. **Commit hooks read the main checkout.** `check-changelog-changed.sh` checks staged files in
   `CLAUDE_PROJECT_DIR` (the main checkout, where another session has `.claude/` edits). L1775-1776
   already covers this with a STOP that reports the hook message word for word. Good, but expect it
   to fire if that session stages `.claude/` files.

## Checklist

| Item | Result |
|---|---|
| Self-contained (paths, excerpts, commands, engine facts) | Yes, apart from B1 and the test-name-to-class mapping |
| Every step ends in a command with an expected result | Yes; the expectations need the B1 format fix |
| Excerpts match `b2e387db` | All code excerpts match exactly (PeriodicallyCheck 12-67, NoEnemyClose 9-28, WargAiControlled 16/24-29, WargAttackTask 23-34, RiderHand 8-18, WargMissionBehavior 1-43/115/152-179, IoC 226/236-239, SubModule 1960, SpatialGrid 17-146, BoneCheck 53-147, BCDA 14-59, docs L126/150-153 and L50/74/76). Surrounding claims: non-blocking 1 |
| Engine facts | Match the v1.5.3 cache (MBAgentVisuals 32-37/145-148, ScriptingInterface 776-786, NativeObject 32-62, Agent 706/710, MatrixFrame 110-114, Mat3 45, Vec3 160/334-336, AgentReadOnlyList 6, MBReadOnlyList 5) |
| TDD order | Step 1 RED then 2 GREEN; 5 RED then 6 GREEN; 8 RED then 9 GREEN. Refactors 3 and 7 are guarded by existing and characterising tests |
| Issue-first | "create before implementation lands (orchestrator)" (L46) |
| Binding ADRs named with summaries | ADR-002, 003, 004, 005, 007, 008, plus AGENTS "Verify before reference" and the `csharp-architecture.md` agent-handle rule (L584-614) |
| Single-owner files | `SubModule.cs`, `IoC.cs`, `TAOM.csproj`, `Directory.Build.props` are recommend-only; the constructor signature is kept (L678-682) |
| STOP conditions specific | Yes (L1750-1778) |
| Done criteria machine-checkable | Yes, once B1 fixes the count format |
| Planned-at SHA and drift paths | `b2e387db`; the drift list (L30) is the same 14 paths as In scope (L663-676) |
| Non-deploying commands | Build and test carry `-p:DisableModuleCopy=true -p:ModuleId=`; `./build.ps1` is forbidden (L646) |
| Dashes and secrets | The only U+2013/U+2014 characters are at L320 and L469, both inside verbatim code excerpts (exempt). No secret values |
